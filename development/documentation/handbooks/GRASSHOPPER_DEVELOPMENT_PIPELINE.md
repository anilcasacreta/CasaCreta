# Rhino 8 / Grasshopper Development Pipeline

Last verified: 2026-07-16

## Purpose

This repository is the source of truth for Casa Creta Grasshopper C#, Python, Grasshopper definitions, tests, and development notes. Code should not live only inside a `.gh` file when it is intended to be maintained or reviewed.

For the complete research and implementation reference covering embedded SDK-Mode scripts, shared Script-Mode files, Visual Studio `.gha` components, RhinoCommon, data trees, debugging, testing, and packaging, read [RHINO_GRASSHOPPER_CSHARP_DEVELOPMENT_HANDBOOK.md](RHINO_GRASSHOPPER_CSHARP_DEVELOPMENT_HANDBOOK.md).

The current `src/csharp/` files are source-controlled, paste-ready SDK-Mode copies for embedded Grasshopper C# components unless a file explicitly states otherwise.

## Verified local environment

| Tool | Installed version/location |
| --- | --- |
| Rhino | Rhino 8.31.26126.13431 |
| Rhino executable | `C:\Program Files\Rhino 8\System\Rhino.exe` |
| Grasshopper SDK | `C:\Program Files\Rhino 8\Plug-ins\Grasshopper\Grasshopper.dll` (8.31) |
| RhinoCommon SDK | `C:\Program Files\Rhino 8\System\RhinoCommon.dll` |
| RhinoCode CLI | `C:\Program Files\Rhino 8\System\rhinocode.exe` |
| .NET SDK | 8.0.423 and 10.0.302 |
| IDE | Visual Studio Community 2026 (18.8) |

Rhino 8 and Grasshopper versions match. For compiled plug-ins, target the framework/version selected by the current McNeel Rhino/Grasshopper project template rather than guessing a target framework.

## Recommended development modes

| Need | Recommended mode | Source of truth | Execution |
| --- | --- | --- | --- |
| Rapid Grasshopper C# development | External C# Script-Mode file | `.csx`/`.cs` in this repo | Grasshopper Script component with `script` input set to `Input is Path` |
| Rapid Grasshopper Python development | External Python file | `.py` in this repo | Grasshopper Script component with `script` input set to `Input is Path` |
| Standalone Rhino command/automation | Rhino ScriptEditor script | `.cs` or `.py` in this repo | `ScriptEditor`, Rhino macro, or `rhinocode script` |
| Stable reusable Grasshopper tool | Compiled Grasshopper plug-in | C# project in this repo | Build/debug a `.gha` with Visual Studio |
| Distribution | Rhino/Grasshopper project or plug-in package | Project source in this repo | Build a `.gha`/`.rhp` and optionally a Yak package |

## The fastest external-file workflow

Rhino 8's modern Grasshopper Script component can execute a script supplied through a special `script` input. This gives us the requested workflow: select a file, edit it outside Grasshopper, and execute it in the component.

### One-time Grasshopper setup

1. Add the modern **Script** component from **Maths > Script**.
2. Choose **C#** as its language.
3. Configure the component's normal inputs and outputs, including their names, type hints, and Item/List/Tree access.
4. Hold **Shift**, right-click the middle of the Script component, and enable **Script Input Parameter (`script`)**.
5. Right-click the new `script` input and enable **Input is Path**.
6. Supply the path of the external source file. A relative path is preferable when the `.gh` definition is stored in this repository beside the source tree.
7. Set the language override on the `script` input to C#, or place this directive at the top of the file:

   ```csharp
   // #! csharp
   ```

8. After saving the external file, recompute the Grasshopper solution if it does not expire automatically. If compilation appears stale, right-click the component and use **Discard Cache**.

### Critical limitation for the current script

The existing pattern/morph-surface code is in **SDK-Mode**:

```csharp
public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(/* ... */)
    {
        // ...
    }
}
```

McNeel's current C# Script component guide states that SDK-Mode is not supported for shared/input scripts. Therefore, the current file cannot simply be connected as a path unchanged.

