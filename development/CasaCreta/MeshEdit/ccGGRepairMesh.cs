using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Runtime;

namespace CasaCreta.MeshEdit
{
    /// <summary>
    /// Robust, crash-proof Grasshopper component that collects any mixed geometry
    /// (closed/open surfaces, spheres, polysurfaces, SubD, extrusions, meshes),
    /// converts them into high-precision polygon meshes (0.05 mm precision),
    /// bakes them to a temporary Rhino layer, executes the GG_Repair_Mesh command
    /// via RhinoApp.Idle, captures the repaired unioned mesh, cleans up the document,
    /// internalizes the resulting mesh into the GH component, and safely returns
    /// the output to Grasshopper wires.
    /// </summary>
    public sealed class ccGGRepairMesh : GH_Component
    {
        private enum RepairState
        {
            Idle,
            Executing,
            ReadyToOutput,
            Failed
        }

        private const string TempLayerName = "__CC_GG_REPAIR_TEMP__";

        // State machine fields
        private RepairState _state = RepairState.Idle;
        private bool _lastRunInput = false;
        private bool _lastResetInput = false;

        // Session execution tracking
        private uint _startRuntimeSerial = 0;
        private int _startHistLength = 0;
        private List<Guid> _bakedGuids = new List<Guid>();
        private string _activeCommandMacro = "! _GG_Repair_Mesh _Enter";

        // Internalized / Cached output results
        private Mesh _cachedCombinedMesh = null;
        private List<Mesh> _cachedMeshes = new List<Mesh>();
        private bool _cachedSuccess = false;
        private string _cachedLog = string.Empty;

        public ccGGRepairMesh()
            : base(
                "GG Repair Mesh cc",
                "GGRepair cc",
                "Converts mixed geometry (surfaces, spheres, polysurfaces, SubD, meshes) to high-precision mesh (0.05mm) "
                + "and safely executes Grasshopper Gold's GG_Repair_Mesh command via asynchronous document idle execution. "
                + "Results are automatically internalized inside the component.",
                "CasaCreta",
                "Mesh Edit")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter(
                "Geometry",
                "G",
                "All geometry types to combine and repair: open/closed surfaces, spheres, polysurfaces (Breps), SubDs, extrusions, or meshes.",
                GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Precision",
                "P",
                "Geometric distance tolerance in mm (max allowed distance between polygon edge/facet and true surface). Default is 0.05 mm.",
                GH_ParamAccess.item,
                0.05);

            pManager.AddBooleanParameter(
                "Run",
                "Go",
                "Trigger the repair process (Button or Toggle). Operates via safe asynchronous Idle execution to prevent Rhino freezing or crashing.",
                GH_ParamAccess.item,
                false);

            pManager.AddBooleanParameter(
                "Reset",
                "Rst",
                "Clear internalized mesh cache memory and reset the computed state of the component (Button or Toggle).",
                GH_ParamAccess.item,
                false);

            pManager.AddTextParameter(
                "Command",
                "C",
                "Rhino command macro to execute (default is '! _GG_Repair_Mesh _Enter'). Automatically sends Enter to complete without prompt stalls.",
                GH_ParamAccess.item,
                "! _GG_Repair_Mesh _Enter");

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter(
                "Mesh",
                "M",
                "Watertight, repaired and unioned solid mesh (internalized).",
                GH_ParamAccess.item);

            pManager.AddMeshParameter(
                "Meshes",
                "Ms",
                "List of individual output meshes (if the repair output contains multiple disjoint bodies).",
                GH_ParamAccess.list);

            pManager.AddBooleanParameter(
                "Success",
                "S",
                "True if the repair operation completed successfully and produced valid geometry.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Log",
                "L",
                "Rhino command-line console output and diagnostic messages.",
                GH_ParamAccess.item);
        }

        // ─── Solve Instance ──────────────────────────────────────────────

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var rawGoos = new List<IGH_GeometricGoo>();
            double precision = 0.05;
            bool runInput = false;
            bool resetInput = false;
            string commandMacro = "! _GG_Repair_Mesh _Enter";

            DA.GetDataList(0, rawGoos);
            DA.GetData(1, ref precision);
            DA.GetData(2, ref runInput);
            DA.GetData(3, ref resetInput);
            DA.GetData(4, ref commandMacro);

