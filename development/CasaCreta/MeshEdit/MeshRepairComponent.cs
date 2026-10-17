using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using CasaCreta.Core;

namespace CasaCreta.MeshEdit
{
    public class MeshRepairComponent : GH_Component
    {
        public MeshRepairComponent()
            : base(
                "Mesh Repair",
                "MeshRepair",
                "Robust volumetric mesh union and repair for clay 3D printing using OpenVDB/Dendro.",
                "CasaCreta",
                "Mesh_Repair")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Geometry",
                "G",
                "Meshes, Breps, or SubDs to repair and union",
                GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Tolerance",
                "T",
                "Voxel size / resolution in model units (smaller = sharper, higher memory)",
                GH_ParamAccess.item,
                RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter(
                "Mesh",
                "M",
                "Repaired and unioned watertight output mesh",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geometries = new List<GeometryBase>();
            double tolerance = 0.001;

            if (!DA.GetDataList(0, geometries) || geometries.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No geometry provided.");
                return;
            }

            if (!DA.GetData(1, ref tolerance) || tolerance <= 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tolerance must be greater than zero.");
                return;
            }

            string errorMsg;
            Mesh result = DendroMeshRepair.RepairAndUnion(geometries, tolerance, out errorMsg);

            if (result == null || !result.IsValid)
            {
                string msg = string.IsNullOrEmpty(errorMsg) 
                    ? "Mesh repair failed or produced an invalid result." 
                    : errorMsg;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                return;
            }

            DA.SetData(0, result);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("8C7A4C6D-4B5B-4E7E-9A8A-3A1E7F9D1F21");
    }
}