For path-based execution, convert it to **Script-Mode**:

- Remove the `Script_Instance : GH_ScriptInstance` wrapper.
- Remove the `RunScript` signature.
- Treat configured Grasshopper inputs (`Points`, `MorphSurface`, and so on) as variables supplied to the script.
- Assign the configured outputs (`HitMask`, `HitPoints`, and so on) directly.
- Keep helper logic as local functions or helper types supported by the script.
- Replace `Component.AddRuntimeMessage(...)` only if the Script-Mode context does not expose the same component member; otherwise keep it after verifying in Rhino.

The Grasshopper component must retain this exact input/output contract:

### Inputs

| Name | Suggested type hint | Access |
| --- | --- | --- |
| `Points` | `Point3d` | Tree |
| `MorphSurface` | `Brep` | Tree |
| `Tolerance` | `double` | Item |
| `PatternCount` | `int` | Item |
| `SpaceCount` | `int` | Item |
| `InvertPattern` | `bool` | Item |

### Outputs

`HitMask`, `HitPoints`, `NotHitPoints`, `ClosestPoints`, `Distances`, `PatternPoints`, `InversePatternPoints`, and `BooleanPattern`.

Do not rename a component input/output without changing the external script and recording the contract change.

## Copy/paste and Export Script behavior

Copy/paste into an embedded C# component remains valid and supports SDK-Mode. It is useful for quick experiments, but the `.gh` file becomes the source of truth and normal Git review becomes difficult.

The component's **Export Script** command saves a copy. McNeel has clarified that exporting does not maintain an association between the exported file and the component. Do not treat Export Script as automatic synchronization.

## Standalone Rhino scripts and remote execution

Standalone C# or Python files can be run by Rhino 8 without a Grasshopper component:

```text
_-ScriptEditor _R "E:\casa_creta_dot_net\Grasshopper_scripting\src\scripts\example.cs"
```

This is appropriate for Rhino commands, document automation, geometry tests, and setup utilities. It does not provide Grasshopper component inputs and outputs.

Rhino 8.11 and later also includes `rhinocode`, which communicates with Rhino's scripting server. The local Rhino 8.31 installation contains this CLI.

### RhinoCode loop

1. In Rhino, run `StartScriptServer`.
2. From a terminal, list available Rhino processes:

   ```powershell
   & 'C:\Program Files\Rhino 8\System\rhinocode.exe' list
   ```

3. Inspect command help before automating:

   ```powershell
   & 'C:\Program Files\Rhino 8\System\rhinocode.exe' script --help
   ```

4. Run the chosen script against the intended Rhino instance. If multiple Rhino instances are open, select the instance explicitly with the CLI's `--rhino` option.

Use RhinoCode for external-editor/agent-to-Rhino execution and for ScriptEditor project builds. Do not confuse this with reactive execution inside a Grasshopper component.

## When to move to a compiled `.gha`

Convert a mature script into a Grasshopper plug-in when any of these becomes true:

- It needs a stable reusable component with its own name, icon, category, and GUID.
- The script is large enough that component compilation or maintenance is awkward.
- It requires proper unit boundaries, multiple source files, NuGet dependencies, or shared libraries.
- It requires Visual Studio breakpoints and repeatable builds.
- It will be distributed to other machines.

Use McNeel's current Grasshopper project template. A compiled component derives from `GH_Component` and implements `RegisterInputParams`, `RegisterOutputParams`, `SolveInstance`, and a permanent unique `ComponentGuid`. Never change a released component GUID casually because Grasshopper definitions use it to identify the component.

## Repository layout

Use this layout as files are added:

```text
Grasshopper_scripting/
├── docs/
│   └── GRASSHOPPER_DEVELOPMENT_PIPELINE.md
├── src/
│   ├── csharp/             # Paste-ready embedded Grasshopper C# SDK-Mode files
│   ├── shared/             # Optional external/shared Script-Mode files
│   ├── python/             # External Grasshopper/Rhino Python files
│   ├── scripts/            # Standalone Rhino scripts
│   └── plugins/            # Compiled .gha/.rhp solutions
├── grasshopper/            # .gh/.ghx host definitions and examples
├── tests/                  # Fixtures, expected results, test definitions
└── README.md
```

