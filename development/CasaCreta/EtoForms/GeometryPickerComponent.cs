using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Rhino;

namespace CasaCreta.EtoForms
{
    public class GeometryPickerComponent : GH_Component
    {
        public GeometryPickerComponent()
            : base(
                "Geometry Picker Form",
                "EtoPicker",
                "Opens an Eto.Forms UI dialog to pick and clear geometry selections in Rhino.",
                "CasaCreta",
                "EtoForms")
        {
        }

        public override Guid ComponentGuid => new Guid("4b12c8a1-5d93-4a18-912b-7c89f50e32a1");

        protected override Bitmap Icon => null;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Open", "O", "Set to true to launch the Eto.Forms Geometry Picker window.", GH_ParamAccess.item, false);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Status of the Eto Geometry Picker Form", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool open = false;
            DA.GetData(0, ref open);

            if (open)
            {
                GeometryPickerForm.ShowForm();
                DA.SetData(0, "Eto Form Open");
            }
            else
            {
                DA.SetData(0, "Idle (Double-click or toggle Open to show form)");
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            Menu_AppendItem(menu, "Open Geometry Picker Form (Eto)...", (s, e) =>
            {
                GeometryPickerForm.ShowForm();
            });
        }
    }
}
