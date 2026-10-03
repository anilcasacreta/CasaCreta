# Eto.Forms in C# .NET — Comprehensive Architecture & Implementation Guide for Rhino & Grasshopper

## 1. Executive Answer: C# .NET vs. Python for Eto.Forms

> **Key Fact**: **Eto.Forms is natively written in C# .NET!**  
> It was created by Curtis Wensley as an open-source cross-platform .NET UI framework. Python in Rhino only accesses Eto through IronPython or Python.NET wrappers.

### Why C# .NET is the Preferred & Native Environment for Eto:
| Feature | C# .NET | Python in Rhino |
| :--- | :--- | :--- |
| **Origin & Nature** | Native (.NET assembly `Eto.dll`) | Bound via Python.NET / IronPython |
| **Type Safety** | 100% compile-time checking | Runtime duck typing (errors only appear when clicked) |
| **IntelliSense** | Full Visual Studio / IDE autocomplete | Partial or dynamic |
| **Event Handling** | Clean delegates: `btn.Click += (s, e) => ...` | Python method binding / GIL overhead |
| **Data Binding** | Native two-way MVVM (`BindDataContext`) | Verbose manual property change notifications |
| **Grasshopper Integration** | Directly embedded in compiled `.gha` components | Requires external `.py` or Rhino 8 Python script block |
| **Dockable Panels** | Direct `Rhino.UI.Panels.RegisterPanel` registration | Complex interop registration |
| **Performance** | Zero overhead, compiled native .NET bytecode | Interpreter dispatch layer |

---

## 2. Eto.Forms Architecture in Rhino 8

Rhino 8 ships with `Eto.dll` and `Rhino.UI.dll` pre-installed:
- On **Windows**, Eto renders using **WPF** (Windows Presentation Foundation) by default, or WinForms.
- On **macOS**, Eto renders using native **Cocoa**.

Your single C# codebase automatically looks and behaves like a 100% native application on both Windows and Mac without writing platform-specific UI code.

### Namespace Hierarchy:
```csharp
using Eto.Forms;      // Form, Dialog, Button, TextBox, DropDown, DynamicLayout, etc.
using Eto.Drawing;    // Size, Point, Color, Font, Padding, Bitmap, etc.
using Rhino.UI;       // RhinoEtoApp, EtoExtensions, Panels, Dialogs
```

---

## 3. The Three UI Archetypes in Rhino / Grasshopper

### A. Modal Dialog (`Eto.Forms.Dialog<T>`)
- Blocks interaction with Rhino/Grasshopper until the user clicks **OK** or **Cancel**.
- Ideal for: **G-Code Export Settings**, **Material Configuration**, **Slicing Parameters**.
- Invocation: `dialog.ShowModal(RhinoEtoApp.MainWindow);`

### B. Modeless / Floating Form (`Eto.Forms.Form`)
- Floats above Rhino while allowing the user to continue orbiting, selecting geometry, or running Grasshopper.
- Ideal for: **Live Print Monitor**, **Jog Controller**, **Sensor Dashboard**.
- Invocation: `form.Owner = RhinoEtoApp.MainWindow; form.Show();`

### C. Rhino Dockable Panel (`Rhino.UI.Panels`)
- Docks alongside Layers, Properties, and Named Views in the Rhino sidebar.
- Registered via `Rhino.UI.Panels.RegisterPanel(plugin, typeof(MyPanel), "Title", icon);`

---

## 4. UI Layout Systems in Eto.Forms

Eto avoids hardcoded pixel coordinates so that UIs scale cleanly across High-DPI (4K) monitors.

### 1. `DynamicLayout` (McNeel Recommended)
Builds UIs row-by-row using a fluent, declarative builder:
```csharp
var layout = new DynamicLayout { Padding = new Padding(12), Spacing = new Size(8, 8) };

layout.BeginVertical();
layout.AddRow(new Label { Text = "Nozzle Size (mm):" }, new NumericStepper { Value = 6.0 });
layout.AddRow(new Label { Text = "Layer Height (mm):" }, new NumericStepper { Value = 1.3 });
layout.EndVertical();

layout.AddRow(null); // Expanding spacer (pushes buttons to bottom)
layout.AddRow(new Button { Text = "Cancel" }, new Button { Text = "Apply" });
```

### 2. `TableLayout`
Grid layout with rows, columns, and cell spanning.

### 3. `PixelLayout`
Absolute $(X, Y)$ coordinate positioning (rarely recommended except for canvas-like mini-editors).

---

## 5. Complete Production C# Example: G-Code Settings Modal Dialog

Here is a complete, production-ready Eto Dialog in C# that can be opened from any Grasshopper component:

