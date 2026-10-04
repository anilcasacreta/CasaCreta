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
    /// High-performance multi-threaded extrusion component for Points, Curves,
    /// Surfaces, and Breps using chunked parallel execution.
    /// </summary>
    public sealed class ccExtrude : GH_Component
    {
        public ccExtrude()
            : base(
                "ccExtrude",
                "ccExtrude",
                "Extrudes points, curves, and surfaces along a vector using multi-threaded parallel execution.",
                "CasaCreta",
                "ReDev")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Base",
                "B",
                "Profiles to extrude (Points, Curves, Surfaces, Breps, or Meshes).",
                GH_ParamAccess.list);

            pManager.AddVectorParameter(
                "Direction",
                "D",
                "Extrusion direction vector (length and direction).",
                GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Extrusion",
                "E",
                "Extrusion results (Curves, Surfaces, or Breps).",
                GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            var baseGoos = new List<IGH_GeometricGoo>();
            var directions = new List<Vector3d>();

            if (!access.GetDataList(0, baseGoos) || baseGoos.Count == 0) return;
            if (!access.GetDataList(1, directions) || directions.Count == 0) return;

            int count = baseGoos.Count;
            int dirCount = directions.Count;

            // Pre-extract pure geometry objects on the main thread
            var rawInputs = new object[count];
            for (int i = 0; i < count; i++)
            {
                var goo = baseGoos[i];
                if (goo == null)
                {
                    rawInputs[i] = null;
                    continue;
                }

                // ScriptVariable() unpacks GH_Point, GH_Curve, GH_Brep, etc. to pure RhinoCommon types
                object scriptObj = goo.ScriptVariable();
                if (scriptObj is Surface srf)
                {
                    rawInputs[i] = srf.ToBrep();
                }
                else
                {
                    rawInputs[i] = scriptObj;
                }
            }

            // Pre-allocate results array to guarantee exact input order and thread-safety
            var results = new GeometryBase[count];

            // For small lists (< 16 items), run sequential loop to avoid task dispatch overhead
            if (count < 16)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3d dir = dirCount == 1 ? directions[0] : directions[Math.Min(i, dirCount - 1)];
                    results[i] = ExtrudeItem(rawInputs[i], dir);
                }
            }
            else
            {
                // Range-chunked parallel execution across available CPU cores
                int coreCount = Math.Max(1, Environment.ProcessorCount);
                int chunkSize = Math.Max(16, count / (coreCount * 2));
                var partitioner = Partitioner.Create(0, count, chunkSize);

                Parallel.ForEach(partitioner, range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        Vector3d dir = dirCount == 1 ? directions[0] : directions[Math.Min(i, dirCount - 1)];
                        results[i] = ExtrudeItem(rawInputs[i], dir);
                    }
                });
            }

            Message = $"{Math.Max(1, Environment.ProcessorCount)} Threads";
            access.SetDataList(0, results);
        }

        internal static GeometryBase ExtrudeItem(object item, Vector3d direction)
        {
            if (item == null || !direction.IsValid || direction.IsZero)
                return null;

            switch (item)
            {
                case Point3d pt:
                    return new LineCurve(pt, pt + direction);

                case Rhino.Geometry.Point pointObj:
                    return new LineCurve(pointObj.Location, pointObj.Location + direction);

                case Curve curve:
                    return ExtrudeCurve(curve, direction);

                case Brep brep:
                    return ExtrudeBrep(brep, direction);

                case Mesh mesh:
                    return ExtrudeMesh(mesh, direction);

                default:
                    return null;
            }
        }

        private static GeometryBase ExtrudeCurve(Curve curve, Vector3d dir)
        {
            if (curve == null || !curve.IsValid) return null;

            // Direct RhinoCommon surface extrusion
            var srf = Surface.CreateExtrusion(curve, dir);
            if (srf != null) return srf;

            // Fallback for complex multi-segment curves where Surface.CreateExtrusion may return null
            var segments = curve.DuplicateSegments();
            if (segments != null && segments.Length > 1)
            {
                var sideFaces = new List<Brep>(segments.Length);
                for (int s = 0; s < segments.Length; s++)
                {
                    var segSrf = Surface.CreateExtrusion(segments[s], dir);
                    if (segSrf != null)
                    {
                        var b = segSrf.ToBrep();
                        if (b != null) sideFaces.Add(b);
                    }
                }

                if (sideFaces.Count > 0)
                {
                    var joined = Brep.JoinBreps(sideFaces, 0.001);
                    if (joined != null && joined.Length > 0)
                        return joined[0];
                }
            }

            return null;
        }

        private static GeometryBase ExtrudeBrep(Brep brep, Vector3d dir)
        {
            if (brep == null || !brep.IsValid) return null;

            var pieces = new List<Brep>();

            // 1. Bottom face
            var bottom = brep.DuplicateBrep();
            pieces.Add(bottom);

            // 2. Side walls: extrude only naked/outer boundary edges
            foreach (var edge in brep.Edges)
            {
                // If edge is shared between two faces on the Brep, skip it (interior seam)
                if (edge.TrimCount > 1) continue;

                var edgeCurve = edge.DuplicateCurve();
                if (edgeCurve == null) continue;

                var sideSrf = Surface.CreateExtrusion(edgeCurve, dir);
                if (sideSrf != null)
                {
                    var sideBrep = sideSrf.ToBrep();
                    if (sideBrep != null) pieces.Add(sideBrep);
                }
            }

            // 3. Top face
            var top = brep.DuplicateBrep();
            top.Translate(dir);
            pieces.Add(top);

            // Join pieces into a solid polysurface
            var joined = Brep.JoinBreps(pieces, 0.001);
            if (joined != null && joined.Length > 0)
            {
                var solid = joined[0];
                if (solid.IsSolid && solid.SolidOrientation == BrepSolidOrientation.Inward)
                {
                    solid.Flip();
                }
                return solid;
            }

            return top;
        }

        private static GeometryBase ExtrudeMesh(Mesh mesh, Vector3d dir)
        {
            if (mesh == null || !mesh.IsValid) return null;

            var translated = mesh.DuplicateMesh();
            translated.Translate(dir);
            return translated;
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("A8B4C120-E35F-4D2A-9A10-7B4D581E9F10");
    }
}
