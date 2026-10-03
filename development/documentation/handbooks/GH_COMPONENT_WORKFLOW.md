# CasaCreta Grasshopper Component Workflow

Use this file as the CLI reference when creating or editing Grasshopper components in this project.

Global reusable pipeline diagram for future component development:

`CasaCreta/GH_COMPONENT_DEV_PIPELINE.svg`

The SVG is intentionally generic. It should be used for upcoming Grasshopper component development, not as a diagram of `CreateUVcurve.cs`.

## Project Package Map

`CasaCreta/CreateUVcurve.cs` uses these namespaces:

1. `System`
2. `System.Collections.Generic`
3. `Grasshopper.Kernel`
4. `Rhino.Geometry`

The project package references are defined in `CasaCreta/CasaCreta.csproj`.

Direct NuGet packages:

1. `Grasshopper` `8.0.23304.9001`
2. `Microsoft.NETFramework.ReferenceAssemblies.net48` `1.0.3`, only for non-Windows target condition
3. `System.Drawing.Common` `7.0.0`, only for non-Windows target condition

For normal component code, the important SDK package is `Grasshopper`. Rhino geometry types such as `Surface`, `Curve`, `Rectangle3d`, `Plane`, and `Interval` come from `Rhino.Geometry`, available through the Rhino/Grasshopper SDK references.

## Official McNeel Workflow Alignment

Reference checked: McNeel's official Windows guide, "Your First Component (Windows)".

The official workflow starts from the `Grasshopper Assembly for Rhino (C#)` Visual Studio template. This repository already has the same main anatomy:

| Official template concept | CasaCreta file or setting |
| --- | --- |
| Project file and dependencies | `CasaCreta/CasaCreta.csproj` |
| Multi-target framework setup | `TargetFrameworks` in `CasaCreta.csproj` |
| Grasshopper NuGet dependency | `PackageReference Include="Grasshopper"` |
| Assembly/plugin metadata file | `CasaCreta/CasaCretaInfo.cs` |
| Component implementation file | `CasaCreta/CreateUVcurve.cs`, `CasaCreta/CasaCretaComponent.cs` |
| Build output extension | `TargetExt` set to `.gha` |

Official-template item not currently present in this repo:

```text
CasaCreta/Properties/launchSettings.json
```

McNeel's Visual Studio template normally includes `launchSettings.json` for debug launch behavior. If CLI or Visual Studio debugging needs to start Rhino/Grasshopper automatically with F5, add or regenerate this file from the official Grasshopper Assembly template.

Official workflow checkpoints to preserve:

1. Build the boilerplate component before changing behavior.
2. Run/debug so Rhino starts and Grasshopper loads the `.gha`.
3. Place the component on the Grasshopper canvas from its category/subcategory.
4. Confirm the blank icon is acceptable while `Icon => null`.
5. Set breakpoints inside `SolveInstance` when debugging runtime behavior.
6. Remember `SolveInstance` runs when the component is placed and again whenever input values change.
7. Stop Rhino/Grasshopper to end the Visual Studio debug session.

## Component Class Pattern

Every component should inherit from `GH_Component`.

```csharp
public class CreateUVcurve : GH_Component
{
    public CreateUVcurve()
      : base(
          "CreateuvCurve",
          "UVCurve",
          "Generates U/V iso curves and UV rectangle",
          "CasaCreta",
          "Surface")
    { }
}
```

Constructor arguments control the Grasshopper node:

1. Component name: full display name in Grasshopper.
2. Nickname: short label shown on the component.
3. Description: tooltip/help text.
4. Category: tab name, here `CasaCreta`.
5. Subcategory: panel group, here `Surface`.

Each component must also provide:

```csharp
protected override System.Drawing.Bitmap Icon => null;

public override Guid ComponentGuid =>
    new Guid("PUT-UNIQUE-GUID-HERE");
```

Always generate a new GUID for a new component. Never reuse an existing component GUID.

## Param Manager Workflow

Grasshopper calls `RegisterInputParams` and `RegisterOutputParams` once when building the component definition. The `p` variable is the parameter manager.

Input manager type:

```csharp
protected override void RegisterInputParams(GH_InputParamManager p)
```

Output manager type:

```csharp
protected override void RegisterOutputParams(GH_OutputParamManager p)
```

Use the manager to add node sockets in exact order. The index used later by `DA.GetData`, `DA.GetDataList`, `DA.SetData`, `DA.SetDataList`, or `DA.SetDataTree` must match this registration order.

## Input Node Management

`CreateUVcurve.cs` registers two inputs:

```csharp
p.AddSurfaceParameter("Surface", "S", "Input surface", GH_ParamAccess.item);
p.AddIntegerParameter("Count", "C", "Division count", GH_ParamAccess.item, 50);
```

Input socket map:

| Index | Name | Nickname | Type | Access | Default |
| --- | --- | --- | --- | --- | --- |
| `0` | `Surface` | `S` | `Surface` | `GH_ParamAccess.item` | none |
| `1` | `Count` | `C` | `int` | `GH_ParamAccess.item` | `50` |

