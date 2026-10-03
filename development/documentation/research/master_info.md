# G-Code Inside Grasshopper: Combined Research Notes

This file replaces the earlier broad research notes with the practical workflow from:

`C:\Users\MY PC\Downloads\NoteGPT_TRANSCRIPT_G-Code basics for 3d printing with Grasshopper  Tutorial.txt`

The transcript explains how to generate G-code directly inside Grasshopper without slicer software, scripts, or plugins. The central method is simple: design the printer path, convert it to ordered XYZ points, turn every point into a line of G-code text using `Concatenate`, add printer start/end protocols, and save the result as a `.gcode` file.

## Core Idea

Normal 3D printing workflow:

```text
Rhino / CAD model
-> export STL mesh
-> slicer software
-> start G-code + motion G-code + end G-code
-> printer firmware
-> machine movement
```

Grasshopper direct workflow from the tutorial:

```text
Rhino / Grasshopper geometry
-> curves or custom paths
-> ordered points
-> X, Y, Z coordinate lists
-> G-code text lines
-> start protocol + core motion + end protocol
-> .gcode file
-> printer firmware
```

The benefit is control. A slicer chooses layer paths, infill, travel moves, supports, and many process decisions automatically. In Grasshopper, the designer must create those paths manually, but can also create paths that common slicers cannot make, such as custom infill, non-planar toolpaths, surface-following isocurves, clay paths, aerial paths, and special continuous paths.

## What Kind of Printer This Applies To

The tutorial focuses on extrusion-based 3D printing:

- FDM / FFF desktop printers.
- Clay, concrete, chocolate, paste, or other nozzle extrusion systems.
- Any machine whose firmware accepts G-code-like movement commands.

It is not mainly about resin, binder jetting, powder sintering, or slicer-only workflows.

For a normal desktop printer, the firmware handles the machine kinematics. Grasshopper does not need to know whether the machine is Cartesian, delta, or another system. Grasshopper only writes coordinates and commands; the firmware translates those commands into motor movement.

## G-Code Structure

A G-code file has three main sections.

### 1. Start Protocol

The start protocol prepares the printer.

Typical responsibilities:

- Set units.
- Set absolute or relative positioning.
- Set absolute or relative extrusion.
- Home axes, commonly with `G28`.
- Heat nozzle and bed if printing plastic.
- Prime or prepare extrusion.
- Set fan, acceleration, or printer-specific settings.

The transcript recommends copying the start protocol from a known working G-code file generated for the same printer. This avoids guessing printer-specific setup commands.

### 2. Core Motion Code

This is the geometry-dependent part. It changes for every design.

The main command is a linear move:

```gcode
G1 F1800 X12.5 Y8.2 Z0.4 E3.2
```

Meaning:

- `G1`: linear move.
- `F1800`: feedrate/speed, usually in millimeters per minute.
- `X`, `Y`, `Z`: target coordinate.
- `E`: extrusion value.

If the command has no `E`, the machine moves without extrusion. That is a travel move.

Example travel-style line:

```gcode
G1 F3000 X40 Y20 Z1.2
```

Example deposition line:

```gcode
G1 F1800 X40 Y20 Z1.2 E6.35
```

### 3. End Protocol

The end protocol finishes the print.

Typical responsibilities:

- Stop extrusion.
- Turn off heaters.
- Turn off fan.
- Move bed or nozzle away.
- Home or park axes.
- Disable motors.
- Printer-specific beeps, lights, or shutdown actions.

The transcript again recommends copying this from a known working file for the target printer.

## Grasshopper Method: Step by Step

### Step 1. Work in Millimeters

Set Rhino units to millimeters. The tutorial warns that incorrect units can produce prints at the wrong scale, for example centimeters becoming millimeters.

### Step 2. Create or Import the Geometry

The transcript demonstrates a cylinder, but the method can start from many geometry types:

- Brep or surface.
- Mesh.
- Curves.
- Polylines.
- Points.
- Surface isocurves.
- Custom non-planar paths.

The geometry itself is not yet G-code. It must become an ordered list of points.

### Step 3. Turn Geometry Into Printable Curves

If starting from a Brep or surface:

```text
Brep / surface
-> Contour curves or isocurves
-> point lists
```

If starting from a mesh:

```text
Mesh
-> polylines / paths
-> point lists
```

If starting from curves:

```text
Curves
-> divide curves
-> point lists
```

For the cylinder example:

1. Create a `Cylinder`.
2. Use `Contour`.
3. The contour distance is the layer height.
4. Use `Divide Curve` on the contour curves.
5. Use enough points for the desired path resolution.

### Step 4. Decide the Layer Height

In a slicer, layer height is a setting. In this Grasshopper workflow, layer height is created by the geometry process itself.

