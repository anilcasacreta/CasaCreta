using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace CasaCreta
{
    public sealed class CreateUVCrv : GH_Component
    {
        private static readonly Bitmap _cachedIcon = LoadIcon();

        public CreateUVCrv()
            : base(
                "Create UV Curves",
                "CreateUVCrv",
                "Creates UV curves of a surface by measuring true max iso lengths "
                + "and mapping trim boundaries onto a flat rectangle.",
                "CasaCreta",
                "Surface")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter(
                "Surface", "S",
                "A surface or single-face Brep (trimmed, untrimmed, open, or closed).",
                GH_ParamAccess.item);

            pManager.AddPlaneParameter(
                "Plane", "P",
                "Base plane for the output rectangle. The corner starts at the plane origin and extends along its X+ and Y+ axes. Default is WorldXY at origin.",
                GH_ParamAccess.item,
                Plane.WorldXY);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter(
                "UV Curves", "C",
                "Flattened trim boundary curves of the surface mapped onto the target rectangle.",
                GH_ParamAccess.list);

            pManager.AddRectangleParameter(
                "Rectangle", "R",
                "Boundary rectangle (maxU × maxV) starting at the Plane origin, extending X+ and Y+.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep source = null;
            if (!DA.GetData(0, ref source) || source == null || source.Faces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid surface.");
                return;
            }

            if (source.Faces.Count > 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Input Brep has multiple faces; only the first face is processed.");
            }

            Plane basePlane = Plane.WorldXY;
            DA.GetData(1, ref basePlane);
            if (!basePlane.IsValid)
                basePlane = Plane.WorldXY;

            BrepFace bf = source.Faces[0];

            // --- Measure true max U and V lengths via isocurve sampling ---
            BoundingBox bbox = bf.GetBoundingBox(true);
            double bboxX = bbox.Max.X - bbox.Min.X;
            double bboxY = bbox.Max.Y - bbox.Min.Y;
            double bboxZ = bbox.Max.Z - bbox.Min.Z;
            double maxDim = Math.Max(bboxX, Math.Max(bboxY, bboxZ));

            // Dense isocurve sampling (~1mm spacing, clamped to reasonable range)
            int isoCount = Math.Max(50, Math.Min((int)Math.Ceiling(maxDim), 2000));

            Interval uDom = bf.Domain(0);
            Interval vDom = bf.Domain(1);

            // Measure U-direction iso curves (each runs along U at a fixed V)
            double maxU = 0.0;
            for (int i = 0; i <= isoCount; i++)
            {
                double v = vDom.Min + (vDom.Max - vDom.Min) * (i / (double)isoCount);
                Curve iso = bf.IsoCurve(0, v);
                if (iso != null)
                {
                    double len = iso.GetLength();
                    if (len > maxU) maxU = len;
                }
            }

            // Measure V-direction iso curves (each runs along V at a fixed U)
            double maxV = 0.0;
            for (int i = 0; i <= isoCount; i++)
            {
                double u = uDom.Min + (uDom.Max - uDom.Min) * (i / (double)isoCount);
                Curve iso = bf.IsoCurve(1, u);
                if (iso != null)
                {
                    double len = iso.GetLength();
                    if (len > maxV) maxV = len;
                }
            }

            if (maxU <= 0.0 || maxV <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not measure surface.");
                return;
            }

            // --- Build boundary rectangle at basePlane origin, extending X+ and Y+ ---
            Rectangle3d rect = new Rectangle3d(
                basePlane,
                new Interval(0, maxU),
                new Interval(0, maxV));

            double tol = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
            if (tol <= 0.0) tol = 0.001;

            // --- Build target planar surface matching rectangle and source UV domains ---
            Point3d p0 = basePlane.PointAt(0, 0);
            Point3d p1 = basePlane.PointAt(maxU, 0);
            Point3d p2 = basePlane.PointAt(maxU, maxV);
            Point3d p3 = basePlane.PointAt(0, maxV);

            NurbsSurface targetSrf = NurbsSurface.CreateFromCorners(p0, p1, p2, p3);
            if (targetSrf == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to create target surface.");
                return;
            }

            // Align target surface domains to match source face domains exactly
            targetSrf.SetDomain(0, uDom);
            targetSrf.SetDomain(1, vDom);

            // --- CopyTrimCurves onto target surface ---
            Brep uvBrep = Brep.CopyTrimCurves(bf, targetSrf, tol);
            if (uvBrep == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "CopyTrimCurves failed.");
                DA.SetData(1, rect);
                return;
            }

            // Join trim curves if possible, fallback to unjoined segments
            if (uvBrep.Curves3D != null && uvBrep.Curves3D.Count > 0)
            {
                Curve[] joined = Curve.JoinCurves(uvBrep.Curves3D, tol);
                if (joined != null && joined.Length > 0)
                    DA.SetDataList(0, joined);
                else
                    DA.SetDataList(0, uvBrep.Curves3D);
            }

            DA.SetData(1, rect);
        }

        private static Bitmap LoadIcon()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAACKElEQVR4nLWVO2gUURSGvxtnFhmNs4kEZCxEC0VQkBQ2giQWNlvEWnEh+CiMoKKFFjZiEcFKCIIpAinSGFwstlFI1ELBEIgQa0FJMN2u6KCZZa7ceezezL5md5MDw1zu4//v+c+55wgpJTtpfTuKDhjJiUqlQq9eCSEwDKOeYP5oVm6Wyj2BZ+x9nF/+gWVZQpEY+s0V+MV5QDnga3+/ybjB3NzNX5TLwSWlIqnGoCpLSvDiEjjj4FyB4rK2Briui+d5AWZdDJoBLq7AzFv48BU2SuD78M8LEQ8OCtaeR/shAI+tniAB/vsP5J9C4VNC6+TJ+IyuRkMCDbziwdgjWPgC2T1wdwwunIYjQ7C4Cs6gCPZNX9sqkW4tJZp5E4IfGICPk3B4qLaeOwXrsSy6rG0fWhzEzzChAID8SAQu2ySBTEMQbbz+DDY9Geg5+64FoOzSg4bE/jZ68OIGZEwRPPv82ZTgnXiQG4apq+HU7Hv4tpHyRafyIDo0PgLnTsDPEgzfh8cFWP0O7l8oroBzC5w74birNDX64PU9yE9BYQkevgy/+KFVX/KAYG2y0yBH/70ZeHUbFh7A5TNwaD/sNhO3bRHk1rVI1sajx2H0WG0+kCgrgiPTl5oHuXktapP3uZOw/qRxmqrMa0hg2jZzE701nF39/Zim0jAiiyufajiu60rVLPR6Tgembq7ALcvCtu2gq1UJdBK9nndjEUnQMrcQxCTb2fT/A9uNTDFxjqRiAAAAAElFTkSuQmCC");
                using (var ms = new MemoryStream(bytes))
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
            new Guid("D5A2E1F7-04BD-4AB9-8D54-6CE5FA9110C2");
    }
}