Use `GH_ParamAccess.item` when the input expects one value.

Use `GH_ParamAccess.list` when the input expects a flat list.

Use `GH_ParamAccess.tree` when the input expects branches and paths.

Default input values are part of node behavior. In `CreateUVcurve.cs`, `Count` defaults to `50` because the default value is the final argument:

```csharp
p.AddIntegerParameter("Count", "C", "Division count", GH_ParamAccess.item, 50);
```

When adding defaults, keep the default in `RegisterInputParams` aligned with the local fallback value in `SolveInstance`.

## Output Node Management

`CreateUVcurve.cs` registers three outputs:

```csharp
p.AddRectangleParameter("UV Rect", "R", "UV rectangle", GH_ParamAccess.item);
p.AddCurveParameter("U Curves", "U", "U direction curves", GH_ParamAccess.list);
p.AddCurveParameter("V Curves", "V", "V direction curves", GH_ParamAccess.list);
```

Output socket map:

| Index | Name | Nickname | Type | Access |
| --- | --- | --- | --- | --- |
| `0` | `UV Rect` | `R` | `Rectangle3d` | `GH_ParamAccess.item` |
| `1` | `U Curves` | `U` | `List<Curve>` | `GH_ParamAccess.list` |
| `2` | `V Curves` | `V` | `List<Curve>` | `GH_ParamAccess.list` |

Set outputs with matching data access calls:

```csharp
DA.SetData(0, rect);
DA.SetDataList(1, uCurves);
DA.SetDataList(2, vCurves);
```

## SolveInstance Workflow

Grasshopper calls `SolveInstance(IGH_DataAccess DA)` whenever the component must recompute.

It is called at least when the component is placed on the canvas and when connected input data changes. During debugging, this is the method where breakpoints show the actual runtime values coming from Grasshopper.

Recommended structure:

1. Declare local variables with defaults.
2. Read inputs by index.
3. Return early if required input is missing or invalid.
4. Clamp or validate numeric inputs.
5. Convert or prepare geometry.
6. Compute results.
7. Assign outputs by index.

`CreateUVcurve.cs` follows this pattern:

```csharp
Surface srf = null;
int count = 50;

if (!DA.GetData(0, ref srf)) return;
if (!DA.GetData(1, ref count)) return;
if (srf == null) return;

count = Math.Max(2, count);
```

Data access methods:

| Access | Read | Write |
| --- | --- | --- |
| item | `DA.GetData(index, ref value)` | `DA.SetData(index, value)` |
| list | `DA.GetDataList(index, list)` | `DA.SetDataList(index, list)` |
| tree | use `GH_Structure<T>` / tree APIs | `DA.SetDataTree(index, tree)` |

## Geometry Types In This Component

Input geometry:

```csharp
Surface srf
```

Generated geometry:

```csharp
List<Curve> uCurves
List<Curve> vCurves
Rectangle3d rect
```

The component reparameterizes the surface to normalized UV domains:

```csharp
srf.SetDomain(0, new Interval(0, 1));
srf.SetDomain(1, new Interval(0, 1));
```

Then it creates evenly spaced parameter values from `0.0` to `1.0`, inclusive:

```csharp
for (int i = 0; i < count; i++)
    parameters.Add(i / (double)(count - 1));
```

Iso-curve generation:

```csharp
uCurves.Add(srf.IsoCurve(0, t));
vCurves.Add(srf.IsoCurve(1, t));
```

Rectangle output:

```csharp
Rectangle3d rect = new Rectangle3d(
    Plane.WorldXY,
    new Interval(0, uMax),
    new Interval(0, vMax)
);
```

## CreateUVcurve Code Audit

Source checked: `CasaCreta/CreateUVcurve.cs`.

Component purpose:

1. Accept one Rhino `Surface`.
2. Accept an integer division `Count`.
3. Normalize the surface UV domains to `0..1`.
4. Generate `count` parameter values across the normalized domain.
5. Create U and V iso-curve lists from those parameters.
6. Measure the longest curve in each direction.
7. Create a world XY `Rectangle3d` sized from the maximum U and V curve lengths.
8. Output the rectangle, U curves, and V curves.

Actual source flow:

```text
RegisterInputParams
  input 0: Surface S, item
  input 1: Count C, item, default 50

RegisterOutputParams
  output 0: UV Rect R, item
  output 1: U Curves U, list
  output 2: V Curves V, list

SolveInstance
  DA.GetData(0, ref srf)
  DA.GetData(1, ref count)
  validate srf
  clamp count to at least 2
  SetDomain U and V to Interval(0, 1)
  build List<double> parameters
  build List<Curve> uCurves and vCurves
  calculate uMax and vMax from curve lengths
  create Rectangle3d on Plane.WorldXY
  DA.SetData(0, rect)
  DA.SetDataList(1, uCurves)
  DA.SetDataList(2, vCurves)
```

