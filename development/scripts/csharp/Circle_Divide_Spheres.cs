// Grasshopper C# Script
#region Usings
using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(object x, object y, ref object a)
    {
        const int divisionCount = 5;
        const double sphereDiameter = 7.5;
        const double tolerance = 1e-9;

        if (!TryGetCurve(x, out Curve crv))
        {
            Print("Connect a Curve parameter to the C# component's x input.");
            a = null;
            return;
        }

        double[] parameters = crv.DivideByCount(
            divisionCount,
            true);

        if (parameters == null || parameters.Length == 0)
        {
            Print("The input curve could not be divided.");
            a = null;
            return;
        }

        int pointCount = parameters.Length;

        // Closed curves return the seam at both ends. Remove the duplicate
        // endpoint so five divisions produce five unique sphere locations.
        if (crv.IsClosed
            && pointCount > 1
            && crv.PointAt(parameters[0]).DistanceTo(
                crv.PointAt(parameters[pointCount - 1])) <= tolerance)
        {
            pointCount--;
        }

        double sphereRadius = sphereDiameter * 0.5;
        var spheres = new List<Brep>(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            Point3d point = crv.PointAt(parameters[i]);
            Brep sphere = new Sphere(point, sphereRadius).ToBrep();
            if (sphere != null)
                spheres.Add(sphere);
        }

        a = spheres;
    }

    private static bool TryGetCurve(object input, out Curve curve)
    {
        curve = input as Curve;
        if (curve != null)
            return true;

        if (input is GH_Curve grasshopperCurve)
        {
            curve = grasshopperCurve.Value;
            return curve != null;
        }

        if (input is GH_ObjectWrapper wrapper)
        {
            curve = wrapper.Value as Curve;
            return curve != null;
        }

        return false;
    }
}
