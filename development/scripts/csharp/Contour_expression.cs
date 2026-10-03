// Grasshopper C# Script
#region Usings
using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        GeometryBase srf_ref,
        double layer_height,
        int bottom_layers,
        double box_h,
        int plain_layers_contours,
        double box_w,
        bool width_as_height,
        ref object offsets,
        ref object XYplane,
        ref object shape,
        ref object gate)
    {
        // Always assign outputs so the component does not retain stale data.
        offsets = new List<double>();
        XYplane = Plane.WorldXY;
        shape = srf_ref;
        gate = width_as_height;

        if (srf_ref == null)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "srf_ref must contain valid Rhino geometry.");
            return;
        }

        if (layer_height <= 0.0)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "layer_height must be greater than zero.");
            return;
        }

        if (bottom_layers < 0 || plain_layers_contours < 0)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "bottom_layers and plain_layers_contours cannot be negative.");
            return;
        }

        // Equivalent to the Python generate_series() function.
        double sr_start = layer_height * bottom_layers;
        double sr_step =
            (layer_height * plain_layers_contours) + box_h;

        if (sr_step == 0.0)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "The calculated series step is zero; all offsets will be equal.");
        }

        BoundingBox bbox = srf_ref.GetBoundingBox(true);
        if (!bbox.IsValid)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                "Could not calculate a valid bounding box for srf_ref.");
            return;
        }

        double srf_z_height = bbox.Max.Z - bbox.Min.Z;
        int sr_count = (int) Math.Ceiling(srf_z_height / layer_height);

        List<double> series = new List<double>(sr_count);
        for (int i = 0; i < sr_count; i++)
        {
            series.Add(sr_start + (i * sr_step));
        }

        // Equivalent to the Python get_box_dimensions() function.
        // These values are retained as local variables because the Python
        // script calculates them but does not expose them as outputs.
        double box_height = box_h;
        double box_width = width_as_height ? box_h : box_w;

        offsets = series;
    }
}
