# Rhino 8 / Grasshopper C# Development Handbook

Last researched: 2026-07-23  
Primary environment: Rhino 8.31 on Windows, Grasshopper 1, C#, Visual Studio 2026  
Repository: `E:\casa_creta_dot_net\Grasshopper_scripting`

## 1. Purpose

This is the first-stop development reference for Codex and human developers working on Casa Creta Rhino/Grasshopper tools. It covers:

- C# code pasted into a Grasshopper Script component;
- external/shared C# scripts in Rhino 8;
- native compiled Grasshopper components developed in Visual Studio;
- RhinoCommon geometry;
- Grasshopper item/list/tree data handling;
- debugging, testing, performance, packaging, and distribution;
- where to verify an API or workflow before implementing it.

This handbook is a routing and workflow document, not a replacement for the live API references. Rhino and Grasshopper APIs evolve. Before using an unfamiliar method, verify its exact signature and the version in which it became available.

## 2. Source authority and research protocol

Use sources in this order:

1. The installed RhinoCommon and Grasshopper assemblies/XML documentation matching the user's Rhino version.
2. The official [Rhino Developer Guides](https://developer.rhino3d.com/guides/).
3. The official [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/) and [Grasshopper API](https://developer.rhino3d.com/api/grasshopper/).
4. McNeel's official [developer samples](https://developer.rhino3d.com/samples/) and [rhino-developer-samples repository](https://github.com/mcneel/rhino-developer-samples).
5. Answers by McNeel staff on [McNeel Discourse](https://discourse.mcneel.com/c/grasshopper-developer/).
6. Community answers on McNeel Discourse, checked against the installed API.
7. Third-party documentation only for third-party plug-ins, with the plug-in name and version recorded.

Do not guess a method overload, parameter order, return contract, thread-safety guarantee, or version requirement.

### How Codex should research a missing detail

1. Identify the development mode: embedded SDK-Mode script, Script-Mode/shared script, ScriptEditor project, or compiled `.gha`.
2. Identify the owner of the API:
   - `Rhino.Geometry`, `RhinoDoc`, display, object tables: RhinoCommon.
   - `GH_Component`, parameters, data access, trees, canvas: Grasshopper SDK.
   - build/runtime/package behavior: Rhino/Grasshopper guides and Yak guides.
3. Search the exact type and method in the official API.
4. Check `Available since` and the API documentation version.
5. Check official samples for a working usage pattern.
6. Use Discourse for practical or version-specific gaps.
7. Record any version-dependent decision in the code comment or project documentation.

## 3. Choose the correct development mode

| Goal | Correct mode | Code shape | Build/deployment |
| --- | --- | --- | --- |
| Fast local algorithm development | Embedded Grasshopper C# SDK-Mode | `Script_Instance : GH_ScriptInstance` with `RunScript` | Paste into C# component; Grasshopper compiles internally |
| Small simple script | Embedded Grasshopper C# Script-Mode | Top-level statements/functions | Paste into C# component |
| File-linked script | Shared/input Script-Mode | Top-level statements/functions; no SDK wrapper | Connect file using the component's `script` input |
| Publish scripts without manually writing a full plug-in | Rhino ScriptEditor project | Script project and published components | ScriptEditor or `rhinocode` produces plug-in/package artifacts |
| Stable native Grasshopper component | Compiled Visual Studio `.gha` | Class derives from `GH_Component` | Build class library, debug in Rhino, package with Yak |
| Rhino command or document automation | RhinoCommon `.rhp`/ScriptEditor | Rhino plug-in command or standalone script | Rhino plug-in or ScriptEditor project |

Official references:

- [Grasshopper Scripting: C#](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/)
- [Essential C# Scripting for Grasshopper](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/)
- [Creating Rhino/Grasshopper Script Plugins](https://developer.rhino3d.com/guides/scripting/projects-create/)
- [Publishing Rhino/Grasshopper Script Plugins](https://developer.rhino3d.com/guides/scripting/projects-publish/)
- [What is a Grasshopper Component?](https://developer.rhino3d.com/guides/grasshopper/what-is-a-grasshopper-component/)

## 4. Primary Casa Creta workflow: embedded C# SDK-Mode

The current repository's `src/csharp/*.cs` files are source-controlled copies of complete scripts intended to be pasted into Grasshopper's C# Script editor.

No separate Visual Studio project, `.csproj`, `.dll`, or `.gha` is required. C# is still a compiled language: the Grasshopper component performs compilation internally when the script, parameters, access modes, type hints, or references change.

### 4.1 Required code shape

```csharp
// Grasshopper Script Instance
#region Usings
using System;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
        Point3d Point,
        List<Curve> Curves,
        DataTree<Brep> Breps,
        ref object Result)
    {
        // Validate inputs, compute, and assign outputs.
        Result = null;
    }

    // Helper methods belong inside Script_Instance.
}
```

Use [GrasshopperCSharpScriptTemplate.cs](../src/csharp/GrasshopperCSharpScriptTemplate.cs) as the repository template.

### 4.2 Component parameter contract

The `RunScript` signature and Grasshopper component parameters are one contract.

| Grasshopper access | SDK-Mode C# input example |
| --- | --- |
| Item | `double`, `int`, `bool`, `Point3d`, `Curve`, `Brep` |
| List | `List<double>`, `List<Point3d>`, `List<Curve>` |
| Tree | `DataTree<double>`, `DataTree<Point3d>`, `DataTree<Brep>` |

Outputs in SDK-Mode are normally `ref object`. Assign a scalar, list, geometry object, or `DataTree<T>` to them.

When creating a component:

1. Add the modern C# Script component.
2. Create meaningful input/output names.
3. Set each input's type hint.
4. Set Item, List, or Tree access.
5. Mark genuinely mandatory inputs as Required when appropriate.
6. Paste the complete SDK-Mode file.
7. Confirm Grasshopper's generated/recognized `RunScript` signature matches the file.
8. Save the `.gh` definition and retain the `.cs` source copy in this repository.

Important behaviors from the official guide:

- The `out` parameter captures `Console.WriteLine` output and can be disabled when unused.
- Outputs remain objects; output type hints convert assigned values.
- `RunScript` may execute multiple times under Item/List matching.
- `BeforeRunScript` and `AfterRunScript` execute once per complete solution.
- SDK-Mode can implement viewport drawing overrides.
- The Rhino 8 script editor supports breakpoints, variables, watches, and call stacks.

### 4.3 Available SDK-Mode context

`GH_ScriptInstance` provides:

- `RhinoDocument`
- `GrasshopperDocument`
- `Component`
- `Iteration`
- `Print(...)`
- `Reflect(...)`

Use runtime messages for user-facing validation:

```csharp
Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning,
    "Tolerance must be positive; document tolerance is being used.");
```

Use `Print` or `Console.WriteLine` for diagnostics, not as the only error-reporting mechanism.

### 4.4 SDK-Mode lifecycle and preview

Use only when needed:

- `BeforeRunScript`: initialize solution-wide state.
- `RunScript`: compute each matched input iteration.
- `AfterRunScript`: clean up or finalize after all iterations.
- `DrawViewportWires`: custom points/curves/wires.
- `DrawViewportMeshes`: shaded or transparent geometry.
- `ClippingBox`: bounds for custom preview geometry.

Preview methods are called repeatedly during viewport interaction and run outside the solve sequence. Do not put heavy computation in them. Compute once during solve and cache only the safe preview data required for drawing.

### 4.5 Shared/input scripts are different

Rhino 8 can read a script from a special `script` input set to `Input is Path`, but shared/input scripts use Script-Mode. Do not connect a full `Script_Instance : GH_ScriptInstance` SDK-Mode file as a shared input script.

For Script-Mode:

- remove the `Script_Instance` wrapper;
- remove the `RunScript` method;
- access component input variables directly;
- assign output variables directly;
- pass input data explicitly into helper classes when those classes cannot see top-level variables.

SDK-Mode and Script-Mode are both valid inside the editor; they are not interchangeable file formats for the shared-script input.

## 5. Native compiled component workflow in Visual Studio

Use a compiled component when the tool needs stable reuse, a category and icon, a permanent component identity, multiple source files, proper library boundaries, dependencies, stronger debugging, automated builds, or distribution.

### 5.1 Environment and target framework

For the current machine, use the McNeel Rhino/Grasshopper project template compatible with the installed Rhino 8 and Visual Studio version. Do not hand-invent a target framework before checking the current template.

Current official runtime guidance:

- Rhino 8.20 and later defaults to .NET 8.
- A Rhino 8 plug-in targeting .NET Core should normally target `net8.0`.
- `net48` is only needed when supporting Rhino 8's deprecated .NET Framework fallback or Rhino 7.
- Multi-target `net48;net8.0` only when that compatibility is actually required.
- Use `AnyCPU` for managed cross-platform assemblies.
- Do not assume a Rhino 9/.NET 10 target is suitable for Rhino 8.

See [Moving to .NET Core](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/). Re-check this guide when the installed Rhino major version changes.

### 5.2 Create the project

1. Install the current McNeel Rhino/Grasshopper templates.
2. Create a Grasshopper component/library project.
3. Select the installed Rhino version and required compatibility targets.
4. Build the untouched template.
5. Launch Rhino through the template's debug profile.
6. Open Grasshopper and place the template component.
7. Confirm a breakpoint in `SolveInstance` is hit.
8. Only then replace the sample logic.

Official starting points:

- [Installing Tools (Windows)](https://developer.rhino3d.com/guides/grasshopper/installing-tools-windows/)
- [Your First Component (Windows)](https://developer.rhino3d.com/guides/grasshopper/your-first-component-windows/)
- [Simple Component](https://developer.rhino3d.com/guides/grasshopper/simple-component/)

Some older Grasshopper guides show Rhino 6/7 and .NET Framework-era project setup. Their `GH_Component` concepts remain useful, but runtime and target-framework decisions must come from the current templates and the current .NET runtime guide.

### 5.3 Native component anatomy

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace CasaCreta.Grasshopper
{
    public sealed class ExampleComponent : GH_Component
    {
        public ExampleComponent()
          : base(
              "Example Component",
              "Example",
              "Clear description of the operation.",
              "Casa Creta",
              "Geometry")
        {
        }

        protected override void RegisterInputParams(
            GH_InputParamManager pManager)
        {
            pManager.AddPointParameter(
                "Points", "P", "Points to process", GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Tolerance", "T", "Distance tolerance",
                GH_ParamAccess.item, 0.001);
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter(
                "Result", "R", "Processed points", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var points = new List<Point3d>();
            double tolerance = 0.001;

            if (!DA.GetDataList(0, points))
                return;
            if (!DA.GetData(1, ref tolerance))
                return;

            if (tolerance <= 0.0)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "Tolerance must be greater than zero.");
                return;
            }

            DA.SetDataList(0, points);
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("GENERATE-A-UNIQUE-GUID-FOR-THIS-COMPONENT");
    }
}
```

Every native component requires:

- a constructor specifying name, nickname, description, category, and subcategory;
- `RegisterInputParams`;
- `RegisterOutputParams`;
- `SolveInstance`;
- a permanent unique `ComponentGuid`;
- optionally a 24×24 icon and exposure setting.

Never copy the sample GUID and never change a released component GUID. Grasshopper definitions identify component types by GUID. Changing it makes existing `.gh` files treat the component as missing.

### 5.4 Parameter registration

Use the strongly typed `pManager.Add...Parameter` methods. Each parameter needs:

- full name;
- nickname;
- useful description;
- `GH_ParamAccess.item`, `.list`, or `.tree`;
- optional default value where appropriate.

Make optional inputs explicitly optional:

```csharp
int index = pManager.AddNumberParameter(
    "Tolerance", "T", "Optional tolerance",
    GH_ParamAccess.item);
Params.Input[index].Optional = true;
```

Do not reorder or remove released parameters casually. Existing Grasshopper definitions connect wires by parameter identity/order and may break. Treat parameter-contract changes as migrations.

### 5.5 Data access

| Registered access | Read | Write |
| --- | --- | --- |
| Item | `DA.GetData(index, ref value)` | `DA.SetData(index, value)` |
| List | `DA.GetDataList(index, list)` | `DA.SetDataList(index, values)` |
| Tree | `DA.GetDataTree(index, out tree)` | `DA.SetDataTree(index, tree)` |

Always check the Boolean result from input retrieval unless the parameter is intentionally optional and a documented fallback is used.

Use output indices consistently. Prefer small constants or an enum-like local convention when a component has many ports.

## 6. Grasshopper data model: items, lists, and trees

A Grasshopper data tree is an ordered collection of branches. Every branch has a unique `GH_Path`, and every branch contains an ordered list of items.

Examples:

```text
{0}       -> item 0, item 1, item 2
{1}       -> item 0
{1;0}     -> item 0, item 1
```

The path describes branch identity and ancestry; the item index describes position inside a branch.

Core learning references:

- [Introduction to Data Structures](https://developer.rhino3d.com/guides/grasshopper/gh-algorithms-and-data-structures/data-structures/)
- [Advanced Data Structures](https://developer.rhino3d.com/guides/grasshopper/gh-algorithms-and-data-structures/advanced-data-structures/)
- [The Why and How of Data Trees](https://developer.rhino3d.com/guides/grasshopper/the-why-and-how-of-data-trees/)
- [Grasshopper data namespace API](https://developer.rhino3d.com/api/grasshopper/html/N_Grasshopper_Kernel_Data.htm)

### 6.1 Use the right tree type

| Context | Preferred tree type | Item type |
| --- | --- | --- |
| Embedded C# Script component | `DataTree<T>` | Plain C#/RhinoCommon type such as `double`, `Point3d`, `Brep` |
| Compiled Grasshopper component | `GH_Structure<TGoo>` | Grasshopper wrapper implementing `IGH_Goo`, such as `GH_Number`, `GH_Point`, `GH_Brep` |

`DataTree<T>` was designed primarily to make trees easier in scripting components. In compiled SDK components, use `GH_Structure<T>` for full Grasshopper behavior and predictable `IGH_DataAccess` integration.

McNeel forum clarification:

- [DataTree or GH_Structure in Visual Studio](https://discourse.mcneel.com/t/datatree-t-or-gh-structure-t-in-visual-studio/106871)
- [DataTree vs IGH_Structure](https://discourse.mcneel.com/t/datatree-vs-igh-structure/67242)

Keep pure algorithm code independent of Grasshopper where practical. Convert between `GH_*` wrappers and RhinoCommon/plain C# objects at the component boundary. This makes the algorithm reusable and testable.

### 6.2 Embedded script tree pattern

```csharp
DataTree<Point3d> output = new DataTree<Point3d>();

for (int branchIndex = 0; branchIndex < input.BranchCount; branchIndex++)
{
    GH_Path path = input.Path(branchIndex);
    IList<Point3d> branch = input.Branch(branchIndex);

    output.EnsurePath(path); // Preserves an empty branch if required.

    for (int itemIndex = 0; itemIndex < branch.Count; itemIndex++)
    {
        output.Add(branch[itemIndex], path);
    }
}
```

### 6.3 Compiled component tree pattern

```csharp
GH_Structure<GH_Point> input;
if (!DA.GetDataTree(0, out input))
    return;

var output = new GH_Structure<GH_Point>();

for (int branchIndex = 0; branchIndex < input.PathCount; branchIndex++)
{
    GH_Path path = input.Path(branchIndex);
    var branch = input.Branches[branchIndex];

    output.EnsurePath(path);

    foreach (GH_Point point in branch)
    {
        if (point != null && point.IsValid)
            output.Append(new GH_Point(point.Value), path);
    }
}

DA.SetDataTree(0, output);
```

Use `SetDataTree`, not `SetData`, for a tree output. This specific distinction is a common source of output showing only a structure description. See [GH_Structure tree output from Visual Studio](https://discourse.mcneel.com/t/gh-structure-to-tree-output-from-visual-studio-c/61217).

### 6.4 Tree operations and invariants

Useful operations include:

- `EnsurePath(path)`: create a branch even when it has no items.
- `Add`/`AddRange`: scripting `DataTree<T>`.
- `Append`/`AppendRange`: compiled `GH_Structure<T>`.
- `Path(index)`/`Paths`: read paths.
- `Branch(index)`/`Branches`: read branches.
- `Flatten`, `Graft`, `Simplify`: topology-changing operations.
- `GH_Path.AppendElement(i)`: create a child path.

Preserve tree topology unless the component's contract explicitly says it changes topology. Do not silently flatten inputs to make implementation easier. Define:

- whether each input branch is processed independently;
- whether empty paths are preserved;
- whether output paths match input paths;
- whether filtered outputs are sparse or item-aligned;
- whether separate inputs require longest-list, shortest-list, or explicit branch matching.

For aligned diagnostics, prefer output trees with one item per input item. If an output is intentionally filtered, state that its item counts no longer align.

## 7. Grasshopper data types and Goo

Grasshopper transports data through types implementing `IGH_Goo`. Native wrappers include:

- `GH_Boolean`
- `GH_Integer`
- `GH_Number`
- `GH_String`
- `GH_Point`
- `GH_Curve`
- `GH_Brep`
- `GH_Mesh`
- `GH_ObjectWrapper`

Scripting components often expose the underlying C#/RhinoCommon values through type hints. Compiled tree inputs commonly expose the Goo wrappers.

Only create a custom Goo/parameter when a native type or `GH_ObjectWrapper` is insufficient and the data needs Grasshopper-specific behavior such as:

- validation and null state;
- conversion with `CastFrom`/`CastTo`;
- duplication;
- serialization;
- preview and baking;
- transformation and bounding boxes;
- persistent parameter storage.

References:

- [Grasshopper Data Types](https://developer.rhino3d.com/guides/grasshopper/grasshopper-data-types/)
- [Simple Data Types](https://developer.rhino3d.com/guides/grasshopper/simple-data-types/)
- [Simple Parameters](https://developer.rhino3d.com/guides/grasshopper/simple-parameters/)

## 8. RhinoCommon geometry workflow

RhinoCommon is the cross-platform .NET SDK used by Rhino plug-ins, Grasshopper components, and Grasshopper scripts.

Start with:

- [What is RhinoCommon?](https://developer.rhino3d.com/guides/rhinocommon/what-is-rhinocommon/)
- [RhinoCommon Guides](https://developer.rhino3d.com/guides/rhinocommon/)
- [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/)
- [RhinoCommon Geometry for Grasshopper C#](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/3-rhinocommon-geometry/)
- [Design Algorithms](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/4-design-algorithms/)

### 8.1 Know value types versus reference types

Common lightweight value types include:

- `Point3d`, `Vector3d`
- `Line`, `Plane`
- `Interval`, `Transform`
- `Circle`, `Arc`, `Ellipse`
- `BoundingBox`, `Box`
- `Sphere`, `Cylinder`, `Cone`, `Torus`

Common reference geometry types include:

- `Curve` and derived curve classes;
- `Surface` and `NurbsSurface`;
- `Brep`;
- `Mesh`;
- `SubD`;
- `PointCloud`.

Many reference geometry objects are mutable. Duplicate input geometry before modifying it unless mutation of the supplied object is explicitly safe and intended:

```csharp
Curve copy = curve?.DuplicateCurve();
Brep copy = brep?.DuplicateBrep();
Mesh copy = mesh?.DuplicateMesh();
```

### 8.2 Geometry method lookup

Search by type and operation:

| Need | Starting type/namespace |
| --- | --- |
| Closest point on curve | `Rhino.Geometry.Curve.ClosestPoint` |
| Closest point on Brep | `Rhino.Geometry.Brep.ClosestPoint` |
| Curve intersections | `Rhino.Geometry.Intersect.Intersection` |
| Brep/Brep intersection | `Intersection.BrepBrep` |
| Containment | `Curve.Contains`, `Brep.IsPointInside`, mesh point-in-solid APIs |
| Transform geometry | `Transform` plus geometry `Transform(...)` |
| Loft/sweep/network | `Brep.CreateFromLoft`, sweep classes, surface creation APIs |
| Mesh from Brep | `Mesh.CreateFromBrep` |
| Join curves/Breps | `Curve.JoinCurves`, `Brep.JoinBreps` |
| Offsets | type-specific curve/surface/Brep offset methods |

Read the exact return contract. A method returning `true` may mean the calculation completed, while empty output arrays may still mean there was no intersection. Validate both success and output content.

### 8.3 Tolerances

Geometry algorithms should accept an explicit tolerance when the user needs control. If a fallback is appropriate:

```csharp
double tolerance = requestedTolerance;
if (tolerance <= 0.0)
{
    tolerance = RhinoDocument != null
        ? RhinoDocument.ModelAbsoluteTolerance
        : 0.001;
}
```

In a compiled component use the active document only when the component contract permits document dependence:

```csharp
double tolerance =
    Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
```

Also consider document angle tolerance for angular operations. Never use exact floating-point equality for geometric decisions. Use distance, tolerance, or API-provided epsilon comparisons.

### 8.4 Validity checks

Before computation, check as appropriate:

- object is not `null`;
- value type reports `IsValid`;
- `GeometryBase.IsValid`;
- curve domain is valid;
- Brep/mesh is closed when a solid operation requires it;
- normal/vector is non-zero before unitizing;
- list has the minimum required count;
- API outputs are not null/empty.

Provide a runtime warning or error that identifies the failing input and branch/item when possible.

## 9. Component design and reliability

### 9.1 Separate adapter from algorithm

For anything beyond a small script:

1. Component/script layer reads Grasshopper data.
2. Validation layer normalizes inputs and reports errors.
3. Pure algorithm layer uses RhinoCommon and ordinary C# collections.
4. Adapter layer converts results into Grasshopper outputs/trees.

This separation makes migration from SDK-Mode to a `.gha` straightforward:

- `RunScript` becomes `SolveInstance`;
- component parameters move into registration methods;
- `DataTree<T>` adapters become `GH_Structure<GH_*>`;
- the core geometry algorithm remains largely unchanged.

### 9.2 Runtime messages

Use:

- `Remark`: useful non-problem information;
- `Warning`: recoverable issue or fallback;
- `Error`: computation cannot produce a valid result.

Messages must tell the user what failed and what to change. Avoid generic messages such as “Operation failed.”

### 9.3 Null, empty, and invalid input contract

Every component should define:

- missing required input behavior;
- null item behavior;
- empty list/tree behavior;
- empty branch preservation;
- invalid geometry behavior;
- zero/negative numeric behavior;
- failure output behavior.

Avoid leaving stale outputs after failure. Use local result variables and assign outputs only after the computation reaches a defined state.

### 9.4 Document access and side effects

Grasshopper computation should usually return geometry rather than directly mutate the Rhino document. Baking or document modification should be an explicit user action.

If document changes are required:

- do them on the UI thread;
- avoid repeated changes on every recompute;
- use a trigger and idempotent behavior;
- group undo records when appropriate;
- never modify the document from a worker thread.

The Rhino document and UI are not generally safe for arbitrary background access. Pure `Rhino.Geometry` calculations are better candidates for parallel work, but verify the specific APIs and avoid shared mutable geometry.

### 9.5 Expiration and scheduled solutions

Do not recursively call `SolveInstance` or create an infinite update loop. For external events and timed updates, use the owning `GH_Document.ScheduleSolution(...)`, then expire the relevant component with `ExpireSolution(false)` inside the scheduled callback.

McNeel forum reference: [Best way to retrigger SolveInstance](https://discourse.mcneel.com/t/best-way-to-retrigger-solveinstance/44332).

Remember to unsubscribe event handlers when the component/document is removed or disposed. Otherwise plug-ins can leak objects and keep receiving events.

## 10. Performance and concurrency

Optimize only after measuring.

First-line improvements:

- avoid repeating the same expensive geometry calculation;
- precompute and cache immutable lookup data during one solve;
- avoid nested full scans where spatial indexing can help;
- constrain searches with maximum distances/bounding boxes;
- allocate result lists with known capacity;
- avoid unnecessary geometry duplication and Goo conversion;
- avoid `ToList()` and LINQ allocations in hot loops when a simple loop is clearer;
- do no heavy work in viewport draw methods.

For long computations:

- keep the UI responsive only when the architecture safely supports it;
- isolate pure computation from `IGH_DataAccess`, RhinoDoc, and UI;
- provide cancellation where supported;
- return deterministic ordering even after parallel work.

Native task-capable components use `GH_TaskCapableComponent<T>`. Grasshopper may run a pre-solve pass to collect inputs/start tasks and a second pass to set outputs. `IGH_DataAccess` is not thread-safe and must not be accessed inside worker computation.

References:

- [Task Capable Components](https://developer.rhino3d.com/guides/grasshopper/programming-task-capable-component/)
- [Multi-threaded components](https://developer.rhino3d.com/guides/grasshopper/multi-treaded-components/)
- [Asynchronous Execution](https://developer.rhino3d.com/guides/scripting/advanced-async/)

## 11. UI, preview, options, and persistence

Use standard Grasshopper UI behavior unless custom interaction creates substantial value.

### 11.1 Icons and exposure

- Component icons are 24×24 pixels.
- Keep a readable border and strong contrast.
- Use `Exposure` to control placement within a category.

Reference: [Grasshopper Icons](https://developer.rhino3d.com/guides/grasshopper/grasshopper-icons/).

### 11.2 Component options

Right-click options are appropriate for secondary modes that should not occupy a normal input. If an option affects outputs:

- record an undo event;
- update the component message if useful;
- expire the solution;
- serialize the value.

Persistent component state uses Grasshopper `GH_IO` serialization by overriding `Write` and `Read`, and must call the base implementations.

Reference: [Custom Component Options](https://developer.rhino3d.com/guides/grasshopper/custom-component-options/).

### 11.3 Custom attributes

Custom attributes control layout, rendering, wires, tooltips, and mouse interaction. They add significant complexity and should be used only when ordinary components/parameters cannot express the interaction.

References:

- [Extending the GUI](https://developer.rhino3d.com/guides/grasshopper/extending-the-gui/)
- [Custom Attributes](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/)

## 12. Debugging workflow

### 12.1 Embedded C# script

1. Confirm input names, type hints, and access modes.
2. Confirm the `RunScript` signature.
3. Read compile/runtime messages on the component.
4. Add a breakpoint in the Rhino 8 script editor.
5. Use Variables, Watch, and Call Stack trays.
6. Inspect `Iteration`, branch paths, item counts, validity, and tolerance.
7. Use `Print`, `Console.WriteLine`, or `TopologyDescription` for temporary diagnostics.
8. Remove noisy diagnostics when done.

If the editor seems stale, save/recompute and use the component's cache-discard option where applicable.

### 12.2 Compiled `.gha`

1. Build the template once before modifying it.
2. Launch Rhino from Visual Studio using the template debug profile.
3. Open Grasshopper.
4. Place the component to load the assembly and call `SolveInstance`.
5. Break in parameter registration for startup issues or `SolveInstance` for computation.
6. Inspect input retrieval results and output assignment.
7. Stop Rhino before rebuilding if the assembly is locked.
8. Confirm the loaded `.gha` path/version when behavior does not match the source.

Keep debugging architecture generated by the current McNeel template rather than copying old launch settings from forum posts.

## 13. Verification and regression testing

Every change should be verified proportionally to risk.

### 13.1 Minimum behavior matrix

- valid normal input;
- missing/null inputs;
- empty list;
- empty tree;
- empty branch;
- invalid geometry;
- minimum and maximum numeric values;
- zero and negative counts/tolerances;
- one item and one branch;
- multiple branches with different item counts;
- grafted, simplified, and non-zero-based paths;
- geometry on, inside, and outside tolerance;
- document-unit/tolerance changes;
- large input for performance;
- save, close, reopen, and recompute.

### 13.2 Tree assertions

For every output tree verify:

- `PathCount`;
- paths and path ordering;
- item count per path;
- whether empty paths remain;
- item ordering;
- alignment with source items;
- behavior after filtering.

Keep a small `.gh` or `.ghx` regression definition for topology-sensitive behavior.

### 13.3 Pure algorithm tests

Place reusable geometry/selection logic in ordinary C# methods or a separate library so it can be tested without constructing a full Grasshopper document. Test the adapter separately in Rhino/Grasshopper.

For geometry results compare:

- validity;
- counts/topology;
- bounding boxes;
- lengths/areas/volumes within tolerance;
- closest distances within tolerance;
- stable ordering and branch mapping.

## 14. Build, installation, packaging, and release

### 14.1 Local development

Use the output location and debug setup generated by the current template. A `.gha` is a Grasshopper component library loaded by Grasshopper inside Rhino.

Before release:

- use Release configuration;
- confirm referenced assemblies are not accidentally copied when they should come from Rhino;
- confirm dependencies are present and compatible;
- confirm the component appears in the intended category;
- test on a clean Rhino profile or second machine when possible.

### 14.2 Yak packaging

Yak is Rhino's package manager and is the normal distribution path for compiled Grasshopper plug-ins.

Typical package workflow:

1. Stage the `.gha`, required dependencies, icon, README, and license.
2. Generate and edit `manifest.yml`.
3. Build the `.yak`.
4. Inspect its distribution tag and contents.
5. Install/test the local package.
6. Log in and push only after verification.

References:

- [Package Manager Guides](https://developer.rhino3d.com/guides/yak/)
- [Creating a Grasshopper Plug-In Package](https://developer.rhino3d.com/guides/yak/creating-a-grasshopper-plugin-package/)
- [Yak CLI Reference](https://developer.rhino3d.com/guides/yak/yak-cli-reference/)
- [Package Restore in Grasshopper](https://developer.rhino3d.com/guides/yak/package-restore-in-grasshopper/)

Use semantic versioning where practical. Do not reuse a released version number for different bits. Maintain assembly/component identity so package restore and existing definitions can find the plug-in.

## 15. Repository conventions

```text
Grasshopper_scripting/
├── docs/
│   ├── GRASSHOPPER_DEVELOPMENT_PIPELINE.md
│   └── RHINO_GRASSHOPPER_CSHARP_DEVELOPMENT_HANDBOOK.md
├── src/
│   ├── csharp/      # Paste-ready embedded SDK-Mode C# sources
│   ├── shared/      # Optional path-linked Script-Mode sources
│   ├── scripts/     # Standalone Rhino/ScriptEditor scripts
│   └── plugins/     # Compiled Visual Studio .gha/.rhp solutions
├── grasshopper/     # .gh/.ghx hosts and regression definitions
└── tests/           # Fixtures and pure C# tests
```

For each C# component source, document near the top or in an adjacent README:

- component display name;
- development mode;
- Rhino/Grasshopper version tested;
- input names, types, and access;
- output names and topology;
- tolerance and failure behavior;
- required third-party plug-ins/packages;
- matching `.gh` regression file.

## 16. Codex implementation checklist

When the user asks Codex to create or modify a Grasshopper C# tool:

1. Read this handbook and the existing component.
2. Confirm the intended development mode from context.
3. Default this repository's `src/csharp` work to complete paste-ready SDK-Mode.
4. Inspect the exact input/output contract.
5. Ask only for a missing detail that materially changes the algorithm.
6. Find the exact official API signatures for unfamiliar operations.
7. Separate tree adaptation from geometry logic.
8. Preserve paths and item ordering unless explicitly changing topology.
9. Validate nulls, empty branches, invalid geometry, and numeric limits.
10. Use explicit tolerance behavior.
11. Duplicate mutable reference geometry before modifying it.
12. Add clear Grasshopper runtime messages.
13. Avoid RhinoDoc/UI work on worker threads.
14. Verify all outputs and tree counts.
15. Record version-specific APIs and dependencies.
16. For a `.gha`, preserve released GUIDs and parameter compatibility.
17. Deliver the source file plus exact Grasshopper parameter setup instructions.

## 17. Topic-to-source lookup map

| Question | First source |
| --- | --- |
| How does the Rhino 8 C# Script component work? | [Grasshopper Scripting: C#](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/) |
| How do Item/List/Tree inputs behave? | [C# Component chapter](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/1-grasshopper-csharp-component/) |
| How do Grasshopper data trees work conceptually? | [Advanced Data Structures](https://developer.rhino3d.com/guides/grasshopper/gh-algorithms-and-data-structures/advanced-data-structures/) |
| What are the current tree API members? | [Grasshopper data API](https://developer.rhino3d.com/api/grasshopper/html/N_Grasshopper_Kernel_Data.htm) |
| `DataTree<T>` or `GH_Structure<T>`? | [McNeel Discourse explanation](https://discourse.mcneel.com/t/datatree-t-or-gh-structure-t-in-visual-studio/106871) |
| How do I build a native component? | [Your First Component](https://developer.rhino3d.com/guides/grasshopper/your-first-component-windows/) |
| What methods define `GH_Component`? | [Simple Component](https://developer.rhino3d.com/guides/grasshopper/simple-component/) |
| What target framework should I use? | [Moving to .NET Core](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/) |
| How do I find a Rhino geometry method? | [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/) |
| How do RhinoCommon geometry types behave? | [RhinoCommon Geometry chapter](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/3-rhinocommon-geometry/) |
| How do custom Grasshopper types work? | [Grasshopper Data Types](https://developer.rhino3d.com/guides/grasshopper/grasshopper-data-types/) |
| How do I add persistent right-click options? | [Custom Component Options](https://developer.rhino3d.com/guides/grasshopper/custom-component-options/) |
| How do I create custom canvas UI? | [Custom Attributes](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/) |
| How do I safely parallelize a component? | [Task Capable Components](https://developer.rhino3d.com/guides/grasshopper/programming-task-capable-component/) |
| How do I package a `.gha`? | [Creating a Grasshopper Plug-In Package](https://developer.rhino3d.com/guides/yak/creating-a-grasshopper-plugin-package/) |
| Where are official working samples? | [Rhino samples](https://developer.rhino3d.com/samples/) |
| Where should unresolved developer questions go? | [Grasshopper Developer forum](https://discourse.mcneel.com/c/grasshopper-developer/) |

## 18. Known version-sensitive areas

Re-research these before changing them:

- Rhino major-version and .NET target combinations;
- Visual Studio/VS Code template installation;
- ScriptEditor project publishing;
- shared/input script limitations;
- RhinoCommon methods marked `Available since` a later Rhino 8 service release;
- task-capable component patterns;
- cross-platform UI libraries (`System.Drawing`, WinForms, Eto);
- Yak distribution tags and package manifest rules;
- Rhino 9 and Grasshopper 2 APIs.

Grasshopper 1 guidance in this document must not be assumed to apply to Grasshopper 2.

