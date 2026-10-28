using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace CasaCreta.ReDev
{
    /// <summary>
    /// Batched multi-threaded Fillet Edge component that processes geometry in
    /// controlled batches to avoid freezing Rhino with large datasets (10K+ items).
    ///
    /// Each Grasshopper solution cycle processes one batch of breps, outputs
    /// accumulated results so far, then auto-triggers the next batch until all
    /// items are complete.
    /// </summary>
    public sealed class ccFilletEdgeV2 : GH_Component
    {
        // ─── Persistent state across solution cycles ─────────────────────

        /// <summary>Hash of current input configuration to detect changes.</summary>
        private int _inputHash;

        /// <summary>Index of the next brep to process (batch cursor).</summary>
        private int _nextIndex;

        /// <summary>Accumulated results across all batches.</summary>
        private Brep[] _accumulatedResults;

        /// <summary>Total number of items in the current job.</summary>
        private int _totalCount;

        /// <summary>Number of items completed so far.</summary>
        private int _completedCount;

        /// <summary>Flag to prevent re-entrant expiration loops.</summary>
        private bool _isScheduled;

        // ─── Constructor ─────────────────────────────────────────────────

        public ccFilletEdgeV2()
            : base(
                "Fillet Edge cc v2",
                "Fillet cc v2",
                "Batched fillet edge component — processes geometry in controlled batches "
                + "to handle 10K+ items without freezing Rhino. Shows real-time progress.",
                "CasaCreta",
                "ReDev")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        // ─── Parameters ──────────────────────────────────────────────────

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Shape", "S",
                "Shapes to fillet.",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter(
                "Blend", "B",
                "Fillet blend type (0 = Fillet, 1 = Chamfer, 2 = Blend).",
                GH_ParamAccess.item, 0);

            pManager.AddIntegerParameter(
                "Metric", "M",
                "Fillet metric type (0 = RollingBall, 1 = DistanceBetweenRails, 2 = DistanceFromEdge).",
                GH_ParamAccess.item, 0);

            pManager.AddIntegerParameter(
                "Edges", "E",
                "Edge indices to fillet per brep. If empty, all edges are considered.",
                GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Radii", "R",
                "Fillet radii/measures per edge.",
                GH_ParamAccess.list, 1.0);

            pManager.AddBooleanParameter(
                "All", "A",
                "When True, process ALL items at once (no batching). "
                + "When False, process in controlled batches. Default is False.",
                GH_ParamAccess.item, false);

            pManager.AddIntegerParameter(
                "BatchSize", "N",
                "Number of breps to process per solution cycle (only used when All is False). "
                + "Smaller = more responsive UI but slower total. "
                + "Larger = faster total but longer freezes. Default 10.",
                GH_ParamAccess.item, 10);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Result", "R",
                "Filleted Brep results (accumulated — updates each batch).",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "Status", "S",
                "Progress status message.",
                GH_ParamAccess.item);
        }

        // ─── Core Solve ──────────────────────────────────────────────────

        protected override void SolveInstance(IGH_DataAccess access)
        {
            // 1. Read all inputs
            var shapes = new List<Brep>();
            int blendInt = 0;
            int metricInt = 0;
            var edgeIndices = new List<int>();
            var radii = new List<double>();
            bool processAll = false;
            int batchSize = 10;

            if (!access.GetDataList(0, shapes) || shapes.Count == 0) return;
            access.GetData(1, ref blendInt);
            access.GetData(2, ref metricInt);
            access.GetDataList(3, edgeIndices);
            access.GetDataList(4, radii);
            access.GetData(5, ref processAll);
            access.GetData(6, ref batchSize);

            if (radii.Count == 0) radii.Add(1.0);
            batchSize = Math.Max(1, Math.Min(batchSize, 10000));

            double tol = RhinoDoc.ActiveDoc != null
                ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
                : 0.001;
            if (tol <= 0.0) tol = 0.001;

            BlendType blendType = ResolveBlendType(blendInt);
            RailType railType = ResolveRailType(metricInt);

            int shapeCount = shapes.Count;
            int coreCount = Math.Max(1, Environment.ProcessorCount);

            // ── ALL-AT-ONCE MODE ──────────────────────────────────────────
            if (processAll)
            {
                Message = $"{coreCount} Threads (All)";
                var results = new Brep[shapeCount];

                // Use all logical cores (hyperthreading included)
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = -1  // unlimited = all logical cores
                };

                if (shapeCount < 4)
                {
                    for (int i = 0; i < shapeCount; i++)
                        results[i] = FilletSingleBrep(shapes[i], edgeIndices, radii, blendType, railType, tol);
                }
                else
                {
                    int chunkSize = Math.Max(2, shapeCount / (coreCount * 2));
                    var partitioner = Partitioner.Create(0, shapeCount, chunkSize);

                    Parallel.ForEach(partitioner, options, range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                            results[i] = FilletSingleBrep(shapes[i], edgeIndices, radii, blendType, railType, tol);
                    });
                }

                access.SetDataList(0, results);
                access.SetData(1, $"Complete: {shapeCount} items ({coreCount} threads)");

                // Reset batch state so toggling back to batch mode works
                _inputHash = 0;
                _nextIndex = 0;
                _totalCount = 0;
                _accumulatedResults = null;
                _completedCount = 0;
                _isScheduled = false;
                return;
            }

            // ── BATCHED MODE ─────────────────────────────────────────────

            // Detect input changes — reset state if inputs changed
            int currentHash = ComputeInputHash(shapeCount, blendInt, metricInt,
                                               edgeIndices, radii, batchSize);

            if (currentHash != _inputHash)
            {
                _inputHash = currentHash;
                _nextIndex = 0;
                _totalCount = shapeCount;
                _accumulatedResults = new Brep[_totalCount];
                _completedCount = 0;
                _isScheduled = false;
            }

            // Determine this batch's range
            int batchStart = _nextIndex;
            int batchEnd = Math.Min(batchStart + batchSize, _totalCount);
            int batchCount = batchEnd - batchStart;

            if (batchCount <= 0)
            {
                Message = $"Done ({_totalCount})";
                OutputResults(access);
                _isScheduled = false;
                return;
            }

            // Process this batch — parallel within the batch, all logical cores
            var batchOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = -1
            };

            if (batchCount < 4)
            {
                for (int i = batchStart; i < batchEnd; i++)
                {
                    _accumulatedResults[i] = FilletSingleBrep(
                        shapes[i], edgeIndices, radii, blendType, railType, tol);
                }
            }
            else
            {
                int chunkSize = Math.Max(2, batchCount / (coreCount * 2));
                var partitioner = Partitioner.Create(batchStart, batchEnd, chunkSize);

                Parallel.ForEach(partitioner, batchOptions, range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        _accumulatedResults[i] = FilletSingleBrep(
                            shapes[i], edgeIndices, radii, blendType, railType, tol);
                    }
                });
            }

            // Advance cursor
            _completedCount += batchCount;
            _nextIndex = batchEnd;

            // Update progress display
            double pct = (double)_completedCount / _totalCount * 100.0;
            Message = $"{_completedCount}/{_totalCount} ({pct:F0}%)";

            // Output accumulated results so far
            OutputResults(access);

            // Schedule next batch if more items remain
            if (_nextIndex < _totalCount && !_isScheduled)
            {
                _isScheduled = true;
                var doc = OnPingDocument();
                if (doc != null)
                {
                    doc.ScheduleSolution(50, d =>
                    {
                        _isScheduled = false;
                        ExpireSolution(false);
                    });
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private void OutputResults(IGH_DataAccess access)
        {
            // Build output list from accumulated results (may contain nulls for unprocessed items)
            var output = new List<Brep>(_completedCount);
            int limit = Math.Min(_nextIndex, _accumulatedResults.Length);
            for (int i = 0; i < limit; i++)
            {
                if (_accumulatedResults[i] != null)
                    output.Add(_accumulatedResults[i]);
            }

            access.SetDataList(0, output);

            string status = _nextIndex >= _totalCount
                ? $"Complete: {_totalCount} items processed"
                : $"Processing: {_completedCount}/{_totalCount} done ({(_completedCount * 100.0 / _totalCount):F1}%)";
            access.SetData(1, status);
        }

        private static int ComputeInputHash(
            int count, int blend, int metric,
            List<int> edges, List<double> radii, int batchSize)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + count;
                hash = hash * 31 + blend;
                hash = hash * 31 + metric;
                hash = hash * 31 + batchSize;
                hash = hash * 31 + (edges != null ? edges.Count : 0);
                if (radii != null)
                {
                    for (int i = 0; i < Math.Min(radii.Count, 8); i++)
                        hash = hash * 31 + radii[i].GetHashCode();
                }
                return hash;
            }
        }

        // ─── Fillet Logic (same as v1) ───────────────────────────────────

        private static Brep FilletSingleBrep(
            Brep inputBrep,
            List<int> edgeIndices,
            List<double> radii,
            BlendType blendType,
            RailType railType,
            double tolerance)
        {
            if (inputBrep == null || !inputBrep.IsValid) return inputBrep;

            // Determine target edges
            var targetEdges = new List<int>();
            if (edgeIndices != null && edgeIndices.Count > 0)
            {
                for (int e = 0; e < edgeIndices.Count; e++)
                {
                    int idx = edgeIndices[e];
                    if (idx >= 0 && idx < inputBrep.Edges.Count)
                    {
                        targetEdges.Add(idx);
                    }
                }
            }
            else
            {
                // If no edges specified, fillet all valid edges
                for (int e = 0; e < inputBrep.Edges.Count; e++)
                {
                    targetEdges.Add(e);
                }
            }

            if (targetEdges.Count == 0) return inputBrep;

            // Build start and end radii per target edge
            var startRadii = new double[targetEdges.Count];
            var endRadii = new double[targetEdges.Count];
            int radiiCount = radii.Count;

            for (int e = 0; e < targetEdges.Count; e++)
            {
                double r = radiiCount == 1 ? radii[0] : radii[Math.Min(e, radiiCount - 1)];
                startRadii[e] = r;
                endRadii[e] = r;
            }

            try
            {
                var filleted = Brep.CreateFilletEdges(
                    inputBrep,
                    targetEdges,
                    startRadii,
                    endRadii,
                    blendType,
                    railType,
                    tolerance);

                if (filleted != null && filleted.Length > 0
                    && filleted[0] != null && filleted[0].IsValid)
                {
                    return filleted[0];
                }
            }
            catch
            {
                // Fallback to original Brep on kernel exception
            }

            return inputBrep;
        }

        private static BlendType ResolveBlendType(int blend)
        {
            switch (blend)
            {
                case 1: return BlendType.Chamfer;
                case 2: return BlendType.Blend;
                default: return BlendType.Fillet;
            }
        }

        private static RailType ResolveRailType(int metric)
        {
            switch (metric)
            {
                case 1: return RailType.DistanceBetweenRails;
                case 2: return RailType.DistanceFromEdge;
                default: return RailType.RollingBall;
            }
        }

        // ─── Icon ────────────────────────────────────────────────────────

        private static readonly Bitmap _cachedIcon = LoadIcon();

        private static Bitmap LoadIcon()
        {
            try
            {
                // Reuse the same fillet icon from v1
                byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAFMklEQVR4nK2VW2xUVRSGv31uc+u0QKUUylAYbgXBSpCg0YTExGiDRjSWRFKBJlLEEAhCTHxqeFEfuCRCRFFQgrSIQEAxoCFEjfVCUkWtbQoIFWlpp6UX2pnOzDlnb3NmIFLuD/7JPslO9ln//6+91tqCG1BVhbl9O/b364oqTv2Z2H3mYlrqptBQ4DrIyRFLzpwWeHLextYT78/GXF6PzR0grt/sK59uLfysMf3VqqKKht8HdlmjRqni+ws015VCaAI3rWRjfYfS413JyaWB8vKt7Uery7HWf0b6rgTV8zDWf4vz9crRy35rSm43RxbIynVzRO7UEQJbgj8EXf9w7sB3qqa2VdhmLjOnhZ4p/7DtyJ2ceASiuhzTU3FoReXftS+f1851sJbOBHXlB+uKlh79py9IVA02lmy5iE9OMIv3ISLZvmRfR3YTXWorktgQcAP7R2ojw9DirCIRoMvLv6kY++1WDel6NCygjWnm1Ob8qJjnSVrZ+u+4QGBZYCpQXcbNNUh29pJugIhVObHgA+6upE79wsVVzn6hKn+xZW7Onff6EQcf7XgzcYW7Y3QuGJ70apZhj8/IJJdCc43d5IYSDLQVE9/aweTJ0A0kqkk5FVlfgsu96B2HUL2O2E9MiFQ9XJN7IPrnYitT+WpATOsXnt7tjB9OsrSiZ27wuvLf+JUS4IHh9v4QlA2F557AlJpcLMmsiQ+6O1F7TqI222HjGg0tKKyJvbeVSe2calXuZFIu25yHLfdRQ8JZLckFU8RtuDhmVA2D/Lvg8HUzbWdTEHeMMSS59FrjsSdlrNs27GoILSwJrbRO6r5LKG7roNKxhEyCQySiKfIsWBNpUAbDt/8mA3ms4ZWiFLZlUrBsDxExQL0/Jy48/eZxIZPKwrWqH1ompIqI0lo/0nTdMgJwuSpigVLobkbdtbClYFsSryg3lHLAl3L7pN21skLj6AHewfSLS3JTcd+GjNfG1JS3i4OYwpg1ToYH4VRY2H5augTsGM/9PZnK8i76LQNnj5NAyFg4AIUuogphWitXba63CeNoQSeLBt8foiWQm4eqB4YPwUqX4VBE7bXQiINnX2wbQ+0XAJ/ENIdkG4DmQZhgmEK4QipMkNsCDxKCaoPvELzSGU/RCZlSWQu7PkcLrbCb2ehrx/ogVQbKBeEAV6rOK4inc6GuyUy6bqesw+KSmDFWggVwt4TkPLc9oHTCrigecE9Xba3lBKa8vjuDiVAC8MfJ+Dbn6H9PBxtgEIv91dA5YFmQtCEk2dQP7Rpcub9fqM4Tzj3RJCBANeFnk4Yng8rF4AvBjkBcFQ2eP05VM0vwh1bErYixdZrTzwa+/KeCIT3GYAH50LpYyD8wGVIHYNkT/ae6s+ian/X3Mi0XGPaJGvd01tjm9l6NdNeHUvvYm+zpLzaVN4IGwSVACcOtgumDif/Qn3yq+aOKwkbpSX+Fc++G9vojQovtuH95/cjtBFAIlumQ985hm69BvCDkQZDh7pm1P4zupz4QNgoiVpVZe+0Z4bdtYlqKFeJtouoCydxnSQI52aCIfAUeR0ch+YG1MEGXZs0K6zPmGguLtsS291QjjXjunFtFA3TEr80msH33xJGylGZVNwLhOfEEZTOCVIYsRaVbYnVVldjzFg/9FUz5s8Njhw9xlzmCO3RjiuuK5XS74VAKtxIvq6HQ3z6+ObYgcx4viH4/4bq6ts37L+h4EoibJfolAAAAABJRU5ErkJggg==");
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    return new Bitmap(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        protected override Bitmap Icon => _cachedIcon;

        public override Guid ComponentGuid =>
            new Guid("F92C4A71-3D68-4E5B-A019-8E2B6D174C30");
    }
}
