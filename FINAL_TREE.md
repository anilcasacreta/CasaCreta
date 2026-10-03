# CasaCreta final filesystem contract

**Authority:** This file is the canonical path and ownership contract.  
**Updated:** 2026-10-02  
**Rule:** Create directories only when their owning section is implemented.
Do not pre-create the entire future tree.

---

## 1. Simple path model

CasaCreta uses one project root with three top-level areas:

| Area | Purpose |
|---|---|
| `development/` | Source code, build system, documentation, standalone scripts, GH definitions and all dev artifacts |
| `production/` | Immutable versioned `.gha` releases, validated definitions, validated scripts and deployment state |
| `configuration/` | Non-secret machine and G-code settings, versioned independently from source and releases |

Standard .NET and OS paths remain where the toolchain expects them:

| Path | Purpose |
|---|---|
| `development/CasaCreta/bin/` | .NET build output (gitignored, always regenerated) |
| `development/CasaCreta/obj/` | .NET intermediate cache (gitignored, always regenerated) |
| `%APPDATA%\Grasshopper\Libraries\` | Grasshopper plugin load path (deployment target, not source-controlled) |

Build artifacts (`bin/`, `obj/`) are caches. They are not production state,
not committed, and can be deleted without data loss.

---

## 2. Current and final tree

`[current]` exists now or will exist after migration from the legacy layout.
`[planned]` is reserved and should be created only when its implementation
begins. `[placeholder]` exists but holds no implementation yet.

```text
E:\CasaCreta_Dev\
│
├── FINAL_TREE.md                                     # This file — canonical contract
├── README.md                                         # Project overview and build instructions
├── .gitignore
│
├── development/                                      [current]
│   ├── CasaCreta.slnx                                # .NET solution (single plugin project)
│   │
│   ├── CasaCreta/                                    [current] compiled Grasshopper plugin
│   │   ├── CasaCreta.csproj                          # net7.0-windows;net7.0;net48 → .gha
│   │   ├── CasaCretaInfo.cs                          # GH_AssemblyInfo (plugin identity)
│   │   │
│   │   │   # ── Component sections ──────────────────
│   │   │   # Each folder is one business section.
│   │   │   # A section owns its components, helpers and section-local docs.
│   │   │   # Namespaces follow: CasaCreta.<Section>
│   │   │
│   │   ├── GCode/                                    [current] G-code generation
│   │   │   ├── CurveOrderComponent.cs                # GH category "CasaCreta", panel "G-Code"
│   │   │   └── MarlinSyntax.md                       # section reference
│   │   │
│   │   ├── GHSync/                                   [current] live file-sync engine
│   │   │   ├── GHSyncManager.cs                      # core sync orchestrator
│   │   │   ├── GHSyncFileWatcher.cs                  # filesystem watcher
│   │   │   ├── GHSyncScriptAdapter.cs                # script replacement adapter
│   │   │   ├── GHSyncLink.cs                         # link state
│   │   │   ├── GHSyncSettings.cs                     # settings
│   │   │   ├── Components/
│   │   │   │   └── GHSyncStatusComponent.cs          # GH panel "Utility"
│   │   │   └── README.md                             # section documentation
│   │   │
│   │   ├── EtoForms/                                 [current] UI components
│   │   │   ├── GeometryPickerCommand.cs
│   │   │   ├── GeometryPickerComponent.cs            # GH panel "EtoForms"
│   │   │   ├── GeometryPickerForm.cs
│   │   │   └── Docs/                                 # section reference
│   │   │       ├── 01_eto_forms_architecture_and_csharp_guide.md
│   │   │       └── 02_eto_forms_official_reference_links.md
│   │   │
│   │   ├── MeshEdit/                                 [current] mesh operations
│   │   │   └── GGRepairMesh.cs                       # GH panel "Mesh Edit"
│   │   │
│   │   ├── Surface/                                  [current] UV-curve operations
│   │   │   ├── CreateUVCrv.cs                        # GH panel "Surface" (294 lines, full)
│   │   │   └── CreateUVcurve.cs                      # ⚠️ likely superseded by CreateUVCrv.cs
│   │   │
│   │   ├── Toolpath/                                 [planned] toolpath generation
│   │   ├── Slicing/                                  [planned] layer slicing strategies
│   │   └── PrintControl/                             [planned] extrusion and print parameters
│   │
│   ├── scripts/                                      [current] standalone GH scripts
│   │   ├── csharp/                                   # paste-ready SDK-Mode C# files
│   │   │   ├── GrasshopperCSharpScriptTemplate.cs    # canonical template
│   │   │   ├── PatternSurfaceSelector.cs
│   │   │   ├── Circle_Divide_Spheres.cs
│   │   │   ├── Contour_expression.cs
│   │   │   └── U_Shape_Z_scale.cs
│   │   └── python/                                   # GH Python scripts
│   │       └── Contour_expression.py
│   │
│   ├── definitions/                                  [current] .gh/.ghx host files
│   │   ├── Voronoi_Cylinder_Native.gh
│   │   └── voronoi_cylinder_preview.png
│   │
│   └── documentation/                                [current] all project docs
│       ├── FINAL_TREE.md → ../FINAL_TREE.md          # the root copy is authority
│       ├── handbooks/                                # developer references
│       │   ├── GH_COMPONENT_WORKFLOW.md
│       │   ├── GH_COMPONENT_DEV_PIPELINE.svg
│       │   ├── GRASSHOPPER_DEVELOPMENT_PIPELINE.md
│       │   └── RHINO_GRASSHOPPER_CSHARP_DEVELOPMENT_HANDBOOK.md
│       ├── machine/                                  # DurBeen ClayBot Mega
│       │   ├── Machine_Configuration.md              # source
│       │   ├── Operation_Manual_Draft.md             # source
│       │   ├── Machine_Interface_Explanation.md      # source
│       │   ├── 01_BabyStep.md … 13_Home.md           # interface screen docs
│       │   ├── DurBeen_ClayBot_Mega_*.pdf            # generated (gitignore candidate)
│       │   └── DurBeen_ClayBot_Mega_*.html           # generated (gitignore candidate)
│       └── research/                                 # G-code engine research
│           ├── 01_community_methods_research.md
│           ├── 02_github_repos_detailed.md
│           ├── 03_youtube_video_resources.md
│           ├── 04_zero_dependency_custom_engine_architecture.md
│           ├── 05_fastest_curve_to_gcode_procedures.md
│           ├── 06_multithreaded_csharp_example.cs
│           ├── 07_soller_large_format_marlin_analysis.md
│           ├── Marlin_GCode_Syntax_A4_Reference.md
│           ├── master_info.md
│           ├── master_flow.svg
│           ├── CasaCreta_Phase2_Pipeline.svg
│           ├── Plain_Cylinder_Gcode_Slicing_Audit.md
│           └── *.pdf, *.png, *.json                  # rendered / generated research artifacts
│
├── production/                                       [current, empty until first release]
│   ├── releases/                                     # immutable versioned builds
│   │   └── <version>/                                # e.g. 1.0.0/
│   │       ├── CasaCreta.gha                         # the built plugin
│   │       ├── manifest.md                           # version, build date, sections, checksums
│   │       └── checksums.sha256
│   ├── current/                                      # pointer to the active release
│   ├── previous/                                     # pointer to the rollback release
│   ├── definitions/                                  # production-ready .gh files
│   │   └── <definition-name>.gh
│   └── scripts/                                      # validated paste-ready scripts
│       ├── csharp/
│       └── python/
│
├── configuration/                                    [current]
│   ├── machine/                                      # DurBeen ClayBot Mega printer settings
│   │   └── versions/<config-id>/
│   ├── gcode/                                        # Marlin G-code generation parameters
│   │   └── versions/<config-id>/
│   └── grasshopper/                                  [planned] GH component presets
│       └── versions/<config-id>/
│
├── assets/                                           [current] static non-code resources
│   ├── machine-interface/                            # curated DurBeen interface screenshots
│   │   ├── *.png                                     # selected annotated screenshots
│   │   ├── BabyStep/ Extruder/ Move/ …               # per-screen organized folders
│   └── icons/                                        [planned] 24×24 component icons
│
└── tools/                                            [planned] build and deployment utilities
    ├── build-release.ps1                             # build → package → hash → release
    └── deploy-to-grasshopper.ps1                     # copy .gha to GH Libraries folder
