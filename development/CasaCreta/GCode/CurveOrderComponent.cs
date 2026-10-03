using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Geometry;

namespace CasaCreta.GCode
{
    public class CurveOrderComponent : GH_Component
    {
        public CurveOrderComponent()
          : base(
              "CurveOrder",
              "CurveOrder",
              "Orders closed planar curves by Z-layers and containment for G-code generation.",
              "CasaCreta",
              "G-Code")
        { }

        // ============================================================
        // INPUTS
        // ============================================================
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddCurveParameter(
                "Curves",
                "C",
                "Closed planar curves (multiple Z layers allowed)",
                GH_ParamAccess.list
            );

            p.AddNumberParameter(
                "Z Tolerance",
                "ZTol",
                "Tolerance for grouping curves into Z layers",
                GH_ParamAccess.item,
                0.01
            );
        }

        // ============================================================
        // OUTPUTS
        // ============================================================
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddCurveParameter(
                "Ordered Curves (Tree)",
                "T",
                "Curves ordered by Z layer and containment (tree structure)",
                GH_ParamAccess.tree
            );

            p.AddCurveParameter(
                "Ordered Curves (Flat)",
                "F",
                "Flattened curve order safe for G-code execution",
                GH_ParamAccess.list
            );
        }

        // ============================================================
        // MAIN SOLVER
        // ============================================================
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var inputCurves = new List<Curve>();
            double zTol = 0.01;

            if (!DA.GetDataList(0, inputCurves)) return;
            if (!DA.GetData(1, ref zTol)) return;
            if (inputCurves.Count == 0) return;

            var zGroups = GroupCurvesByZ(inputCurves, zTol);

            var orderedTree = new GH_Structure<GH_Curve>();
            var flatList = new List<Curve>();

            int zIndex = 0;

            foreach (var zGroup in zGroups)
            {
                SolveContainmentForOneLayer(
                    zGroup.Value,
                    orderedTree,
                    flatList,
                    zIndex
                );

                zIndex++;
            }

            DA.SetDataTree(0, orderedTree);
            DA.SetDataList(1, flatList);
        }

        // ============================================================
        // Z GROUPING
        // ============================================================
        private SortedDictionary<int, List<Curve>> GroupCurvesByZ(
            List<Curve> curves,
            double zTol)
        {
            var groups = new SortedDictionary<int, List<Curve>>();

            foreach (var crv in curves)
            {
                BoundingBox bb = crv.GetBoundingBox(true);
                double z = 0.5 * (bb.Min.Z + bb.Max.Z);

                int key = (int)Math.Round(z / zTol);

                if (!groups.ContainsKey(key))
                    groups[key] = new List<Curve>();

                groups[key].Add(crv);
            }

            return groups;
        }

        // ============================================================
        // CONTAINMENT SOLVER
        // ============================================================
        private void SolveContainmentForOneLayer(
            List<Curve> curves,
            GH_Structure<GH_Curve> tree,
            List<Curve> flatList,
            int zIndex)
        {
            curves.Sort((a, b) =>
            {
                double aa = Math.Abs(AreaMassProperties.Compute(a).Area);
                double ab = Math.Abs(AreaMassProperties.Compute(b).Area);
                return ab.CompareTo(aa);
            });

            int n = curves.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = -1;

            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            for (int i = 0; i < n; i++)
            {
                Point3d testPt = curves[i].PointAtNormalizedLength(0.37);

                int bestParent = -1;
                double bestArea = double.MaxValue;

                for (int j = 0; j < i; j++)
                {
                    if (curves[j].Contains(testPt, Plane.WorldXY, tol)
                        == PointContainment.Inside)
                    {
                        double area =
                            Math.Abs(AreaMassProperties.Compute(curves[j]).Area);

                        if (area < bestArea)
                        {
                            bestArea = area;
                            bestParent = j;
                        }
                    }
                }

                parent[i] = bestParent;
            }

            int lobeIndex = 0;

            for (int i = 0; i < n; i++)
            {
                if (parent[i] != -1) continue;

                GH_Path path = new GH_Path(zIndex, lobeIndex);

                AppendWithChildren(
                    i,
                    curves,
                    parent,
                    tree,
                    flatList,
                    path
                );

                lobeIndex++;
            }
        }

        // ============================================================
        // APPEND CHILDREN
        // ============================================================
        private void AppendWithChildren(
            int parentIndex,
            List<Curve> curves,
            int[] parent,
            GH_Structure<GH_Curve> tree,
            List<Curve> flatList,
            GH_Path path)
        {
            tree.Append(new GH_Curve(curves[parentIndex]), path);
            flatList.Add(curves[parentIndex]);

            for (int i = 0; i < parent.Length; i++)
            {
                if (parent[i] == parentIndex)
                {
                    AppendWithChildren(
                        i,
                        curves,
                        parent,
                        tree,
                        flatList,
                        path
                    );
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("A6F4C1E9-8B2D-4E73-9F12-7C3A9D5E0B41");
    }
}
