using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;

using Rhino;
using Rhino.Geometry;
using Surface = Rhino.Geometry.Surface;

namespace CasaCreta
{
    /// <summary>
    /// Exposes the RhinoCommon parameter-space operations behind Rhino's
    /// CreateUVCrv workflow without running an interactive Rhino command.
    /// </summary>
    public sealed class CreateUVCrv : GH_Component
    {
        public CreateUVCrv()
            : base(
                "Create UV Curves",
                "CreateUVCrv",
                "Creates the true UV-domain boundary, trim loops, curve "
                + "pullbacks, and point parameters for one surface face "
                + "using RhinoCommon.",
                "CasaCreta",
                "Surface")
        {
        }

        protected override void RegisterInputParams(
            GH_InputParamManager parameters)
        {
            parameters.AddSurfaceParameter(
                "Surface",
                "S",
                "A single trimmed or untrimmed surface face.",
                GH_ParamAccess.item);

            parameters.AddCurveParameter(
                "Curves",
                "C",
                "Optional 3D curves lying on the surface to pull back "
                + "into its UV parameter space.",
                GH_ParamAccess.list);

            parameters.AddPointParameter(
                "Points",
                "P",
                "Optional 3D points on the surface to convert to UV points.",
                GH_ParamAccess.list);

            parameters.AddNumberParameter(
                "Tolerance",
                "T",
                "Pullback and point-on-surface tolerance. Values less than "
                + "or equal to zero use the active Rhino document tolerance.",
                GH_ParamAccess.item,
                -1.0);

            parameters[1].Optional = true;
            parameters[2].Optional = true;
            parameters[3].Optional = true;
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager parameters)
        {
            parameters.AddCurveParameter(
                "UV Domain",
                "D",
                "Untrimmed surface-domain rectangle on World XY.",
                GH_ParamAccess.item);

            parameters.AddCurveParameter(
                "Trim Loops",
                "T",
                "All Brep trim loops in UV parameter space. On an "
                + "untrimmed surface, the outer loop may coincide with "
                + "the UV domain boundary.",
                GH_ParamAccess.list);

            parameters.AddCurveParameter(
                "UV Curves",
                "C",
                "Input 3D curves pulled back to UV parameter space.",
                GH_ParamAccess.list);

            parameters.AddPointParameter(
                "UV Points",
                "P",
                "Input 3D points represented as (u, v, 0).",
                GH_ParamAccess.list);

            parameters.AddIntervalParameter(
                "U Domain",
                "U",
                "Original U parameter domain of the underlying surface.",
                GH_ParamAccess.item);

            parameters.AddIntervalParameter(
                "V Domain",
                "V",
                "Original V parameter domain of the underlying surface.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            Brep brep = null;
            var curves = new List<Curve>();
            var points = new List<Point3d>();
            double tolerance = -1.0;

            if (!access.GetData(0, ref brep) || brep == null)
                return;

            access.GetDataList(1, curves);
            access.GetDataList(2, points);
            access.GetData(3, ref tolerance);

            if (brep.Faces.Count != 1)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "CreateUVCrv requires one surface face. Explode or "
                    + "deconstruct polysurfaces and process each face "
                    + "separately because every face has its own UV space.");
                return;
            }

            tolerance = ResolveTolerance(tolerance);

            BrepFace face = brep.Faces[0];
            Surface surface = face.UnderlyingSurface();

            if (surface == null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "The underlying Rhino surface is unavailable.");
                return;
            }

            Interval uDomain = surface.Domain(0);
            Interval vDomain = surface.Domain(1);
            Curve domainBoundary = CreateDomainBoundary(
                uDomain,
                vDomain);

            List<Curve> trimLoops = GetTrimLoops(face);
            List<Curve> uvCurves = PullBackCurves(
                surface,
                curves,
                tolerance);
            List<Point3d> uvPoints = PullBackPoints(
                surface,
                points,
                tolerance);

            access.SetData(0, domainBoundary);
            access.SetDataList(1, trimLoops);
            access.SetDataList(2, uvCurves);
            access.SetDataList(3, uvPoints);
            access.SetData(4, uDomain);
            access.SetData(5, vDomain);
        }

        private static Curve CreateDomainBoundary(
            Interval uDomain,
            Interval vDomain)
        {
            var boundary = new Polyline(5)
            {
                new Point3d(uDomain.Min, vDomain.Min, 0.0),
                new Point3d(uDomain.Max, vDomain.Min, 0.0),
                new Point3d(uDomain.Max, vDomain.Max, 0.0),
                new Point3d(uDomain.Min, vDomain.Max, 0.0),
                new Point3d(uDomain.Min, vDomain.Min, 0.0)
            };

            return new PolylineCurve(boundary);
        }

        private static List<Curve> GetTrimLoops(BrepFace face)
        {
            var result = new List<Curve>();

            foreach (BrepLoop loop in face.Loops)
            {
                Curve curve = loop.To2dCurve();
                if (curve != null && curve.IsValid)
                    result.Add(curve);
            }

            return result;
        }

        private List<Curve> PullBackCurves(
            Rhino.Geometry.Surface surface,
            IEnumerable<Curve> curves,
            double tolerance)
        {
            var result = new List<Curve>();
            int failed = 0;

            foreach (Curve curve in curves)
            {
                if (curve == null || !curve.IsValid)
                {
                    failed++;
                    continue;
                }

                Curve uvCurve = surface.Pullback(curve, tolerance);
                if (uvCurve == null || !uvCurve.IsValid)
                {
                    failed++;
                    continue;
                }

                result.Add(uvCurve);
            }

            if (failed > 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    failed + " curve(s) could not be pulled back. Curves "
                    + "must lie on the underlying surface within tolerance.");
            }

            return result;
        }

        private List<Point3d> PullBackPoints(
            Rhino.Geometry.Surface surface,
            IEnumerable<Point3d> points,
            double tolerance)
        {
            var result = new List<Point3d>();
            int failed = 0;

            foreach (Point3d point in points)
            {
                if (!surface.ClosestPoint(
                    point,
                    out double u,
                    out double v))
                {
                    failed++;
                    continue;
                }

                Point3d surfacePoint = surface.PointAt(u, v);
                if (!surfacePoint.IsValid
                    || surfacePoint.DistanceTo(point) > tolerance)
                {
                    failed++;
                    continue;
                }

                result.Add(new Point3d(u, v, 0.0));
            }

            if (failed > 0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    failed + " point(s) were farther from the surface "
                    + "than the specified tolerance.");
            }

            return result;
        }

        private static double ResolveTolerance(double tolerance)
        {
            if (tolerance > 0.0)
                return tolerance;

            RhinoDoc document = RhinoDoc.ActiveDoc;
            return document?.ModelAbsoluteTolerance > 0.0
                ? document.ModelAbsoluteTolerance
                : 0.001;
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("D5A2E1F7-04BD-4AB9-8D54-6CE5FA9110C2");
    }
}
