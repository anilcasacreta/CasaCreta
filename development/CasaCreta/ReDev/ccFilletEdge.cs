using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace CasaCreta.ReDev
{
    /// <summary>
    /// Multi-threaded Fillet Edge component for Breps executing in parallel
    /// across all CPU cores.
    /// </summary>
    public sealed class ccFilletEdge : GH_Component
    {
        public ccFilletEdge()
            : base(
                "ccFilletEdge",
                "ccFillet",
                "Fillet some edges of a brep using multi-threaded parallel execution across all CPU cores.",
                "CasaCreta",
                "ReDev")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Shape",
                "S",
                "Shape or shapes to fillet.",
                GH_ParamAccess.list);

            pManager.AddIntegerParameter(
                "Blend",
                "B",
                "Fillet blend type (0 = Fillet, 1 = Chamfer, 2 = Blend).",
                GH_ParamAccess.item,
                0);

            pManager.AddIntegerParameter(
                "Metric",
                "M",
                "Fillet metric type (0 = RollingBall, 1 = DistanceBetweenRails, 2 = DistanceFromEdge).",
                GH_ParamAccess.item,
                0);

            pManager.AddIntegerParameter(
                "Edges",
                "E",
                "Edge indices to fillet per brep. If empty, all edges are considered.",
                GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Radii",
                "R",
                "Fillet radii/measures per edge.",
                GH_ParamAccess.list,
                1.0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Result",
                "R",
                "Filleted Brep results.",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            var shapes = new List<Brep>();
            int blendInt = 0;
            int metricInt = 0;
            var edgeIndices = new List<int>();
            var radii = new List<double>();

            if (!access.GetDataList(0, shapes) || shapes.Count == 0) return;
            access.GetData(1, ref blendInt);
            access.GetData(2, ref metricInt);
            access.GetDataList(3, edgeIndices);
            access.GetDataList(4, radii);

            if (radii.Count == 0) radii.Add(1.0);

            // Resolve tolerance safely on the main UI thread
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;
            if (tol <= 0.0) tol = 0.001;

            BlendType blendType = ResolveBlendType(blendInt);
            RailType railType = ResolveRailType(metricInt);

            int shapeCount = shapes.Count;
            int coreCount = Math.Max(1, Environment.ProcessorCount);
            Message = $"{coreCount} Threads";

            var results = new Brep[shapeCount];

            // If only 1 shape, run sequentially
            if (shapeCount == 1)
            {
                results[0] = FilletSingleBrep(shapes[0], edgeIndices, radii, blendType, railType, tol);
                access.SetDataList(0, results);
                return;
            }

            // Range-chunked parallel execution across all CPU threads
            int chunkSize = Math.Max(4, shapeCount / (coreCount * 2));
            var partitioner = Partitioner.Create(0, shapeCount, chunkSize);

            Parallel.ForEach(partitioner, range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    results[i] = FilletSingleBrep(shapes[i], edgeIndices, radii, blendType, railType, tol);
                }
            });

            access.SetDataList(0, results);
        }

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
                // Call RhinoCommon's native kernel
                var filleted = Brep.CreateFilletEdges(
                    inputBrep,
                    targetEdges,
                    startRadii,
                    endRadii,
                    blendType,
                    railType,
                    tolerance);

                if (filleted != null && filleted.Length > 0 && filleted[0] != null && filleted[0].IsValid)
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

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("E84D2319-5F10-4A3C-91B2-4A738202CD12");
    }
}
