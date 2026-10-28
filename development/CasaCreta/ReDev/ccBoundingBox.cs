using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace CasaCreta.ReDev
{
    /// <summary>
    /// High-performance multi-threaded Bounding Box component using
    /// chunked parallel execution across all CPU cores.
    /// Supports per-object and union modes, with optional plane input
    /// for oriented bounding boxes.
    /// </summary>
    public sealed class ccBoundingBox : GH_Component
    {
        public ccBoundingBox()
            : base(
                "Bounding Box cc",
                "BBox cc",
                "Computes axis-aligned or plane-oriented bounding boxes using multi-threaded parallel execution.",
                "CasaCreta",
                "ReDev")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Geometry",
                "G",
                "Geometry objects to compute bounding boxes for.",
                GH_ParamAccess.list);

            pManager.AddPlaneParameter(
                "Plane",
                "P",
                "Optional orientation plane. When supplied, bounding boxes are computed relative to this plane (oriented bounding box). Default is WorldXY (axis-aligned).",
                GH_ParamAccess.item,
                Plane.WorldXY);
            pManager[1].Optional = true;

            pManager.AddBooleanParameter(
                "Union",
                "U",
                "If True, return a single bounding box enclosing all geometry. If False, return one box per input object. Default is False (per-object).",
                GH_ParamAccess.item,
                false);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter(
                "Box",
                "B",
                "Resulting bounding box(es).",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            var geoGoos = new List<IGH_GeometricGoo>();
            if (!access.GetDataList(0, geoGoos) || geoGoos.Count == 0) return;

            Plane plane = Plane.WorldXY;
            access.GetData(1, ref plane);
            if (!plane.IsValid)
                plane = Plane.WorldXY;

            bool union = false;
            access.GetData(2, ref union);

            bool isWorldXY = IsWorldXY(plane);

            int count = geoGoos.Count;

            // Pre-extract pure geometry on the main thread
            var rawGeometry = new GeometryBase[count];
            for (int i = 0; i < count; i++)
            {
                var goo = geoGoos[i];
                if (goo == null) continue;

                object scriptObj = goo.ScriptVariable();
                switch (scriptObj)
                {
                    case GeometryBase gb:
                        rawGeometry[i] = gb;
                        break;
                    case Point3d pt:
                        rawGeometry[i] = new Rhino.Geometry.Point(pt);
                        break;
                    default:
                        rawGeometry[i] = null;
                        break;
                }
            }

            int coreCount = Math.Max(1, Environment.ProcessorCount);
            Message = $"{coreCount} Threads";

            if (union)
            {
                // --- Union Mode: single box enclosing everything ---
                Box resultBox = ComputeUnionBox(rawGeometry, count, coreCount, plane, isWorldXY);
                if (resultBox.IsValid)
                    access.SetDataList(0, new[] { resultBox });
            }
            else
            {
                // --- Per-Object Mode: one box per geometry ---
                var results = ComputePerObjectBoxes(rawGeometry, count, coreCount, plane, isWorldXY);
                access.SetDataList(0, results);
            }
        }

        /// <summary>
        /// Compute one bounding box per input geometry, using parallel chunked execution.
        /// </summary>
        private static Box[] ComputePerObjectBoxes(
            GeometryBase[] geometry, int count, int coreCount,
            Plane plane, bool isWorldXY)
        {
            var results = new Box[count];

            if (count < 16)
            {
                // Small list: sequential to avoid thread overhead
                for (int i = 0; i < count; i++)
                    results[i] = ComputeSingleBox(geometry[i], plane, isWorldXY);
            }
            else
            {
                // Chunked parallel execution
                int chunkSize = Math.Max(16, count / (coreCount * 2));
                var partitioner = Partitioner.Create(0, count, chunkSize);

                Parallel.ForEach(partitioner, range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                        results[i] = ComputeSingleBox(geometry[i], plane, isWorldXY);
                });
            }

            return results;
        }

        /// <summary>
        /// Compute a single union bounding box enclosing all geometry.
        /// Uses parallel reduction for large sets.
        /// </summary>
        private static Box ComputeUnionBox(
            GeometryBase[] geometry, int count, int coreCount,
            Plane plane, bool isWorldXY)
        {
            if (isWorldXY)
            {
                // Fast path: axis-aligned union using BoundingBox (parallelisable struct merge)
                if (count < 64)
                {
                    BoundingBox merged = BoundingBox.Empty;
                    for (int i = 0; i < count; i++)
                    {
                        if (geometry[i] == null) continue;
                        merged.Union(geometry[i].GetBoundingBox(true));
                    }
                    return WorldBBoxToBox(merged);
                }
                else
                {
                    // Parallel reduction: each chunk computes local union, then merge
                    int chunkSize = Math.Max(64, count / (coreCount * 2));
                    var partitioner = Partitioner.Create(0, count, chunkSize);
                    var partialBoxes = new ConcurrentBag<BoundingBox>();

                    Parallel.ForEach(partitioner, range =>
                    {
                        BoundingBox local = BoundingBox.Empty;
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            if (geometry[i] == null) continue;
                            local.Union(geometry[i].GetBoundingBox(true));
                        }
                        if (local.IsValid)
                            partialBoxes.Add(local);
                    });

                    BoundingBox final = BoundingBox.Empty;
                    foreach (var pb in partialBoxes)
                        final.Union(pb);

                    return WorldBBoxToBox(final);
                }
            }
            else
            {
                // Oriented union: transform all geometry into plane-local coords,
                // compute axis-aligned bbox there, then transform back
                Transform toLocal = Transform.ChangeBasis(Plane.WorldXY, plane);
                Transform toWorld = Transform.ChangeBasis(plane, Plane.WorldXY);

                if (count < 64)
                {
                    BoundingBox merged = BoundingBox.Empty;
                    for (int i = 0; i < count; i++)
                    {
                        if (geometry[i] == null) continue;
                        BoundingBox bb = geometry[i].GetBoundingBox(toLocal);
                        merged.Union(bb);
                    }
                    return LocalBBoxToOrientedBox(merged, plane, toWorld);
                }
                else
                {
                    int chunkSize = Math.Max(64, count / (coreCount * 2));
                    var partitioner = Partitioner.Create(0, count, chunkSize);
                    var partialBoxes = new ConcurrentBag<BoundingBox>();

                    Parallel.ForEach(partitioner, range =>
                    {
                        BoundingBox local = BoundingBox.Empty;
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            if (geometry[i] == null) continue;
                            BoundingBox bb = geometry[i].GetBoundingBox(toLocal);
                            local.Union(bb);
                        }
                        if (local.IsValid)
                            partialBoxes.Add(local);
                    });

                    BoundingBox final = BoundingBox.Empty;
                    foreach (var pb in partialBoxes)
                        final.Union(pb);

                    return LocalBBoxToOrientedBox(final, plane, toWorld);
                }
            }
        }

        /// <summary>
        /// Compute the bounding box of a single geometry object.
        /// </summary>
        private static Box ComputeSingleBox(GeometryBase geo, Plane plane, bool isWorldXY)
        {
            if (geo == null) return Box.Empty;

            if (isWorldXY)
            {
                BoundingBox bb = geo.GetBoundingBox(true);
                return bb.IsValid ? WorldBBoxToBox(bb) : Box.Empty;
            }
            else
            {
                Transform toLocal = Transform.ChangeBasis(Plane.WorldXY, plane);
                Transform toWorld = Transform.ChangeBasis(plane, Plane.WorldXY);
                BoundingBox bb = geo.GetBoundingBox(toLocal);
                return bb.IsValid ? LocalBBoxToOrientedBox(bb, plane, toWorld) : Box.Empty;
            }
        }

        /// <summary>
        /// Convert an axis-aligned BoundingBox to a Box at WorldXY.
        /// </summary>
        private static Box WorldBBoxToBox(BoundingBox bb)
        {
            if (!bb.IsValid) return Box.Empty;
            return new Box(
                Plane.WorldXY,
                new Interval(bb.Min.X, bb.Max.X),
                new Interval(bb.Min.Y, bb.Max.Y),
                new Interval(bb.Min.Z, bb.Max.Z));
        }

        /// <summary>
        /// Convert a local-coordinate BoundingBox into an oriented Box in world space.
        /// </summary>
        private static Box LocalBBoxToOrientedBox(BoundingBox bb, Plane plane, Transform toWorld)
        {
            if (!bb.IsValid) return Box.Empty;

            // Build the box plane at the local min corner, oriented to the input plane
            Plane boxPlane = new Plane(
                plane.PointAt(bb.Min.X, bb.Min.Y, bb.Min.Z),
                plane.XAxis,
                plane.YAxis);

            return new Box(
                boxPlane,
                new Interval(0, bb.Max.X - bb.Min.X),
                new Interval(0, bb.Max.Y - bb.Min.Y),
                new Interval(0, bb.Max.Z - bb.Min.Z));
        }

        /// <summary>
        /// Check whether a plane is WorldXY (skip costly transforms).
        /// </summary>
        private static bool IsWorldXY(Plane p)
        {
            const double eps = 1e-10;
            return Math.Abs(p.OriginX) < eps
                && Math.Abs(p.OriginY) < eps
                && Math.Abs(p.OriginZ) < eps
                && Math.Abs(p.XAxis.X - 1.0) < eps
                && Math.Abs(p.XAxis.Y) < eps
                && Math.Abs(p.XAxis.Z) < eps
                && Math.Abs(p.YAxis.X) < eps
                && Math.Abs(p.YAxis.Y - 1.0) < eps
                && Math.Abs(p.YAxis.Z) < eps;
        }

        // ─── Icon ───────────────────────────────────────────────────────────

        private static readonly Bitmap _cachedIcon = LoadIcon();

        private static Bitmap LoadIcon()
        {
            try
            {
                // 24×24 bounding-box icon (orange wireframe cube)
                byte[] bytes = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6Q"
                  + "AAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAGfSURBVEhL"
                  + "7ZQ9SwNBEIbfvbuYS6JGo6IWgoWFjYWNhYV/wMJCsLKw8Q+IlYWFP8DKRrCw"
                  + "EGwsxEIsLC0sLCwUFEQQP+K9m3Vm7+JdchcTxcIHhp2Zd2Zndxf4ryMCIMwT"
                  + "U1MxblvOIyKCqrUdQFO9ZdLnN+j5dIhKJUJf1nVNQEqlYqB+7tX4KYZRX+A4P"
                  + "QCzKRTiN68BsYOI78SRrLlGLnlS8D8CoKnV+DAcez9WXTaJbACZFNJ5LZvIb"
                  + "+YgHn3BDi8B2a2oLhdcB7i7g18fQYur5GKO2AsQHC5huzGDeTqFbBzAOw/wsx+"
                  + "lMIvvD5TuPokvILiEDAHDAL9ekIFkNs4J4+/RfmMk4vb4PIFWLsBnh7R3o3wbE"
                  + "rg+hk4eSSsgk/O7zIUDqAAcJd9bxW24Av+eRnlK4d3YAIYkH5JC8O4/0K+eQD"
                  + "M7kT7n5J2Y8+kCyh5Aqgn/0Q8/sw1lcO/VG+WNJdgYLeyxE0Fg8f5hTdL3V/"
                  + "tWhQWwX/PgEnNzXCBFcAAAAASUVORK5CYII=");
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
            new Guid("B3F72E18-8A41-4D9C-B5E0-3C6A87F21D04");
    }
}