```

---

## 3. Current implemented sections

Each section is a folder inside `development/CasaCreta/` containing one or more
`GH_Component` classes that share a business responsibility.

| Section | Namespace | GH Panel | Components | Status |
|---|---|---|---|---|
| **GCode** | `CasaCreta.GCode` | G-Code | `CurveOrderComponent` | [current] |
| **GHSync** | `CasaCreta.GHSync` | Utility | `GHSyncStatusComponent` | [current] |
| **EtoForms** | `CasaCreta.EtoForms` | EtoForms | `GeometryPickerComponent` | [current] |
| **MeshEdit** | `CasaCreta.MeshEdit` | Mesh Edit | `GGRepairMesh` | [current] |
| **Surface** | `CasaCreta.Surface` | Surface | `CreateUVCrv`, `CreateUVcurve` | [current] |
| **Toolpath** | `CasaCreta.Toolpath` | Toolpath | — | [planned] |
| **Slicing** | `CasaCreta.Slicing` | Slicing | — | [planned] |
| **PrintControl** | `CasaCreta.PrintControl` | Print | — | [planned] |

All sections share:
- GH Category: **`CasaCreta`**
- Namespace root: **`CasaCreta`**
- Build output: single **`CasaCreta.gha`**

---

## 4. Section ownership rules

1. Each section owns its source files, section-local helpers, and any
   section-scoped documentation (README, syntax references).
2. A section does not reach into another section's internal types or files.
   Cross-section dependencies use public interfaces or shared types at the
   `CasaCreta` namespace root.
3. Every new component gets a **unique `ComponentGuid`** generated at creation.
   Never reuse or change a released GUID.
4. Folder name and C# namespace segment must match:
   `GCode/` → `namespace CasaCreta.GCode`.

---

## 5. Section documentation standard

Each implemented section maintains documentation proportional to its
complexity:

| Section size | Required |
|---|---|
| Single-component section | XML doc-comments in the `.cs` file are sufficient |
| Multi-file section | One `README.md` in the section folder documenting ownership, component list, input/output contracts and known limitations |
| Section with external dependencies | Additional reference doc (e.g., `MarlinSyntax.md` in GCode) |

Project-wide handbooks live under `development/documentation/handbooks/`,
not inside individual sections.

---

## 6. Two development modes

| Mode | Artifact | Source location | Execution |
|---|---|---|---|
| **Compiled plugin** | `CasaCreta.gha` | `development/CasaCreta/` | `dotnet build` → Grasshopper loads `.gha` |
| **Paste-ready script** | `.cs` / `.py` file | `development/scripts/` | Copy into GH Script component (SDK-Mode) or link via GHSync |

These modes have independent lifecycles. A paste-ready script does not
require a `.gha` build. A compiled component does not live under `scripts/`.

Promotion path for scripts:
```text
development/scripts/csharp/MyScript.cs        (working draft)
  → production/scripts/csharp/MyScript.cs     (validated, version-tagged)