```csharp
using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace CasaCreta.EtoForms
{
    public class GCodeSettingsModel
    {
        public double NozzleDiameter { get; set; } = 6.0;
        public double LayerHeight { get; set; } = 1.3;
        public double PrintSpeed { get; set; } = 17.0;
        public double TravelSpeed { get; set; } = 25.0;
        public double ExtrusionMultiplier { get; set; } = 1.0;
        public bool EnableFan { get; set; } = true;
    }

    public class GCodeSettingsDialog : Dialog<bool>
    {
        private readonly NumericStepper _nozzleInput;
        private readonly NumericStepper _layerHeightInput;
        private readonly NumericStepper _printSpeedInput;
        private readonly NumericStepper _travelSpeedInput;
        private readonly NumericStepper _flowInput;
        private readonly CheckBox _fanCheck;

        public GCodeSettingsModel Model { get; }

        public GCodeSettingsDialog(GCodeSettingsModel initialModel = null)
        {
            Model = initialModel ?? new GCodeSettingsModel();

            Title = "CasaCreta — Marlin G-Code Parameters";
            ClientSize = new Size(380, 320);
            MinimumSize = new Size(340, 280);
            Padding = new Padding(14);
            Resizable = false;

            // Controls
            _nozzleInput = new NumericStepper { Value = Model.NozzleDiameter, DecimalPlaces = 2, Increment = 0.5, MinValue = 0.2, MaxValue = 20.0 };
            _layerHeightInput = new NumericStepper { Value = Model.LayerHeight, DecimalPlaces = 2, Increment = 0.1, MinValue = 0.1, MaxValue = 10.0 };
            _printSpeedInput = new NumericStepper { Value = Model.PrintSpeed, DecimalPlaces = 1, Increment = 1.0, MinValue = 1.0, MaxValue = 300.0 };
            _travelSpeedInput = new NumericStepper { Value = Model.TravelSpeed, DecimalPlaces = 1, Increment = 5.0, MinValue = 5.0, MaxValue = 500.0 };
            _flowInput = new NumericStepper { Value = Model.ExtrusionMultiplier, DecimalPlaces = 2, Increment = 0.05, MinValue = 0.1, MaxValue = 5.0 };
            _fanCheck = new CheckBox { Text = "Enable Auxiliary Cooling / Relay (M106 S255)", Checked = Model.EnableFan };

            var btnCancel = new Button { Text = "Cancel" };
            btnCancel.Click += (s, e) => Close(false);

            var btnOk = new Button { Text = "Save Settings" };
            btnOk.Click += (s, e) =>
            {
                Model.NozzleDiameter = _nozzleInput.Value;
                Model.LayerHeight = _layerHeightInput.Value;
                Model.PrintSpeed = _printSpeedInput.Value;
                Model.TravelSpeed = _travelSpeedInput.Value;
                Model.ExtrusionMultiplier = _flowInput.Value;
                Model.EnableFan = _fanCheck.Checked ?? true;
                Close(true);
            };

            DefaultButton = btnOk;
            AbortButton = btnCancel;

            // Fluent Layout
            var layout = new DynamicLayout { Spacing = new Size(8, 8) };

            layout.BeginGroup("Tool & Kinematics", new Padding(8));
            layout.AddRow(new Label { Text = "Nozzle Bore (mm):" }, _nozzleInput);
            layout.AddRow(new Label { Text = "Layer Height (mm):" }, _layerHeightInput);
            layout.AddRow(new Label { Text = "Print Speed (mm/s):" }, _printSpeedInput);
            layout.AddRow(new Label { Text = "Travel Speed (mm/s):" }, _travelSpeedInput);
            layout.AddRow(new Label { Text = "Flow Multiplier:" }, _flowInput);
            layout.EndGroup();

            layout.AddRow(_fanCheck);
            layout.AddRow(null); // Spring spacer

            layout.AddRow(null, btnCancel, btnOk);

            Content = layout;
        }
    }
}
```

---

## 6. How to Trigger Eto Forms from a Grasshopper Component

To give your Grasshopper component a custom right-click context menu item or double-click dialog:

```csharp
// Inside your GH_Component subclass:
protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
{
    base.AppendAdditionalComponentMenuItems(menu);

    Menu_AppendItem(menu, "Configure G-Code Settings (Eto UI)...", (sender, e) =>
    {
        var dlg = new GCodeSettingsDialog(_currentSettings);
        bool? result = dlg.ShowModal(RhinoEtoApp.MainWindow);
        
        if (result == true)
        {
            // Update settings and trigger component recalculation
            ExpireSolution(true);
        }
    });
}
```
