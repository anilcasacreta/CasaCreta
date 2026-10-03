using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace CasaCreta.MeshEdit
{
    /// <summary>
    /// Grasshopper wrapper for the public mesh-repair API installed with
    /// Grasshopper Gold. No Grasshopper Gold binaries are redistributed.
    /// </summary>
    public sealed class GGRepairMesh : GH_Component
    {
        private const string RepairAssemblyName = "GG_Repair_Mesh";
        private const string RepairTypeName = "Repair_Mesh.Neper";
        private const string RepairMethodName = "BuildMesh";
        private const string DendroSettingsTypeName = "DendroSettings";
        private const string DendroVolumeTypeName = "Repair_Mesh.DendroVolume";
        private const string DefaultInstallDirectory =
            @"C:\Grasshopper Gold\food4rhino\PLUGINS\GG_Repair_Mesh\1.0";

        private static readonly object RepairLock = new object();

        public GGRepairMesh()
            : base(
                "GG Repair Mesh",
                "GGRepair",
                "Repairs meshes or creates a watertight voxel union using the installed Grasshopper Gold engine.",
                "CasaCreta",
                "Mesh Edit")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter(
                "Meshes",
                "M",
                "Meshes, closed surfaces, or closed polysurfaces to combine and repair.",
                GH_ParamAccess.list);
            pManager.AddBooleanParameter(
                "Use Dendro",
                "D",
                "False performs an exact Rhino mesh Boolean followed by FixMesh repair. True uses a smooth Dendro voxel union.",
                GH_ParamAccess.item,
                false);
            pManager.AddNumberParameter(
                "Voxel Size",
                "V",
                "Dendro voxel size in model units. Smaller values preserve more detail but use more memory.",
                GH_ParamAccess.item,
                0.5);
            pManager.AddBooleanParameter(
                "Run",
                "Go",
                "Run the repair operation.",
                GH_ParamAccess.item,
                false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Repaired mesh.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True when repair succeeds.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "Msg", "Repair status or error details.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var inputs = new List<Mesh>();
            bool voxelUnion = false;
            double voxelSize = 0.5;
            bool run = false;

            if (!DA.GetDataList(0, inputs) || inputs.Count == 0)
                return;

            DA.GetData(1, ref voxelUnion);
            DA.GetData(2, ref voxelSize);
            DA.GetData(3, ref run);

            Mesh combined = CombineMeshes(inputs);

            if (!run)
            {
                DA.SetData(0, combined);
                DA.SetData(1, false);
                DA.SetData(2, "Set Run to true to process the meshes.");
                return;
            }

            try
            {
                if (voxelUnion && (!Rhino.RhinoMath.IsValidDouble(voxelSize) || voxelSize <= 0.0))
                    throw new ArgumentOutOfRangeException(
                        nameof(voxelSize),
                        "Voxel Size must be greater than zero.");

                Mesh repaired;

                // The native engine and its progress callback use shared state.
                lock (RepairLock)
                {
                    if (voxelUnion)
                    {
                        repaired = InvokeVoxelUnion(combined, voxelSize);
                    }
                    else
                    {
                        Mesh booleanUnion = InvokeBooleanUnion(inputs);
                        if (!InvokeRepair(booleanUnion, true, out repaired))
                            repaired = null;
                    }
                }

                if (repaired == null)
                    throw new InvalidOperationException("Grasshopper Gold returned no repaired mesh.");

                repaired.Normals.ComputeNormals();
                repaired.Compact();

                DA.SetData(0, repaired);
                DA.SetData(1, true);
                DA.SetData(
                    2,
                    $"{(voxelUnion ? "Dendro voxel union" : "Exact Boolean + FixMesh repair")} complete: " +
                    $"{repaired.Vertices.Count:N0} vertices, " +
                    $"{repaired.Faces.Count:N0} faces.");
            }
            catch (Exception ex)
            {
                Exception root = Unwrap(ex);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, root.Message);
                DA.SetData(0, combined);
                DA.SetData(1, false);
                DA.SetData(2, root.Message);
            }
        }

        private static Mesh CombineMeshes(IEnumerable<Mesh> meshes)
        {
            var combined = new Mesh();

            foreach (Mesh mesh in meshes)
            {
                if (mesh != null && mesh.IsValid)
                    combined.Append(mesh);
            }

            if (combined.Faces.Count == 0)
                throw new ArgumentException("No valid mesh faces were supplied.");

            combined.Normals.ComputeNormals();
            combined.Compact();
            return combined;
        }

        private static Mesh InvokeBooleanUnion(IEnumerable<Mesh> meshes)
        {
            var validMeshes = meshes
                .Where(mesh => mesh != null && mesh.IsValid && mesh.Faces.Count > 0)
                .Select(mesh => mesh.DuplicateMesh())
                .ToList();

            if (validMeshes.Count == 0)
                throw new ArgumentException("No valid meshes were supplied.");

            if (validMeshes.Count == 1)
                return validMeshes[0];

            double tolerance = Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
            Mesh[] unionParts = Mesh.CreateBooleanUnion(validMeshes, tolerance);

            if (unionParts == null || unionParts.Length == 0)
                throw new InvalidOperationException(
                    "The exact mesh Boolean union failed. Check that the input meshes are closed, " +
                    "valid, and overlap by more than the document tolerance.");

            var union = new Mesh();
            foreach (Mesh part in unionParts)
                union.Append(part);

            union.Normals.ComputeNormals();
            union.Compact();
            return union;
        }

        private static bool InvokeRepair(Mesh mesh, bool repair, out Mesh repaired)
        {
            LoadNativeLibrary(Path.Combine(DefaultInstallDirectory, "FixMesh.dll"));
            Assembly assembly = LoadRepairAssembly();

            Type repairType = assembly.GetType(RepairTypeName, true);
            MethodInfo method = repairType.GetMethod(
                RepairMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Mesh), typeof(bool), typeof(Mesh).MakeByRefType() },
                null);

            if (method == null)
                throw new MissingMethodException(
                    RepairTypeName,
                    "BuildMesh(Mesh, Boolean, out Mesh)");

            object[] arguments = { mesh, repair, null };
            bool success = (bool)method.Invoke(null, arguments);
            repaired = arguments[2] as Mesh;
            return success;
        }

        private static Mesh InvokeVoxelUnion(Mesh mesh, double voxelSize)
        {
            LoadNativeLibrary(Path.Combine(DefaultInstallDirectory, "DendroAPI.dll"));
            Assembly assembly = LoadRepairAssembly();

            Type settingsType = assembly.GetType(DendroSettingsTypeName, true);
            Type volumeType = assembly.GetType(DendroVolumeTypeName, true);
            object settings = Activator.CreateInstance(settingsType);

            settingsType.GetProperty("VoxelSize").SetValue(settings, voxelSize);
            settingsType.GetProperty("Bandwidth").SetValue(settings, 1.0);
            settingsType.GetProperty("IsoValue").SetValue(settings, 0.01);
            settingsType.GetProperty("Adaptivity").SetValue(settings, 0.1);

            object volume = null;
            try
            {
                volume = Activator.CreateInstance(volumeType, new[] { mesh, settings });
                bool isValid = (bool)volumeType.GetProperty("IsValid").GetValue(volume);
                if (!isValid)
                    throw new InvalidOperationException("Dendro could not create a valid volume.");

                Mesh display = volumeType.GetProperty("Display").GetValue(volume) as Mesh;
                return display?.DuplicateMesh();
            }
            finally
            {
                (volume as IDisposable)?.Dispose();
            }
        }

        private static Assembly LoadRepairAssembly()
        {
            string pluginPath = Path.Combine(DefaultInstallDirectory, "GG_Repair_Mesh.rhp");
            if (!File.Exists(pluginPath))
                throw new FileNotFoundException(
                    "GG_Repair_Mesh.rhp was not found. Install Grasshopper Gold in C:\\Grasshopper Gold.",
                    pluginPath);

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                    string.Equals(
                        a.GetName().Name,
                        RepairAssemblyName,
                        StringComparison.OrdinalIgnoreCase))
                ?? Assembly.LoadFrom(pluginPath);
        }

        private static void LoadNativeLibrary(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("A Grasshopper Gold repair dependency is missing.", path);

            if (LoadLibrary(path) == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Could not load {Path.GetFileName(path)} (Windows error {Marshal.GetLastWin32Error()}).");
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;

            return exception;
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("7DDFA092-AB08-4EF6-96B8-57C9CB677872");
    }
}
