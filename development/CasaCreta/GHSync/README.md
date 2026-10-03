# CasaCreta GHSync

GHSync links a modern Rhino 8 C# Script component to an external `.cs`
file. The external file remains the source of truth and is watched without
requiring a VS Code extension.

## Current v0.1 workflow

1. Build `CasaCreta.slnx` and install the resulting `CasaCreta.gha`.
2. In Grasshopper, place **CasaCreta > Development > C# File Sync**.
3. Select the target Rhino 8 C# Script component and the C# File Sync
   component.
4. Right-click C# File Sync and choose
   **Capture selected C# Script component**.
5. Right-click it again and choose
   **Link existing .cs file (file -> component)**.
6. Edit and save the linked file. GHSync debounces the save event, updates
   the target component on Rhino's UI thread, and expires its solution.

Use **Export component to new .cs file** when the embedded component source
should be the initial source of truth.

## Inputs

- `File`: optional path override. Relative paths resolve from the saved
  Grasshopper document.
- `Enabled`: starts or stops watching without deleting the saved link.
- `Auto Recompute`: when enabled, rebuilds and recomputes the target after
  source replacement. When disabled, the new source is rebuilt and the
  target is left expired until the next Grasshopper solution.

## Working with Grasshopper geometry

GHSync is a source-code controller, not a geometry proxy. Curves, surfaces,
meshes, numbers, and other data remain connected directly to the native
Rhino 8 C# Script component. Its output wires also remain unchanged.

The external `.cs` file must keep a `RunScript` signature compatible with
the inputs and outputs configured on that native component. GHSync preserves
those parameters and their existing wires while replacing and rebuilding
the component source.

After `Enabled` changes from false back to true, GHSync immediately pushes
the linked file once. It does not wait for another file-save event.

## Safety

- Empty files are not pushed automatically.
- Pulling component source into a file requires confirmation.
- Deleted files do not erase component source.
- The target is identified by its Grasshopper `InstanceGuid`.
- Rhino 8.31 language state is preserved, and a C# language directive is
  added to pushed source when missing.

## Scope

Version 0.1 synchronizes source only. It does not add, remove, or reorder
Grasshopper input/output parameters from the `RunScript` signature.
