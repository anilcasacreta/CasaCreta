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
                "Fillet Edge cc",
                "Fillet cc",
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

        private static readonly Bitmap _cachedIcon = LoadIcon();

        private static Bitmap LoadIcon()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAFMklEQVR4nK2VW2xUVRSGv31uc+u0QKUUylAYbgXBSpCg0YTExGiDRjSWRFKBJlLEEAhCTHxqeFEfuCRCRFFQgrSIQEAxoCFEjfVCUkWtbQoIFWlpp6UX2pnOzDlnb3NmIFLuD/7JPslO9ln//6+91tqCG1BVhbl9O/b364oqTv2Z2H3mYlrqptBQ4DrIyRFLzpwWeHLextYT78/GXF6PzR0grt/sK59uLfysMf3VqqKKht8HdlmjRqni+ws015VCaAI3rWRjfYfS413JyaWB8vKt7Uery7HWf0b6rgTV8zDWf4vz9crRy35rSm43RxbIynVzRO7UEQJbgj8EXf9w7sB3qqa2VdhmLjOnhZ4p/7DtyJ2ceASiuhzTU3FoReErpxsHt+WML3JeWj1bzxkVErLfRvj8yP5OnKY6fP2ttMWQu78QKi5z1MTpoReXftS+f1851sJbOBHXlB+uKlh79py9IVA02lmy5iE9OMIv3ISLZvmRfR3YTXWorktgQcAP7R2ojw9DirCIRoMvLv6kY++1WDel6NCygjWnm1Ob8qJjnSVrZ+u+4QGBZYCpQXcbNNUh29pJugIhVObHgA+6upE79wsVVzn6hKn+xZW7Onff6EQcf7XgzcYW7Y3QuGJ70apZhj8/IJJdCc43d5IYSDLQVE9/aweTJ0A0kqkk5FVlfgsu96B2HUL2O2E9MiFQ9XJN7IPrnYitT+WpATOsXnt7tjB9OsrSiZ27wuvLf+JUS4IHh9v4QlA2F557AlJpcLMmsiQ+6O1F7TqI222HjGg0tKKyJvbeVSe2calXuZFIu25yHLfdRQ8JZLckFU8RtuDhmVA2D/Lvg8HUzbWdTEHeMMSS59FrjsSdlrNs27GoILSwJrbRO6r5LKG7roNKxhEyCQySiKfIsWBNpUAbDt/8mA3ms4ZWiFLZlUrBsDxExQL0/Jy48/eZxIZPKwrWqH1ompIqI0lo/0nTdMgJwuSpigVLobkbdtbClYFsSryg3lHLAl3L7pN21skLj6AHewfSLS3JTcd+GjNfG1JS3i4OYwpg1ToYH4VRY2H5augTsGM/9PZnK8i76LQNnj5NAyFg4AIUuogphWitXba63CeNoQSeLBt8foiWQm4eqB4YPwUqX4VBE7bXQiINnX2wbQ+0XAJ/ENIdkG4DmQZhgmEK4QipMkNsCDxKCaoPvELzSGU/RCZlSWQu7PkcLrbCb2ehrx/ogVQbKBeEAV6rOK4inc6GuyUy6bqesw+KSmDFWggVwt4TkPLc9oHTCrigecE9Xba3lBKa8vjuDiVAC8MfJ+Dbn6H9PBxtgEIv91dA5YFmQtCEk2dQP7Rpcub9fqM4Tzj3RJCBANeFnk4Yng8rF4AvBjkBcFQ2eP05VM0vwh1bErYixdZrTzwa+/KeCIT3GYAH50LpYyD8wGVIHYNkT/ae6s+ian/X3Mi0XGPaJGvd01tjm9l6NdNeHUvvYm+zpLzaVN4IGwSVACcOtgumDif/Qn3yq+aOKwkbpSX+Fc++G9vojQovtuH95/cjtBFAIlumQ985hm69BvCDkQZDh7pm1P4zupz4QNgoiVpVZe+0Z4bdtYlqKFeJtouoCydxnSQI52aCIfAUeR0ch+YG1MEGXZs0K6zPmGguLtsS291QjjXjunFtFA3TEr80msH33xJGylGZVNwLhOfEEZTOCVIYsRaVbYnVVldjzFg/9FUz5s8Njhw9xlzmCO3RjiuuK5XS74VAKtxIvq6HQ3z6+ObYgcx4viH4/4bq6ts37L+h4EoibJfolAAAAABJRU5ErkJggg==");
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
            new Guid("E84D2319-5F10-4A3C-91B2-4A738202CD12");
    }
}