```

Promotion path for the plugin:
```text
development/CasaCreta/ → dotnet build
  → production/releases/<version>/CasaCreta.gha
  → production/current/ (pointer)
```

When a paste-ready script matures into a compiled component, it migrates
from `development/scripts/` into the appropriate section under
`development/CasaCreta/<Section>/`. The paste-ready copy is retired.

---

## 7. Release and deployment

### 7.1 Release lifecycle

```text
1. Build from development/CasaCreta/ using dotnet build -c Release.
2. Copy the .gha to production/releases/<version>/.
3. Generate manifest.md with: version, build date, target frameworks,
   section list, component GUIDs, and SHA-256 checksums.
4. Point production/current/ to the new release.
5. Move the old current to production/previous/.
6. Deploy: copy .gha from production/current/ to Grasshopper Libraries.
7. Validate: open Grasshopper, confirm all sections load, place each
   component, verify SolveInstance fires.
```

### 7.2 Rollback

Rollback changes only the `current` pointer back to `previous`. No source
change, rebuild, or configuration change is required.

### 7.3 Release directory content

```text
production/releases/<version>/
├── CasaCreta.gha
├── manifest.md
└── checksums.sha256
```

Releases are immutable after acceptance. Never overwrite a released version
with different bits.

---

## 8. Configuration

Configuration is versioned separately from source and releases.

| Area | Owns |
|---|---|
| `configuration/machine/` | DurBeen ClayBot Mega printer parameters (bed size, nozzle diameter, speeds, acceleration, steps/mm) |
| `configuration/gcode/` | Marlin G-code generation settings (start/end sequences, retraction, temperature profiles, layer heights) |
| `configuration/grasshopper/` | [planned] Component preset values and saved states |

Each configuration area uses versioned subdirectories:
```text
configuration/machine/
├── versions/
│   ├── 20261002T171200Z/
│   │   └── machine.toml
│   └── <future-config-id>/
├── current → versions/<active-id>
└── previous → versions/<rollback-id>
```

Configuration never contains secrets. Secrets (API keys, credentials) are
stored outside this repository and outside version control.

---

## 9. Locked decisions

| Decision | Value | Rationale |
|---|---|---|
| Plugin name | **CasaCreta** | Grasshopper identity, assembly name, GH category tab |
| GH Category | **`CasaCreta`** | Single tab in the Grasshopper ribbon |
| Namespace root | **`CasaCreta`** | All sections descend from `CasaCreta.<Section>` |
| Build output | **`.gha`** | Grasshopper Assembly, set by `<TargetExt>.gha</TargetExt>` |
| Target frameworks | **`net7.0-windows;net7.0;net48`** | Cross-platform + .NET Framework fallback |
| NuGet dependency | **`Grasshopper 8.0.23304.9001`** | Rhino 8 SDK; update only with verified compatibility |
| Solution format | **`.slnx`** | Lightweight XML solution (VS 2022 17.10+) |
| Assembly GUID | `ea1216fd-2971-4bbb-856e-5f1d92f991e4` | Registered in `CasaCretaInfo.cs`; never change |
| Source organization | **By business section** | Not by language, file type, or geometry kind |
| Development root | `development/` | All source, docs, scripts and definitions |
| Production root | `production/` | Only validated, immutable, deployable artifacts |
| Two dev modes | Compiled `.gha` + paste-ready scripts | Coexist; independent lifecycles |
| DurBeen machine docs | Under `documentation/machine/` | Machine knowledge stays with the project |
| Research docs | Under `documentation/research/` | G-code engine research stays accessible |

---

## 10. Adding a new section

When you begin developing new Grasshopper components in a new business area:

```text
1. Create the folder under development/CasaCreta/<SectionName>/.
2. Add the first .cs component file with:
   - namespace CasaCreta.<SectionName>
   - GH_Component constructor: category "CasaCreta", subcategory "<Panel Name>"
   - unique ComponentGuid