For contour slicing:

```text
layer height = distance between contour sections
```

Example:

```text
Contour distance = 1 mm
-> layer height = 1 mm
```

For more normal plastic printing, a layer height might be around `0.1` to `0.3 mm`. For larger clay printing, the layer height may be much larger.

### Step 5. Create Ordered Point Lists

Use `Divide Curve` to create points on each contour or path.

Important Grasshopper issue:

- Contours produce separate branches in a DataTree.
- Each contour has its own list of points.
- If connected directly to `Polyline`, each layer stays separate.

To create one continuous printer path:

```text
Divide Curve output
-> Flatten Tree
-> Polyline
```

Flattening turns many branch lists into one ordered point list. The points themselves are not changed; only the data structure changes.

The resulting `Polyline` previews the actual path the printer will follow.

### Step 6. Understand Continuous Path Behavior

When the point list is flattened, the printer path becomes one continuous route:

```text
last point of layer 1
-> first point of layer 2
-> last point of layer 2
-> first point of layer 3
```

This can reduce stops, retractions, and restarts, improving print quality. However, the designer must check that the transition between paths makes physical sense.

For more advanced paths, the transcript mentions spiralized printing: a continuous spiral rather than separate layer contours. The tutorial does not build the full spiral method in detail, but identifies it as a better continuous approach.

### Step 7. Deconstruct Points Into Coordinates

Use `Deconstruct Point`.

Input:

```text
ordered points
```

Outputs:

```text
X coordinate list
Y coordinate list
Z coordinate list
```

These coordinate lists are the raw numeric material used to construct the G-code motion lines.

Important coordinate note:

- The `X`, `Y`, and `Z` values from `Deconstruct Point` are absolute coordinates in the Rhino/Grasshopper coordinate system.
- If a point is `(25, 10, 3)`, then `Deconstruct Point` outputs `X = 25`, `Y = 10`, `Z = 3`.
- The tutorial workflow therefore assumes normal absolute machine positioning for movement, usually set in the printer start protocol with `G90`.
- This is separate from extrusion mode. The `E` value may be absolute or relative depending on whether the start protocol uses `M82` or `M83`.

Example absolute movement line:

```gcode
G1 F1800 X25 Y10 Z3 E1.2
```

If relative XYZ movement were desired, the workflow would need to calculate coordinate differences between consecutive points and use relative positioning such as `G91`. That is not the method shown in the transcript.

### Step 8. Build G-Code Lines With Concatenate

The key Grasshopper component is `Concatenate`.

Location in Grasshopper:

```text
Sets -> Text -> Concatenate
```

The tutorial presents `Concatenate` as the simple core of the workflow. It joins text fragments and numeric values into full G-code lines.

The basic line pattern is:

```text
G1 F{feedrate} X{x} Y{y} Z{z} E{extrusion}
```

In Grasshopper, feed the `Concatenate` component with fragments like:

```text
"G1 F1800 "
"X"
X coordinates
" Y"
Y coordinates
" Z"
Z coordinates
" E"
E values
```

Spaces are important. Without spaces, the printer may receive invalid text such as:

```text
F1800X12.5
```

Correct:

```text
F1800 X12.5
```

The transcript notes that capital/lowercase letters generally do not matter, but uppercase is clearer.

### Step 9. Add Feedrate

Feedrate is the `F` value.

Example:

```gcode
G1 F1800 X10 Y20 Z0.4 E1
```

The tutorial uses `F1800` as an example. This may be too fast or too slow depending on the machine, material, nozzle, and print scale.

Feedrate can be:

- Constant for a simple test.
- Variable per segment for more advanced control.

### Step 10. Add Extrusion Values

The `E` value tells the extruder how much material to push.

The transcript explains the practical rule:

```text
extrusion should be related to segment length
```

For equal-length segments, a constant `E` increment may work as a rough test.

For unequal segments, calculate segment lengths:

```text
Polyline
-> Explode into line segments
-> Measure segment length
-> Convert length to E value using a calibration proportion
```

The `E` value depends on:

- Segment length.
- Nozzle diameter.
- Layer height.
- Material.
- Printer/extruder behavior.
- Whether the firmware expects absolute or relative extrusion.

### Step 11. Determine Absolute or Relative Extrusion

This is a critical printer-specific detail.

The transcript explains:

- `M82`: extruder uses absolute coordinates.
- `M83`: extruder uses relative coordinates.

Relative extrusion:

```text
segment E values are per segment
Example: 2, 4, 2, 1
```

Absolute extrusion:

```text
E value accumulates
Example: 2, 6, 8, 9
```

In Grasshopper:

- Relative mode: use segment extrusion values directly.
- Absolute mode: use cumulative addition, such as Grasshopper `Mass Addition`, to convert segment values into cumulative `E`.

