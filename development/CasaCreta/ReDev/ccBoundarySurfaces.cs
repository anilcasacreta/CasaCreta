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
    /// Ultra-fast multi-threaded Boundary Surfaces component using spatial R-Tree
    /// indexing and core-chunked parallel execution across all CPU threads.
    /// </summary>
    public sealed class ccBoundarySurfaces : GH_Component
    {
        public ccBoundarySurfaces()
            : base(
                "ccBoundarySurfaces",
                "ccBoundSrf",
                "Creates planar surfaces from boundary edge curves using multi-threaded parallel execution and spatial indexing.",
                "CasaCreta",
                "ReDev")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter(
                "Edges",
                "E",
                "Planar boundary edge curves to convert into surfaces.",
                GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Surfaces",
                "S",
                "Resulting planar surfaces.",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            var rawCurves = new List<Curve>();
            if (!access.GetDataList(0, rawCurves) || rawCurves.Count == 0) return;

            // Resolve document tolerance safely on the main thread
            double tol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;
            if (tol <= 0.0) tol = 0.001;

            // Filter valid curves
            var validCurves = new List<Curve>(rawCurves.Count);
            for (int i = 0; i < rawCurves.Count; i++)
            {
                if (rawCurves[i] != null && rawCurves[i].IsValid)
                    validCurves.Add(rawCurves[i]);
            }

            int count = validCurves.Count;
            if (count == 0) return;

            int coreCount = Math.Max(1, Environment.ProcessorCount);
            Message = $"{coreCount} Threads";

            // If small list, solve directly
            if (count == 1)
            {
                var single = Brep.CreatePlanarBreps(validCurves[0], tol);
                if (single != null) access.SetDataList(0, single);
                return;
            }

            // 1. Compute 3D bounding boxes for all curves
            var bboxes = new BoundingBox[count];
            for (int i = 0; i < count; i++)
            {
                bboxes[i] = validCurves[i].GetBoundingBox(true);
            }

            // 2. Spatial Indexing using RhinoCommon RTree to cluster co-dependent curves (e.g. holes or loops)
            var uf = new DisjointSet(count);
            using (var rtree = new RTree())
            {
                for (int i = 0; i < count; i++)
                {
                    rtree.Insert(bboxes[i], i);
                }

                for (int i = 0; i < count; i++)
                {
                    int current = i;
                    var searchBox = bboxes[current];
                    searchBox.Inflate(tol);

                    rtree.Search(searchBox, (sender, args) =>
                    {
                        if (args.Id > current)
                        {
                            uf.Union(current, args.Id);
                        }
                    });
                }
            }

            // 3. Group curve indices into disjoint independent clusters
            var clusterMap = new Dictionary<int, List<Curve>>();
            for (int i = 0; i < count; i++)
            {
                int root = uf.Find(i);
                if (!clusterMap.TryGetValue(root, out var list))
                {
                    list = new List<Curve>();
                    clusterMap[root] = list;
                }
                list.Add(validCurves[i]);
            }

            var clusters = new List<List<Curve>>(clusterMap.Values);
            int clusterCount = clusters.Count;
            var clusterResults = new Brep[clusterCount][];

            // 4. Parallel Solve across all CPU cores
            int chunkSize = Math.Max(16, clusterCount / (coreCount * 2));
            var partitioner = Partitioner.Create(0, clusterCount, chunkSize);

            Parallel.ForEach(partitioner, range =>
            {
                for (int c = range.Item1; c < range.Item2; c++)
                {
                    var crvs = clusters[c];
                    if (crvs == null || crvs.Count == 0) continue;

                    Brep[] breps = null;
                    if (crvs.Count == 1)
                    {
                        var crv = crvs[0];
                        if (crv.IsClosed)
                        {
                            breps = Brep.CreatePlanarBreps(crv, tol);
                        }
                    }
                    else
                    {
                        breps = Brep.CreatePlanarBreps(crvs, tol);
                    }

                    clusterResults[c] = breps;
                }
            });

            // 5. Gather all planar surfaces
            var finalSurfaces = new List<Brep>(clusterCount);
            for (int c = 0; c < clusterCount; c++)
            {
                var breps = clusterResults[c];
                if (breps != null && breps.Length > 0)
                {
                    finalSurfaces.AddRange(breps);
                }
            }

            access.SetDataList(0, finalSurfaces);
        }

        private sealed class DisjointSet
        {
            private readonly int[] _parent;

            public DisjointSet(int size)
            {
                _parent = new int[size];
                for (int i = 0; i < size; i++)
                    _parent[i] = i;
            }

            public int Find(int i)
            {
                if (_parent[i] == i)
                    return i;
                return _parent[i] = Find(_parent[i]);
            }

            public void Union(int i, int j)
            {
                int rootI = Find(i);
                int rootJ = Find(j);
                if (rootI != rootJ)
                {
                    _parent[rootI] = rootJ;
                }
            }
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("7C3E1492-4B21-4E76-8812-70B92A478C33");
    }
}