3. Build and verify the component appears in the correct GH tab and panel.
4. If the section has more than one component or external dependencies,
   add a README.md to the section folder.
5. Mark the section as [current] in this FINAL_TREE.md.
6. Do not pre-create [planned] section folders until step 1 is ready.
```

---

## 11. Cross-section conventions

- Components within one section may freely share internal helpers.
- Cross-section access uses only `public` types at the section boundary.
- If two sections need the same geometry utility, extract it to a shared
  file at the `CasaCreta/` root (e.g., `CasaCreta/Shared/GeometryHelpers.cs`)
  or a `Common/` folder. Do not duplicate code across sections.
- Each section registers its components with a consistent GH panel name.
  The panel name is the section's public identity in Grasshopper.

---

## 12. Migration from current layout

The current flat layout must be restructured into the `development/` +
`production/` model. This is a relocation, not a rewrite.

### Files that move

| Current path | Target path |
|---|---|
| `CasaCreta.slnx` | `development/CasaCreta.slnx` |
| `CasaCreta/` (entire project) | `development/CasaCreta/` |
| `scripts/` | `development/scripts/` |
| `docs/dev/` | `development/documentation/handbooks/` |
| `docs/machine/` | `development/documentation/machine/` |
| `docs/research/` | `development/documentation/research/` |
| `assets/` | `assets/` (stays at root) |
| `tools/` | `tools/` (stays at root) |
| `README.md` | `README.md` (stays at root) |
| `.gitignore` | `.gitignore` (stays at root, update paths) |

### Files to update after relocation

| File | Change required |
|---|---|
| `CasaCreta.slnx` | Update project path: `CasaCreta/CasaCreta.csproj` (relative stays same) |
| `.gitignore` | Prefix `development/` to build output patterns |
| `README.md` | Update folder descriptions |

### New directories to create

| Directory | When |
|---|---|
| `production/releases/` | First release build |
| `production/current/` | First deployment |
| `production/previous/` | Second deployment (rollback target) |
| `production/definitions/` | First validated `.gh` file |
| `production/scripts/` | First validated script |
| `configuration/machine/` | First machine config extraction |
| `configuration/gcode/` | First G-code config extraction |
| `development/definitions/` | When `.gh` files move from `scripts/` |

### What this migration does NOT do

- Does not rewrite any C# source code.
- Does not change namespaces, GUIDs, or component metadata.
- Does not delete the legacy layout until validation is complete.
- Does not create empty [planned] section folders.
- Does not touch `.gha` build output or Grasshopper loading behavior.

---

## 13. Gitignore policy

The `.gitignore` must cover:

```text
# .NET build output
development/CasaCreta/bin/
development/CasaCreta/obj/