Generated `bin/`, `obj/`, `.vs/`, packages, and local caches should not be committed unless there is a specific packaging reason.

## Change and verification procedure

For every Grasshopper script change:

1. Read the script and its documented input/output contract.
2. Make the smallest source-controlled change.
3. Check null/empty trees, empty branches, invalid geometry, tolerance fallback, and input access modes.
4. Open the host `.gh` definition and recompute.
5. Confirm the Rhino/Grasshopper runtime has loaded the saved file, not an embedded stale copy.
6. Verify tree paths and item counts for every output.
7. Test at least:
   - normal geometry;
   - no Breps;
   - empty point tree/branches;
   - zero or negative tolerance;
   - zero/negative pattern and spacing counts;
   - both invert states;
   - points within, on, and outside tolerance.
8. Record any required plug-in and its exact version.
9. Keep a minimal `.gh` regression definition when behavior depends on Grasshopper data-tree semantics.

## Documentation source policy

Use sources in this order so implementation decisions are reproducible:

1. **McNeel official developer guides and API documentation** for supported behavior.
2. **McNeel-authored answers on Discourse** for Rhino-version-specific details not yet present in a guide.
3. **Grasshopper forum** for historical/community knowledge, verified against Rhino 8 behavior.
4. **GrasshopperDocs** for discovering third-party plug-in components, then verify against the plug-in author's documentation and installed version.
5. **Food4Rhino/Rhino Package Manager** for plug-in discovery and distribution metadata, not as the primary API specification.

Do not guess an API signature. Check the installed RhinoCommon/Grasshopper XML/API documentation or the official online documentation. For third-party components, record the plug-in name and version because component behavior can change between releases.

## Canonical references

### Current Rhino 8 scripting and Grasshopper workflow

- [Grasshopper C# scripting guide](https://developer.rhino3d.com/en/guides/scripting/scripting-gh-csharp/)
- [Grasshopper Script component](https://developer.rhino3d.com/en/guides/scripting/scripting-component/)
- [Essential C# Scripting for Grasshopper](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/)
- [Grasshopper developer guides](https://developer.rhino3d.com/guides/grasshopper/)
- [ScriptEditor command macros](https://developer.rhino3d.com/guides/scripting/advanced-scripteditor-macros/)
- [RhinoCode command-line interface](https://developer.rhino3d.com/guides/scripting/advanced-cli/)
- [Creating Rhino/Grasshopper Script Plugins](https://developer.rhino3d.com/en/guides/scripting/projects-create/)
- [Publishing Rhino/Grasshopper Script Plugins](https://developer.rhino3d.com/en/guides/scripting/projects-publish/)
- [Your First Grasshopper Component on Windows](https://developer.rhino3d.com/guides/grasshopper/your-first-component-windows/)
- [Official developer samples](https://developer.rhino3d.com/samples/)

### SDK families supplied for this codebase

- [Rhino developer portal](https://developer.rhino3d.com/)
- [RhinoCommon guides](https://developer.rhino3d.com/guides/rhinocommon/)
- [Rhino.Python guides](https://developer.rhino3d.com/guides/rhinopython/)
- [openNURBS guides](https://developer.rhino3d.com/guides/opennurbs/)
- [RhinoScript guides](https://developer.rhino3d.com/guides/rhinoscript/)
- [C++ guides](https://developer.rhino3d.com/guides/cpp/)

### Community and third-party discovery

- [McNeel Discourse](https://discourse.mcneel.com/)
- [Grasshopper community forum](https://www.grasshopper3d.com/forum)
- [GrasshopperDocs plug-in component index](https://grasshopperdocs.com/)
- [McNeel explanation of external script paths](https://discourse.mcneel.com/t/rhino-8-feature-scripteditor-cpython-csharp/128353/425)
- [McNeel clarification that Export Script is not linked](https://discourse.mcneel.com/t/using-the-export-script-option-in-the-new-script-editor-node/170412)
