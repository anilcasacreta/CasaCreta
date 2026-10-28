# CasaCreta Release v1.2.0

**Build date:** 2026-10-16  
**Target framework:** net7.0-windows (Rhino 8) / net48 (Rhino 7)  
**Build configuration:** Release  

## Key Changes in v1.2.0
- **Surface / CreateUVCrv:** Implemented true physical maximum U and V length detection using auto-selected dense isocurve sampling (1 mm physical spacing based on surface bounding box max dimension). Generates 2D planar unrolled trim curves and centered boundary rectangle.
- **MeshEdit / MeshRepairComponent:** Added zero-dependency OpenVDB/Dendro volumetric union and healing engine.

## Sections & Components

| Section | Component | GH Panel | Namespace |
|---|---|---|---|
| GCode | CurveOrderComponent | G-Code | CasaCreta.GCode |
| GHSync | GHSyncStatusComponent | Utility | CasaCreta.GHSync.Components |
| EtoForms | GeometryPickerComponent | EtoForms | CasaCreta.EtoForms |
| MeshEdit | GGRepairMesh | Mesh Edit | CasaCreta.MeshEdit |
| MeshEdit | MeshRepairComponent | Mesh_Repair | CasaCreta.MeshEdit |
| Surface | CreateUVCrv | Surface | CasaCreta.Surface |
| ReDev | Extrude cc | ReDev | CasaCreta.ReDev |
| ReDev | Boundary Surfaces cc | ReDev | CasaCreta.ReDev |
| ReDev | Fillet Edge cc | ReDev | CasaCreta.ReDev |

## Plugin Identity

- **Assembly name:** CasaCreta
- **Assembly GUID:** `ea1216fd-2971-4bbb-856e-5f1d92f991e4`
- **GH Category:** CasaCreta

## Checksums

```
SHA-256: 2D0834FBFB57D8EF30A768F8A9B28F9578758615DCC5A0B1AD1FB647590B0C81  CasaCreta.gha
```

## Deployment

Copied to Grasshopper Libraries folder:
```
%APPDATA%\Grasshopper\Libraries\CasaCreta.gha
```