The transcript recommends checking a known working G-code file from the printer to see whether it uses `M82` or `M83`.

### Step 12. Calibrate the E Proportion

A practical calibration method from the Q&A:

1. Open a known working G-code file for the target printer.
2. Read two consecutive movement lines with `E` values.
3. Identify the two XYZ points.
4. Measure the distance between those points.
5. Compare that distance to the change in `E`.
6. Use that proportion as a starting point in Grasshopper.

Example idea:

```text
segmentLength -> extrusionAmount
```

This is not universal. It changes with nozzle diameter, layer height, and material.

### Step 13. Design Travel Moves Manually

If the nozzle must move without printing, create a travel move by omitting `E`.

Travel moves matter when:

- The design has disconnected regions.
- The nozzle must jump from one island to another.
- A path should move through air before resuming deposition.

In this direct Grasshopper workflow, travel is not automatic. The designer must create it.

### Step 14. Design Infill, Supports, and Build Plate Contact Manually

A slicer normally creates:

- Infill.
- Supports.
- Brim/skirt/raft/build plate contact.
- Travel and retraction strategy.

In Grasshopper, these must be designed if needed.

The tutorial emphasizes this as both the difficulty and the power of the method. Custom infill can follow forces, structure, or design intent rather than default slicer grids.

Advice from the transcript:

- Try to design without supports when possible.
- Supports waste material and time.
- If you need infill, design the infill path explicitly.
- If you need build plate contact lines, design those lines explicitly.

### Step 15. Add Start, Core, and End Into One Text Output

The final G-code text is:

```text
start protocol
core motion lines
end protocol
```

In Grasshopper:

1. Put start protocol in a Panel.
2. Create the core `G1` lines with `Concatenate`.
3. Put end protocol in a Panel.
4. Combine them into one final Panel.

The transcript suggests copying start and end protocols from an existing working G-code file for the same printer.

### Step 16. Export the G-Code

Manual export method from the transcript:

1. Right-click the final Grasshopper Panel.
2. Choose `Copy Data Only`.
3. Paste into Notepad on Windows.
4. Save as all files.
5. Use a `.gcode` extension.

Example:

```text
trial.gcode
```

Then put the file on the SD card or send it to the printer according to the printer workflow.

## Example: Cylinder Workflow From Tutorial

```text
Set Rhino units to millimeters
-> Create Cylinder centered at 0,0
-> Radius example: 25 mm
-> Height example: 50 mm
-> Contour the cylinder
-> Contour distance = layer height
-> Divide each contour curve into points
-> Flatten point tree into one ordered list
-> Polyline preview of final printer path
-> Deconstruct points into X, Y, Z
-> Concatenate G1 lines with F, X, Y, Z, E
-> Add copied start protocol
-> Add copied end protocol
-> Copy Data Only
-> Save as .gcode
```

## Example: Surface Isocurve Workflow

The transcript also describes a non-planar method:

```text
Draw ellipse
-> Rebuild as degree 3 curve with control points
-> Copy / edit endpoints
-> Loft between curves to create surface
-> Extract isocurves from surface
-> Convert isocurves to points
-> Use same G-code generation method as before
```

This is valuable because slicers usually create planar layer paths. Isocurves can follow a surface and create non-planar paths.

## What Was Removed From Earlier Research

The previous research files included broader architecture concepts that are not part of this tutorial’s actual G-code writing method:

- Knowledge graph.
- Chinese Postman routing.
- SCARA-specific validation.
- Advanced clay process simulation.
- Constraint engines.
- Manufacturing operating system architecture.
- Large module layouts.

Those ideas may be useful for a future professional engine, but they are not the direct Grasshopper G-code tutorial process requested here. The current master file keeps the workflow close to the transcript.

## Correct Flow To Use In Diagram

```text
1. Set units and choose printer
2. Copy known start/end protocols
3. Create or import Grasshopper geometry
4. Convert geometry into curves/paths
5. Set layer height through contour/path spacing
6. Divide paths into ordered points
7. Flatten/order the point tree
8. Preview the resulting polyline path
9. Deconstruct points into X/Y/Z
10. Compute feedrate and E values
11. Build G1 text lines with Concatenate
12. Add travel moves where E is omitted
13. Combine start + core + end
14. Copy Data Only / save as .gcode
15. Test carefully on the real printer
```

## Safety Notes

- Always test with safe heights and slow speeds first.
- Use a known working start/end protocol for the target machine.
- Check whether extrusion is `M82` absolute or `M83` relative.
- Do not assume a random `E` value is safe.
- Watch the first print closely.
- Incorrect coordinates, units, temperatures, or extrusion values can crash the printer or clog the nozzle.
