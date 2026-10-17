using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rhino.Geometry;

namespace CasaCreta.Core
{
    /// <summary>
    /// Core engine that converts mixed Rhino geometry (Mesh, Brep, SubD, Extrusion) 
    /// and performs a robust volumetric union via Dendro (OpenVDB).
    /// </summary>
    public static class DendroMeshRepair
    {
        private static Type _dendroVolumeType;
        private static Type _dendroSettingsType;
        private static bool _dendroResolved;

        /// <summary>
        /// Main entry point: converts mixed geometry into one clean, watertight union mesh.
        /// </summary>
        /// <param name="geometries">List of Mesh, Brep, SubD, or Extrusion objects.</param>
        /// <param name="tolerance">Voxel size / resolution in model units (smaller = sharper, higher memory).</param>
        /// <param name="errorMessage">Detailed error message if repair fails.</param>
        /// <returns>One clean watertight union mesh, or null on failure.</returns>
        public static Mesh RepairAndUnion(IEnumerable<GeometryBase> geometries, double tolerance, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (geometries == null)
            {
                errorMessage = "Geometry list is null.";
                return null;
            }

            // 1. Convert all mixed geometries into individual valid meshes
            List<Mesh> inputMeshes = NormalizeToMeshes(geometries);
            if (inputMeshes.Count == 0)
            {
                errorMessage = "No valid convertible geometry (Mesh, Brep, SubD) found in input.";
                return null;
            }

            // 2. Ensure Dendro types are resolved from the active Grasshopper environment
            if (!EnsureDendroTypes(out errorMessage))
            {
                return null;
            }

            // 3. Configure Dendro Settings
            // Adaptivity = 0.0 ensures sharp model edges are preserved rather than smoothed/rounded.
            object settings = Activator.CreateInstance(_dendroSettingsType);
            _dendroSettingsType.GetProperty("VoxelSize")?.SetValue(settings, Math.Max(0.001, tolerance));
            _dendroSettingsType.GetProperty("Bandwidth")?.SetValue(settings, 3.0);
            _dendroSettingsType.GetProperty("IsoValue")?.SetValue(settings, 0.0);
            _dendroSettingsType.GetProperty("Adaptivity")?.SetValue(settings, 0.0);

            // 4. Convert meshes to DendroVolumes and perform Volumetric Union
            List<IDisposable> volumesToDispose = new List<IDisposable>();

            try
            {
                ConstructorInfo volumeCtor = _dendroVolumeType.GetConstructor(new[] { typeof(Mesh), _dendroSettingsType });
                if (volumeCtor == null)
                {
                    errorMessage = "Could not locate DendroVolume(Mesh, DendroSettings) constructor.";
                    return null;
                }

                List<object> volumes = new List<object>();

                foreach (Mesh m in inputMeshes)
                {
                    if (!m.IsValid || m.Faces.Count == 0)
                        continue;

                    // Ensure closed and normals computed
                    Mesh cleanCopy = m.DuplicateMesh();
                    cleanCopy.Normals.ComputeNormals();
                    cleanCopy.Compact();

                    object vol = volumeCtor.Invoke(new[] { cleanCopy, settings });
                    if (vol != null)
                    {
                        bool isValid = (bool)(_dendroVolumeType.GetProperty("IsValid")?.GetValue(vol) ?? false);
                        if (isValid)
                        {
                            volumes.Add(vol);
                            if (vol is IDisposable disp)
                                volumesToDispose.Add(disp);
                        }
                    }
                }

                if (volumes.Count == 0)
                {
                    errorMessage = "Dendro could not voxelize the input meshes. Check that meshes are valid solids.";
                    return null;
                }

                object resultVolume;

                if (volumes.Count == 1)
                {
                    resultVolume = volumes[0];
                }
                else
                {
                    // Call: DendroVolume.BooleanUnion(List<DendroVolume>)
                    MethodInfo unionListMethod = _dendroVolumeType.GetMethod("BooleanUnion", new[] { typeof(List<>).MakeGenericType(_dendroVolumeType) });

                    if (unionListMethod != null)
                    {
                        // Create strongly typed List<DendroVolume>
                        Type genericListType = typeof(List<>).MakeGenericType(_dendroVolumeType);
                        IList typedList = (IList)Activator.CreateInstance(genericListType);
                        foreach (var v in volumes)
                            typedList.Add(v);

                        resultVolume = unionListMethod.Invoke(volumes[0], new object[] { typedList });
                    }
                    else
                    {
                        // Fallback: Pairwise union
                        MethodInfo unionSingleMethod = _dendroVolumeType.GetMethod("BooleanUnion", new[] { _dendroVolumeType });
                        resultVolume = volumes[0];
                        for (int i = 1; i < volumes.Count; i++)
                        {
                            resultVolume = unionSingleMethod.Invoke(resultVolume, new[] { volumes[i] });
                            if (resultVolume is IDisposable disp)
                                volumesToDispose.Add(disp);
                        }
                    }

                    if (resultVolume is IDisposable finalDisp && !volumesToDispose.Contains(finalDisp))
                        volumesToDispose.Add(finalDisp);
                }

                // 5. Extract the resulting mesh from the unioned volume
                MethodInfo updateDisplay = _dendroVolumeType.GetMethod("UpdateDisplay", new[] { _dendroSettingsType });
                if (updateDisplay != null)
                {
                    updateDisplay.Invoke(resultVolume, new[] { settings });
                }

                PropertyInfo displayProp = _dendroVolumeType.GetProperty("Display");
                Mesh outputMesh = displayProp?.GetValue(resultVolume) as Mesh;

                if (outputMesh == null || !outputMesh.IsValid || outputMesh.Faces.Count == 0)
                {
                    errorMessage = "Volumetric union produced an empty or invalid mesh.";
                    return null;
                }

                Mesh finalResult = outputMesh.DuplicateMesh();
                finalResult.Normals.ComputeNormals();
                finalResult.Compact();
                return finalResult;
            }
            catch (Exception ex)
            {
                errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return null;
            }
            finally
            {
                // Unmanaged memory cleanup: free OpenVDB grids
                foreach (var d in volumesToDispose)
                {
                    try { d.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// Converts mixed inputs (Brep, SubD, Extrusion, Mesh) into solid meshes.
        /// </summary>
        private static List<Mesh> NormalizeToMeshes(IEnumerable<GeometryBase> geometries)
        {
            var meshes = new List<Mesh>();

            // Fast, high-accuracy meshing parameters suitable for preserving edges before voxelization
            var mp = MeshingParameters.QualityRenderMesh;

            foreach (var geom in geometries)
            {
                if (geom == null) continue;

                if (geom is Mesh m)
                {
                    if (m.IsValid && m.Faces.Count > 0)
                        meshes.Add(m.DuplicateMesh());
                }
                else if (geom is Brep brep)
                {
                    Mesh[] bMeshes = Mesh.CreateFromBrep(brep, mp);
                    if (bMeshes != null && bMeshes.Length > 0)
                    {
                        Mesh combined = new Mesh();
                        foreach (var bm in bMeshes)
                            if (bm != null && bm.IsValid) combined.Append(bm);

                        if (combined.Faces.Count > 0)
                            meshes.Add(combined);
                    }
                }
                else if (geom is Extrusion ext)
                {
                    Brep extBrep = ext.ToBrep();
                    if (extBrep != null)
                    {
                        Mesh[] bMeshes = Mesh.CreateFromBrep(extBrep, mp);
                        if (bMeshes != null && bMeshes.Length > 0)
                        {
                            Mesh combined = new Mesh();
                            foreach (var bm in bMeshes)
                                if (bm != null && bm.IsValid) combined.Append(bm);

                            if (combined.Faces.Count > 0)
                                meshes.Add(combined);
                        }
                    }
                }
                else if (geom is SubD subd)
                {
                    // Rhino 7/8 SubD conversion with high density (3) to preserve curvature & creases
                    Mesh subdMesh = Mesh.CreateFromSubD(subd, 3);
                    if (subdMesh != null && subdMesh.IsValid && subdMesh.Faces.Count > 0)
                        meshes.Add(subdMesh);
                }
            }

            return meshes;
        }

        /// <summary>
        /// Resolves Dendro classes from the active Grasshopper AppDomain.
        /// </summary>
        private static bool EnsureDendroTypes(out string error)
        {
            error = string.Empty;
            if (_dendroResolved && _dendroVolumeType != null && _dendroSettingsType != null)
                return true;

            // Search currently loaded assemblies for DendroGH
            Assembly dendroAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "DendroGH", StringComparison.OrdinalIgnoreCase));

            // If not found in current domain, attempt to load from standard Grasshopper Libraries path
            if (dendroAsm == null)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string ghLibDir = Path.Combine(appData, "Grasshopper", "Libraries");

                if (Directory.Exists(ghLibDir))
                {
                    // Look for DendroGH.gha or DendroGH.dll recursively
                    var files = Directory.GetFiles(ghLibDir, "DendroGH.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        if (f.EndsWith(".gha", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                dendroAsm = Assembly.LoadFrom(f);
                                if (dendroAsm != null) break;
                            }
                            catch { }
                        }
                    }
                }
            }

            if (dendroAsm == null)
            {
                error = "DendroGH assembly was not found. Please ensure Dendro is installed in Grasshopper.";
                return false;
            }

            _dendroVolumeType = dendroAsm.GetType("DendroGH.DendroVolume");
            _dendroSettingsType = dendroAsm.GetType("DendroGH.DendroSettings");

            if (_dendroVolumeType == null || _dendroSettingsType == null)
            {
                error = "Found DendroGH assembly, but DendroVolume or DendroSettings types are missing.";
                return false;
            }

            _dendroResolved = true;
            return true;
        }
    }
}