            // Rising edge detection on Reset input
            bool resetEdge = resetInput && !_lastResetInput;
            _lastResetInput = resetInput;

            if (resetEdge)
            {
                ClearCache();
                OutputCachedResults(DA);
                return;
            }

            // Rising edge detection on the Run input (works for both Button and Toggle)
            bool risingEdge = runInput && !_lastRunInput;
            _lastRunInput = runInput;

            // Phase 3: We were woken up by ScheduleSolution after the command completed on Idle
            if (_state == RepairState.ReadyToOutput)
            {
                _state = RepairState.Idle;
                OutputCachedResults(DA);
                Message = _cachedSuccess
                    ? $"Repaired ({_cachedMeshes.Count} parts)"
                    : "Repair Failed";
                return;
            }

            // Phase 1: Rising edge triggered -> Prepare meshes and initiate safe Idle execution
            if (risingEdge && _state != RepairState.Executing)
            {
                if (rawGoos == null || rawGoos.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No geometry supplied to repair.");
                    OutputCachedResults(DA);
                    return;
                }

                if (precision <= 0.0 || double.IsNaN(precision))
                    precision = 0.05;

                StartRepairPipeline(rawGoos, precision, commandMacro);
                OutputCachedResults(DA);
                return;
            }

            // Default: If idle, keep displaying cached/internalized results
            OutputCachedResults(DA);
        }

        private void OutputCachedResults(IGH_DataAccess DA)
        {
            DA.SetData(0, _cachedCombinedMesh);
            DA.SetDataList(1, _cachedMeshes);
            DA.SetData(2, _cachedSuccess);
            DA.SetData(3, _cachedLog);
        }

        // ─── Cache Cleanup & State Reset ─────────────────────────────────

        public void ClearCache()
        {
            _cachedCombinedMesh = null;
            _cachedMeshes.Clear();
            _cachedSuccess = false;
            _cachedLog = "Cache memory cleared and state reset.";
            _state = RepairState.Idle;
            Message = "Cleared";
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Clear Cache & Reset Memory", (sender, e) =>
            {
                RecordUndoEvent("Clear GG_Repair Cache");
                ClearCache();
                ExpireSolution(true);
            }, true, false);
        }

        // ─── Serialization & Data Internalization ────────────────────────

        public override bool Write(GH_IWriter writer)
        {
            bool baseResult = base.Write(writer);
            try
            {
                writer.SetBoolean("HasCachedData", _cachedCombinedMesh != null && _cachedCombinedMesh.IsValid);
                writer.SetBoolean("CachedSuccess", _cachedSuccess);
                writer.SetString("CachedLog", _cachedLog ?? string.Empty);

                if (_cachedCombinedMesh != null && _cachedCombinedMesh.IsValid)
                {
                    byte[] mainBytes = GH_Convert.CommonObjectToByteArray(_cachedCombinedMesh);
                    if (mainBytes != null && mainBytes.Length > 0)
                        writer.SetByteArray("CombinedMeshBytes", mainBytes);
                }

                int partCount = _cachedMeshes?.Count ?? 0;
                writer.SetInt32("PartCount", partCount);
                for (int i = 0; i < partCount; i++)
                {
                    var m = _cachedMeshes[i];
                    if (m != null && m.IsValid)
                    {
                        byte[] partBytes = GH_Convert.CommonObjectToByteArray(m);
                        if (partBytes != null && partBytes.Length > 0)
                            writer.SetByteArray($"MeshPart_{i}", partBytes);
                    }
                }
            }
            catch { }
            return baseResult;
        }

        public override bool Read(GH_IReader reader)
        {
            bool baseResult = base.Read(reader);
            try
            {
                if (reader.ItemExists("CachedSuccess"))
                    _cachedSuccess = reader.GetBoolean("CachedSuccess");

                if (reader.ItemExists("CachedLog"))
                    _cachedLog = reader.GetString("CachedLog");

                if (reader.ItemExists("CombinedMeshBytes"))
                {
                    byte[] mainBytes = reader.GetByteArray("CombinedMeshBytes");
                    if (mainBytes != null && mainBytes.Length > 0)
                    {
                        _cachedCombinedMesh = GH_Convert.ByteArrayToCommonObject<Mesh>(mainBytes);
                    }
                }

                if (reader.ItemExists("PartCount"))
                {
                    int partCount = reader.GetInt32("PartCount");
                    _cachedMeshes = new List<Mesh>();
                    for (int i = 0; i < partCount; i++)
                    {
                        string key = $"MeshPart_{i}";
                        if (reader.ItemExists(key))
                        {
                            byte[] partBytes = reader.GetByteArray(key);
                            if (partBytes != null && partBytes.Length > 0)
                            {
                                var partMesh = GH_Convert.ByteArrayToCommonObject<Mesh>(partBytes);
                                if (partMesh != null)
                                    _cachedMeshes.Add(partMesh);
                            }
                        }
                    }
                }

                if (_cachedCombinedMesh != null && _cachedCombinedMesh.IsValid)
                {
                    _state = RepairState.Idle;
                    Message = $"Internalized ({_cachedMeshes?.Count ?? 1})";
                }
            }
            catch { }
            return baseResult;
        }

        // ─── Phase 1: Convert, Bake to Temp Layer & Schedule Idle ──────────

        private void StartRepairPipeline(List<IGH_GeometricGoo> goos, double precisionMm, string commandMacro)
        {
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document found.");
                return;
            }

            Message = "Meshing (0.05mm)...";

            // 1. Convert all mixed geometries into high-precision meshes
            List<Mesh> inputMeshes = ConvertToPrecisionMeshes(goos, precisionMm);
            if (inputMeshes == null || inputMeshes.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not convert any input geometry to valid meshes.");
                Message = "Meshing failed";
                return;
            }

            try
            {
                // 2. Setup clean temporary layer
                int layerIndex = SetupTempLayer(doc);

                // 3. Clear previous doc selection so no other objects get included
                doc.Objects.UnselectAll();

                // 4. Bake converted meshes to temporary layer
                _bakedGuids.Clear();
                var attr = new ObjectAttributes
                {
                    LayerIndex = layerIndex,
                    Name = "__CasaCreta_GGRepair_Input"
                };

                foreach (var mesh in inputMeshes)
                {
                    if (mesh != null && mesh.IsValid && mesh.Faces.Count > 0)
                    {
                        Guid id = doc.Objects.AddMesh(mesh, attr);
                        if (id != Guid.Empty)
                        {
                            _bakedGuids.Add(id);
                            doc.Objects.Select(id, true, true, true);
                        }
                    }
                }

                if (_bakedGuids.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to bake input meshes to Rhino document.");
                    PurgeTempLayerAndObjects(doc, _bakedGuids);
                    Message = "Bake failed";
                    return;
                }

                // 5. Sanitize command macro - ALWAYS ensure leading '!' and trailing '_Enter'
                string cmd = string.IsNullOrWhiteSpace(commandMacro) ? "! _GG_Repair_Mesh _Enter" : commandMacro.Trim();
                if (!cmd.StartsWith("!"))
                    cmd = "! " + cmd;
                if (!cmd.EndsWith("_Enter", StringComparison.OrdinalIgnoreCase) &&
                    !cmd.EndsWith("Enter", StringComparison.OrdinalIgnoreCase) &&
                    !cmd.EndsWith(";") &&
                    !cmd.EndsWith("\n"))
                {
                    cmd = cmd + " _Enter";
                }
                _activeCommandMacro = cmd;

                // 6. Snapshot document state before running command
                _startRuntimeSerial = RhinoObject.NextRuntimeSerialNumber;
                _startHistLength = RhinoApp.CommandHistoryWindowText?.Length ?? 0;

                // 7. Transition state and hook RhinoApp.Idle for safe UI execution
                _state = RepairState.Executing;
                Message = "Running GG_Repair...";

                RhinoApp.Idle += OnRhinoAppIdle;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Preparation error: {ex.Message}");
                PurgeTempLayerAndObjects(doc, _bakedGuids);
                _state = RepairState.Idle;
                Message = "Error";
            }
        }

        // ─── Phase 2: Safe Execution on RhinoApp.Idle ────────────────────

        private void OnRhinoAppIdle(object sender, EventArgs e)
        {
            // IMMEDIATELY unhook to ensure this only executes once
            RhinoApp.Idle -= OnRhinoAppIdle;

            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _state = RepairState.Failed;
                _cachedSuccess = false;
                _cachedLog = "No active Rhino document when Idle triggered.";
                ScheduleComponentUpdate();
                return;
            }

            var capturedMeshes = new List<Mesh>();
            bool commandSucceeded = false;
            string commandLog = string.Empty;
            var createdDocObjects = new List<RhinoObject>();

            uint undoRecord = doc.BeginUndoRecord("CasaCreta GG_Repair_Mesh Execution");

            try
            {
                // Execute the command in the Rhino command line (with automatic _Enter)
                commandSucceeded = RhinoApp.RunScript(_activeCommandMacro, echo: true);

                // Collect any newly created or modified objects in the document
                var settings = new ObjectEnumeratorSettings
                {
                    NormalObjects = true,
                    LockedObjects = true,
                    HiddenObjects = true,
                    DeletedObjects = false,
                    ActiveObjects = true
                };

                createdDocObjects = doc.Objects.FindByFilter(settings)
                    .Where(o => o != null && o.RuntimeSerialNumber >= _startRuntimeSerial && !o.IsDeleted)
                    .ToList();

                // If RuntimeSerialNumber check found nothing, check if the command selected its output
                if (createdDocObjects.Count == 0)
                {
                    var selected = doc.Objects.GetSelectedObjects(false, false);
                    if (selected != null)
                    {
                        createdDocObjects = selected
                            .Where(o => o != null && !_bakedGuids.Contains(o.Id))
                            .ToList();
                    }
                }

                // Extract all resulting meshes into Grasshopper memory
                foreach (var obj in createdDocObjects)
                {
                    if (obj.Geometry is Mesh m && m.IsValid && m.Faces.Count > 0)
                    {
                        var clone = m.DuplicateMesh();
                        clone.Normals.ComputeNormals();
                        clone.Compact();
                        capturedMeshes.Add(clone);
                    }
                    else if (obj.Geometry is Brep b)
                    {
                        var bMeshes = Mesh.CreateFromBrep(b, MeshingParameters.QualityRenderMesh);
                        if (bMeshes != null)
                        {
                            foreach (var bm in bMeshes)
                            {
                                if (bm != null && bm.IsValid && bm.Faces.Count > 0)
                                {
                                    bm.Normals.ComputeNormals();
                                    bm.Compact();
                                    capturedMeshes.Add(bm);
                                }
                            }
                        }
                    }
                }

                // Read command history output delta
                string fullHist = RhinoApp.CommandHistoryWindowText;
                if (!string.IsNullOrEmpty(fullHist) && fullHist.Length > _startHistLength)
                {
                    commandLog = fullHist.Substring(_startHistLength).Trim();
                }
            }
            catch (Exception ex)
            {
                commandLog += $"\n[Exception during command execution]: {ex.Message}";
                commandSucceeded = false;
            }
            finally
            {
                // CLEANUP: Purge all temporary created objects and inputs from the document
                try
                {
                    foreach (var obj in createdDocObjects)
                    {
                        if (obj != null && !obj.IsDeleted)
                            doc.Objects.Delete(obj, true);
                    }

                    PurgeTempLayerAndObjects(doc, _bakedGuids);
                    doc.Views.Redraw();
                }
                catch { }

                if (undoRecord > 0)
                {
                    doc.EndUndoRecord(undoRecord);
                }
            }

            // Package final results into cached memory
            _cachedMeshes = capturedMeshes;
            if (capturedMeshes.Count > 0)
            {
                var combined = new Mesh();
                foreach (var m in capturedMeshes)
                    combined.Append(m);

                combined.Normals.ComputeNormals();
                combined.Compact();

                _cachedCombinedMesh = combined;
                _cachedSuccess = true;
                _state = RepairState.ReadyToOutput;
            }
            else
            {
                _cachedCombinedMesh = null;
                _cachedSuccess = false;
                _state = RepairState.ReadyToOutput;
                if (string.IsNullOrEmpty(commandLog))
                    commandLog = "Command executed but produced no valid output mesh.";
            }

            _cachedLog = commandLog;

            // Safely notify Grasshopper to trigger re-solution and push outputs
            ScheduleComponentUpdate();
        }

        private void ScheduleComponentUpdate()
        {
            var ghDoc = OnPingDocument();
            if (ghDoc != null)
            {
                ghDoc.ScheduleSolution(1, d =>
                {
                    ExpireSolution(false);
                });
            }
        }

        // ─── Geometry to Precision Mesh Conversion ───────────────────────

        /// <summary>
        /// Converts any mixed geometry (open/closed surfaces, spheres, polysurfaces,
        /// SubD, extrusions, meshes) into clean meshes conforming to precisionMm tolerance.
        /// </summary>
        private static List<Mesh> ConvertToPrecisionMeshes(IEnumerable<IGH_GeometricGoo> goos, double precisionMm)
        {
            var meshes = new List<Mesh>();
            if (goos == null) return meshes;

            // Configure meshing parameters for 0.05 mm precision
            var mp = MeshingParameters.QualityRenderMesh;
            mp.Tolerance = Math.Max(0.0001, precisionMm); // Max distance from edge to NURBS surface <= 0.05 mm
            mp.RelativeTolerance = 0.0;                   // Strict absolute tolerance across all dimensions
            mp.RefineGrid = true;
            mp.ComputeCurvature = true;
            mp.JaggedSeams = false;
            mp.SimplePlanes = false;
            mp.GridAngle = RhinoMath.ToRadians(15.0);      // 15 degrees angle limit for tight curvature
            mp.MinimumEdgeLength = precisionMm * 0.1;

            foreach (var goo in goos)
            {
                if (goo == null) continue;

                object scriptVar = goo.ScriptVariable();

                if (scriptVar is Mesh directMesh)
                {
                    if (directMesh.IsValid && directMesh.Faces.Count > 0)
                    {
                        var clone = directMesh.DuplicateMesh();
                        clone.Normals.ComputeNormals();
                        clone.Compact();
                        meshes.Add(clone);
                    }
                }
                else if (scriptVar is Brep brep)
                {
                    ProcessBrep(brep, mp, meshes);
                }
                else if (scriptVar is Surface srf)
                {
                    // Handles single surfaces: Sphere, Cylinder, RevSurface, Plane, etc.
                    Brep srfBrep = srf.ToBrep();
                    if (srfBrep != null)
                        ProcessBrep(srfBrep, mp, meshes);
                }
                else if (scriptVar is Extrusion ext)
                {
                    Brep extBrep = ext.ToBrep();
                    if (extBrep != null)
                        ProcessBrep(extBrep, mp, meshes);
                }
                else if (scriptVar is SubD subd)
                {
                    // SubD density 3 generates high-fidelity polygonal mesh capturing smooth limit surfaces and creases
                    Mesh subdMesh = Mesh.CreateFromSubD(subd, 3);
                    if (subdMesh != null && subdMesh.IsValid && subdMesh.Faces.Count > 0)
                    {
                        subdMesh.Normals.ComputeNormals();
                        subdMesh.Compact();
                        meshes.Add(subdMesh);
                    }
                }
                else
                {
                    // Fallback using GH_Convert for primitive goos (GH_Sphere, GH_Box, etc.)
                    Brep castBrep = null;
                    Mesh castMesh = null;
                    if (GH_Convert.ToBrep(goo, ref castBrep, GH_Conversion.Both) && castBrep != null)
                    {
                        ProcessBrep(castBrep, mp, meshes);
                    }
                    else if (GH_Convert.ToMesh(goo, ref castMesh, GH_Conversion.Both) && castMesh != null)
                    {
                        if (castMesh.IsValid && castMesh.Faces.Count > 0)
                        {
                            var clone = castMesh.DuplicateMesh();
                            clone.Normals.ComputeNormals();
                            clone.Compact();
                            meshes.Add(clone);
                        }
                    }
                }
            }

            return meshes;
        }

        private static void ProcessBrep(Brep brep, MeshingParameters mp, List<Mesh> outputList)
        {
            if (brep == null || !brep.IsValid) return;

            Mesh[] bMeshes = Mesh.CreateFromBrep(brep, mp);
            if (bMeshes != null)
            {
                foreach (var m in bMeshes)
                {
                    if (m != null && m.IsValid && m.Faces.Count > 0)
                    {
                        m.Normals.ComputeNormals();
                        m.Compact();
                        outputList.Add(m);
                    }
                }
            }
        }

        // ─── Layer and Object Management ─────────────────────────────────

        private static int SetupTempLayer(RhinoDoc doc)
        {
            int existing = doc.Layers.FindName(TempLayerName)?.Index ?? -1;
            if (existing >= 0)
            {
                // Purge any stale objects on the layer
                doc.Layers.Purge(existing, true);
                var layer = doc.Layers[existing];
                if (layer != null)
                {
                    layer.IsVisible = true;
                    layer.IsLocked = false;
                }
                return existing;
            }

            int newLayerIdx = doc.Layers.Add(TempLayerName, Color.DarkMagenta);
            if (newLayerIdx >= 0)
            {
                var layer = doc.Layers[newLayerIdx];
                if (layer != null)
                {
                    layer.IsVisible = true;
                    layer.IsLocked = false;
                }
                return newLayerIdx;
            }

            return doc.Layers.CurrentLayerIndex;
        }

        private static void PurgeTempLayerAndObjects(RhinoDoc doc, List<Guid> bakedGuids)
        {
            if (doc == null) return;

            if (bakedGuids != null)
            {
                foreach (var guid in bakedGuids)
                {
                    try
                    {
                        var obj = doc.Objects.FindId(guid);
                        if (obj != null && !obj.IsDeleted)
                            doc.Objects.Delete(obj, true);
                    }
                    catch { }
                }
                bakedGuids.Clear();
            }

            try
            {
                int layerIdx = doc.Layers.FindName(TempLayerName)?.Index ?? -1;
                if (layerIdx >= 0)
                {
                    doc.Layers.Purge(layerIdx, true);
                }
            }
            catch { }
        }

        // ─── Icon and Metadata ───────────────────────────────────────────

        private static readonly Bitmap _cachedIcon = LoadIcon();

        private static Bitmap LoadIcon()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAMLSURBVEhLvZVPSNRRFMfvvfec"
                    + "t954ZmqZqVlhZmqlpEWWlmVRm5a1aVEbN0HbNGjTpi2qRUEEBVFtglpUURu3QURtkVZEUVkRRdZ/3zvv3pmxjzQzZ33gwRvc9348uPd+v9/vC0X/M9Xq"
                    + "h6q6K6e5q2oV3d1t1dbq/Kqq3Z2dne1TU1P3/vXg6ePHTw8ODu4ODQ0d9/f33+3t7T08PT19293dvV5dXf1qY2Pj4erq6sP19fXntbW1t+vr62+3trZeb2xs"
                    + "PF1fX388NTV1u7+/v/vw8PDxwcHBzV8d4Nn4+PjxoaGhO+3t7W2K6jRFdT4/P7/04cOH5e3t7eWtrS1ZXV3V8vLy8vLycklzc3PZ5uZmSU9Pj8zMzGhmZkaT"
                    + "k5Pa2dnR5uamNjY2ZHNzU5ubm1pfX5f19fXl7e3t5cPDw/P9/f1nAwMD9ycnJ+9V638Qz8bGxu51d3e35efnnxwbGytpbW0t4fv+6evXr798+fKl7NevX9rY"
                    + "2NDe3p729/e1v7+vw8NDHRwc6OjoSId37nzo4OBAh4eHOjw81OHhoQ4PD3V4eKijo6Pnq6urH/v7+89GR0fvj4+P3/sngN6Wlpa2/Pz8E77vny4sLLx/9uzZ"
                    + "l2/fvmnv779rZWVFe3t7Oj4+1uHhoY6Pj3V8fKyjo6Ovr1+/fj4zM3P/7du3r09PT19XVVWtTExM3B8aGrrb399/t7Oz8/b/AHR3d7f5vn+6qKjox9bWlvb2"
                    + "9rS/v6/9/X0dHR3p+PhYR0dHOjk50enpqU5PT7W7u6utra2Xnz59ev7u3bvywcHBb3Nzc7e2trZebWxsvFhdXX20tbX1dGdn59H+/v6D3d3d+zs7O4/X1tbW"
                    + "NzY21tbX1x8vLy8vLy0t/VhYWPixa9eunZ4/f/6Z6urqFSUnJ1empKTklJaWlhYVFRWUlZUVlJSUFOTm5ubn5OTkHD58+G86dOjQ1127dn0sLCws4Aelpqbm"
                    + "FBYWFubm5p48fPjw386dO/ebqqpq1Wq15/kLwQf80s/f7h0AAAAASUVORK5CYII=");

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
            new Guid("4344BDE9-F9F9-4CEE-9FE7-A083F56D597D");
    }
}
