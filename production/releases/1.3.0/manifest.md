# CasaCreta Release v1.3.0

**Build date:** 2026-10-27  
**Target framework:** net7.0-windows (Rhino 8) / net48 (Rhino 7)  
**Build configuration:** Release  

## Key Changes in v1.3.0
- **MeshEdit / ccGGRepairMesh (`GG Repair Mesh cc`):**
  - **Automatic Enter Handling**: Macro defaults to `! _GG_Repair_Mesh _Enter` and automatically appends ` _Enter` to any custom command if not present, completely eliminating prompt stalls and human keypress waiting.
  - **Data Internalization**: Implements Grasshopper SDK `Write(GH_IWriter)` and `Read(GH_IReader)` serialization with `GH_Convert.CommonObjectToByteArray` and `GH_Convert.ByteArrayToCommonObject<Mesh>`. Computed repaired meshes and logs are internalized inside the `.gh` definition chunk, surviving file saves, reloads, and canvas restarts without requiring re-computation.
  - **Reset Button & Memory Cleanup**: Added a dedicated `Reset` (`Rst`) input parameter (button/toggle) and a right-click context menu item ("Clear Cache & Reset Memory") to purge internalized mesh memory and reset component computed state at will.
  - **Mixed Geometry Support**: Collects open/closed surfaces, spheres, polysurfaces (Breps), SubD, extrusions, and meshes.
  - **0.05 mm Precision**: High-precision meshing with maximum distance tolerance of 0.05 mm.
  - **Crash-Proof Execution**: Automatically creates temporary layer `__CC_GG_REPAIR_TEMP__`, bakes and selects the meshes, runs via `RhinoApp.Idle`, captures output via `RuntimeSerialNumber`, and purges all temporary items from Rhino.
- **ReDev / ccBoundingBox (`Bounding Box cc`):**
  - High-performance multi-threaded bounding box with optional orientation plane and union/per-object modes.
- **ReDev / ccFilletEdgeV2 (`Fillet Edge cc v2`):**
  - Batched fillet edge processor designed for massive datasets (23K+ items) with live progress feedback and hyperthreading.

## Sections & Components

| Section | Component | GH Panel | Namespace |
|---|---|---|---|
| GCode | CurveOrderComponent | G-Code | CasaCreta.GCode |
| GHSync | GHSyncStatusComponent | Utility | CasaCreta.GHSync.Components |
| EtoForms | GeometryPickerComponent | EtoForms | CasaCreta.EtoForms |
| MeshEdit | GG Repair Mesh cc | Mesh Edit | CasaCreta.MeshEdit |
| Surface | CreateUVCrv | Surface | CasaCreta.Surface |
| ReDev | Extrude cc | ReDev | CasaCreta.ReDev |
| ReDev | Boundary Surfaces cc | ReDev | CasaCreta.ReDev |
| ReDev | Fillet Edge cc | ReDev | CasaCreta.ReDev |
| ReDev | Fillet Edge cc v2 | ReDev | CasaCreta.ReDev |
| ReDev | Bounding Box cc | ReDev | CasaCreta.ReDev |

## Plugin Identity

- **Assembly name:** CasaCreta
- **Assembly GUID:** `ea1216fd-2971-4bbb-856e-5f1d92f991e4`
- **GH Category:** CasaCreta

## Checksums

```
SHA-256: A85839AF34411867B4D119C4E73CC9DD194FA8E678AE36D3BCE49EB6EB195FA0  CasaCreta.gha
```

## Deployment

Copied to Grasshopper Libraries folder:
```
%APPDATA%\Grasshopper\Libraries\CasaCreta.gha
```