# IDE
.vs/
*.user
*.suo

# NuGet
packages/
*.nupkg

# Python
__pycache__/
*.pyc
pydeps/

# Temporary artifacts
tmp/
chrome-profile*/
edge-profile*/

# OS
Thumbs.db
Desktop.ini
.DS_Store

# Generated docs (optional — keep if distribution is needed)
# development/documentation/machine/*.pdf
# development/documentation/machine/*.html
```

Build artifacts, browser profiles, vendored dependencies, and OS junk
never enter version control. Generated PDFs and HTML from markdown sources
are optional — include them only if they need distribution without a
build step.

---

## 14. Definition and script governance

### Grasshopper definitions (`.gh` / `.ghx`)

- Source definitions live under `development/definitions/`.
- A definition is promoted to `production/definitions/` only after it
  passes verification against the current production `.gha` release.
- Definitions reference components by `ComponentGuid`. Changing a released
  GUID breaks all definitions that use that component.

### Paste-ready scripts

- Working scripts live under `development/scripts/`.
- Validated scripts are copied (not moved) to `production/scripts/`.
- Each script file documents its GH component parameter contract near the
  top (input names, types, access modes, output names).

---

## 15. Acceptance gates

A release is accepted only when:

- `dotnet build -c Release` succeeds for all target frameworks.
- The `.gha` loads in Grasshopper without errors.
- Every section's components appear in the correct GH category and panel.
- `SolveInstance` fires on placement and input change for each component.
- No stale component GUIDs conflict with existing definitions.
- Manifest and checksums are recorded.
- `production/previous/` points to a valid rollback.
- The deployer has verified in a live Grasshopper session.

---

## 16. Cleanup boundary

No file is deleted merely because a target location exists.

Before cleanup of the legacy flat layout:

1. Verify every file exists at its new location under `development/`.
2. Confirm `dotnet build` succeeds from `development/`.
3. Confirm the `.gha` loads correctly in Grasshopper.
4. Confirm documentation links and relative paths are not broken.
5. Obtain explicit approval before deleting the old flat copies.

This document does not authorize deletion, rewrite, or deployment by itself.