Important implementation detail:

`srf.SetDomain(...)` mutates the input surface object. If future components must preserve the original input domain, duplicate the surface before reparameterizing:

```csharp
Surface workingSurface = srf.DuplicateSurface();
workingSurface.SetDomain(0, new Interval(0, 1));
workingSurface.SetDomain(1, new Interval(0, 1));
```

Compile issue currently present:

```csharp
IGH_ActiveObject
```

This token appears inside `SolveInstance` after the length calculations. It is not a statement, declaration, or valid expression in that location. Remove it before build.

Recommended robustness improvements for future edits:

1. Remove invalid `IGH_ActiveObject` token.
2. Duplicate the surface before `SetDomain` if mutation is not intended.
3. Check `IsoCurve(...)` result for null before adding to output lists.
4. Check curve length only when the curve is non-null.
5. Keep input and output indexes synchronized with registration order.

## Data Tree Type Pattern

`CreateUVcurve.cs` does not output a data tree. It outputs one item and two flat lists.

For tree output, use the pattern already present in `CasaCretaComponent.cs`:

```csharp
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
```

```csharp
var tree = new GH_Structure<GH_Curve>();
GH_Path path = new GH_Path(zIndex, lobeIndex);
tree.Append(new GH_Curve(curve), path);
DA.SetDataTree(0, tree);
```

Common tree wrapper types:

| Rhino type | Grasshopper wrapper |
| --- | --- |
| `Curve` | `GH_Curve` |
| `Point3d` | `GH_Point` |
| `Brep` | `GH_Brep` |
| `Mesh` | `GH_Mesh` |
| `double` | `GH_Number` |
| `int` | `GH_Integer` |
| `string` | `GH_String` |

Use a data tree when branch identity matters, for example layers, groups, rows, or nested toolpaths. Use a list when only order matters.

## CLI Development Checklist

When adding a new component:

1. Create a new `.cs` file under `CasaCreta/`.
2. Add `using System;`, `using Grasshopper.Kernel;`, and the Rhino/Grasshopper namespaces needed by the component.
3. Create a public class inheriting `GH_Component`.
4. Fill the constructor metadata with correct category and subcategory.
5. Register inputs in `RegisterInputParams`.
6. Register outputs in `RegisterOutputParams`.
7. Implement `SolveInstance`.
8. Use `DA.GetData`, `DA.GetDataList`, or tree APIs according to `GH_ParamAccess`.
9. Validate null geometry, empty lists, tolerances, and numeric ranges before computing.
10. Use `DA.SetData`, `DA.SetDataList`, or `DA.SetDataTree` according to output access.
11. Add `Icon => null` unless a real bitmap icon exists.
12. Generate and assign a unique `ComponentGuid`.
13. Build the project and fix compile errors.
14. If debugging in Visual Studio, make sure Rhino/Grasshopper launch settings exist or start Rhino manually and load the built `.gha`.
15. Place the component on a Grasshopper canvas and verify `SolveInstance` runs on placement and input changes.

## Compile Warning From Current File

`CreateUVcurve.cs` currently contains this stray token inside `SolveInstance`:

```csharp
IGH_ActiveObject
```

This is not valid in the current location and should be removed before building.

## Minimal Component Template

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace CasaCreta
{
    public class MyComponent : GH_Component
    {
        public MyComponent()
          : base(
              "My Component",
              "MyComp",
              "Describe what the component computes.",
              "CasaCreta",
              "Surface")
        { }

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddSurfaceParameter("Surface", "S", "Input surface", GH_ParamAccess.item);
            p.AddIntegerParameter("Count", "C", "Division count", GH_ParamAccess.item, 50);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddCurveParameter("Curves", "C", "Generated curves", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Surface srf = null;
            int count = 50;

            if (!DA.GetData(0, ref srf)) return;
            if (!DA.GetData(1, ref count)) return;
            if (srf == null) return;

            count = Math.Max(2, count);

            var curves = new List<Curve>();

            for (int i = 0; i < count; i++)
            {
                double t = i / (double)(count - 1);
                curves.Add(srf.IsoCurve(0, t));
            }

            DA.SetDataList(0, curves);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("PUT-UNIQUE-GUID-HERE");
    }
}
```

## CLI Prompt Pattern

Use this when asking a CLI agent to create a component:

```text
Read CasaCreta/GH_COMPONENT_WORKFLOW.md first.
Open CasaCreta/GH_COMPONENT_DEV_PIPELINE.svg if a visual development pipeline is useful.
Create a Grasshopper component in CasaCreta using the existing GH_Component pattern.
Register inputs and outputs with GH_InputParamManager and GH_OutputParamManager.
Use GH_ParamAccess.item/list/tree correctly.
Use IGH_DataAccess indexes matching registration order.
Validate inputs before computing.
Use Rhino.Geometry types for geometry.
Generate a unique ComponentGuid.
Build the project and report compile errors.
```
