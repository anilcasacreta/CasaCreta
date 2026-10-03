---
title: "Marlin-Flavour G-code Syntax Reference"
subtitle: "For the Casa Creta Grasshopper Curve-to-G-code Generator"
author: "Casa Creta - G_code_engine"
date: "2026-08-14"
papersize: a4
geometry: margin=16mm
fontsize: 9.5pt
header-includes:
  - |
    <style>
      @page { size: A4; margin: 16mm 15mm 18mm 15mm; }
      body { font-family: Arial, Helvetica, sans-serif; font-size: 9.5pt; line-height: 1.28; color: #172033; }
      h1 { font-size: 20pt; color: #0f4f59; border-bottom: 2px solid #0f766e; padding-bottom: 5px; }
      h2 { font-size: 14pt; color: #0f4f59; page-break-after: avoid; }
      h3 { font-size: 11pt; color: #334155; page-break-after: avoid; }
      table { width: 100%; border-collapse: collapse; font-size: 8.4pt; }
      th, td { border-bottom: 1px solid #cbd5e1; padding: 4px 5px; vertical-align: top; }
      th { background: #e6f4f1; color: #123; text-align: left; }
      pre { background: #f3f6f8; border-left: 3px solid #0f766e; padding: 7px; white-space: pre-wrap; }
      code { font-family: Consolas, "Courier New", monospace; }
      blockquote { border-left: 3px solid #ea580c; margin-left: 0; padding-left: 9px; color: #475569; }
      .page-break { page-break-before: always; }
    </style>
---

# Marlin-Flavour G-code Syntax Reference

**Purpose:** practical command reference for a C#.NET Grasshopper component that converts ordered RhinoCommon curves into Marlin-compatible G-code.

**Scope:** commands needed to generate, validate, preview, and safely export a normal motion/extrusion job. Marlin contains many additional calibration, networking, display, laser, CNC, and board-specific commands; consult the [official Marlin G-code index](https://marlinfw.org/meta/gcode/) for the complete firmware catalogue.

> **Safety:** Never assume that a command supported by one Marlin printer is enabled on another. Start/end code, temperature limits, homing, bed levelling, cold-extrusion behaviour, tool selection, motion limits, and parking coordinates must come from a tested `MachineProfile` for the exact printer.

## 1. G-code line syntax

General form:

```gcode
G1 X120.500 Y45.250 Z8.000 E14.627 F1800 ; deposition move
```

| Element | Meaning | Generator rule |
|---|---|---|
| `G` | Preparatory/motion command | Use for moves, units, coordinates, homing and dwell. |
| `M` | Machine/firmware command | Use for extrusion modes, heaters, fans, limits and status. |
| `T` | Tool/extruder selection | Emit only for a verified multi-tool machine profile. |
| `X Y Z` | Axis destination | Millimetres when `G21` is active. Absolute under `G90`; relative under `G91`. |
| `E` | Extruder-axis destination or increment | Absolute under `M82`; relative under `M83`. |
| `F` | Feedrate | Current units per minute; normally mm/min. It remains active until changed. |
| `S` | Common value parameter | Meaning depends on the command: temperature, fan PWM, percentage, etc. |
| `P` | Common secondary parameter | Often time, index or a command-specific value. |
| `I J K` | Arc-centre offsets / command-specific parameters | For `G2/G3`, normally arc-centre offsets from the start point. |
| `R` | Radius or wait-both-directions temperature | Meaning depends on the command. Never interpret without command context. |
| `;` | Comment start | Everything after `;` is a human-readable comment. |
| `N...*...` | Optional line number and checksum | Mainly for host streaming protocols; not normally required in an SD-card file. |

### Formatting rules for the C# postprocessor

- Emit one command per line using `\n` or the selected output newline policy.
- Use `CultureInfo.InvariantCulture`; decimal separators must be `.`.
- Never emit `NaN`, infinity, empty coordinates, or locale-formatted numbers.
- Use consistent precision, commonly 3 decimals for XYZ and 4-5 for E.
- Remove unnecessary trailing zeros only if the output remains unambiguous.
- Do not repeat modal values unless readability or the machine profile requires it.
- Treat feedrate as state: `G1 F1800` affects following moves until another `F` is supplied.
- Keep start protocol, generated motion, and end protocol as separate blocks internally.

Example C# formatter:

```csharp
using System.Globalization;

static string Mm(double value, int decimals = 3) =>
    value.ToString($"0.{new string('#', decimals)}", CultureInfo.InvariantCulture);

string line = $"G1 X{Mm(x)} Y{Mm(y)} Z{Mm(z)} E{Mm(e, 5)} F{feedMmPerMin}";
```

## 2. Modal machine state

The generator must explicitly establish these modes near the beginning of every file.

| State | Recommended declaration | Meaning |
|---|---|---|
| Units | `G21` | Millimetres. Do not rely on firmware startup state. |
| XYZ positioning | `G90` | Absolute X/Y/Z positions from Rhino/Grasshopper coordinates. |
| Extrusion positioning | `M83` or `M82` | Relative or absolute E; choose exactly one through `MachineProfile`. |
| Extruder origin | `G92 E0` | Reset logical E position, mainly useful with absolute extrusion. |
| Active tool | `T0` if required | Select first tool on verified multi-tool machines. |
| Levelling | `M420 S1` or machine-specific `G29` | Restore or generate bed compensation after homing, when supported. |

Important interactions:

- `G90/G91` controls XYZ and may also affect E unless `M82/M83` explicitly overrides extrusion mode.
- Always emit `M82` or `M83` after choosing `G90/G91` so extrusion behaviour is unambiguous.
- `G28` may disable bed levelling. Restore it after homing if the printer requires `M420 S1`.
- `G92` changes the logical position without moving the machine.

## 3. Core commands the generator must understand

### 3.1 Motion and coordinate commands

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`G0`](https://marlinfw.org/docs/gcode/G000-G001.html) | `G0 X100 Y50 Z5 F4200` | Linear non-deposition/rapid move. Marlin generally queues it like `G1`; on SCARA it may have different behaviour. | Travel, approach, Z-hop and parking moves. Omit `E`. |
| [`G1`](https://marlinfw.org/docs/gcode/G000-G001.html) | `G1 X100 Y50 Z2 E0.842 F1800` | Coordinated linear move. `E` synchronises extrusion with XYZ movement. | Primary deposition command. Also used for controlled retract/prime moves. |
| [`G2`](https://marlinfw.org/docs/gcode/G002-G003.html) | `G2 X50 Y20 I10 J0 E1.25 F1800` | Clockwise arc/circle move when `ARC_SUPPORT` is enabled. | Optional arc postprocessor. Validate plane and firmware support. |
| [`G3`](https://marlinfw.org/docs/gcode/G002-G003.html) | `G3 X50 Y20 I10 J0 E1.25 F1800` | Counter-clockwise arc/circle move when `ARC_SUPPORT` is enabled. | Optional arc postprocessor. Fall back to sampled `G1` segments. |
| [`G4`](https://marlinfw.org/docs/gcode/G004.html) | `G4 P500` or `G4 S1` | Dwell. `P` is milliseconds; `S` is seconds. | Process pause, pressure stabilisation or timed wait. |
| [`G21`](https://marlinfw.org/docs/gcode/G021.html) | `G21` | Select millimetres. | Required near file start. |
| [`G28`](https://marlinfw.org/docs/gcode/G028.html) | `G28` / `G28 X Y` | Home all or selected axes. Establishes a trusted machine position. | Start protocol only, using machine-approved axes/order. |
| [`G29`](https://marlinfw.org/docs/gcode/G029.html) | Variant-specific | Probe or operate bed levelling. Parameters vary by configured levelling system. | Never invent generic parameters. Copy verified printer start code. |
| [`G90`](https://marlinfw.org/docs/gcode/G090.html) | `G90` | Absolute positioning. | Recommended for curve coordinates. |
| [`G91`](https://marlinfw.org/docs/gcode/G091.html) | `G91` | Relative positioning. | Useful temporarily for safe Z-lift/retraction sequences. Restore `G90`. |
| [`G92`](https://marlinfw.org/docs/gcode/G092.html) | `G92 E0` | Set the current logical coordinate without moving. | Reset E or establish a deliberate work coordinate. |

### 3.2 Extrusion mode and flow commands

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`M82`](https://marlinfw.org/docs/gcode/M082.html) | `M82` | Absolute extruder positioning. Every E value is cumulative. | `E[n] = E[n-1] + segmentExtrusion`. Periodically reset using `G92 E0` if required. |
| [`M83`](https://marlinfw.org/docs/gcode/M083.html) | `M83` | Relative extruder positioning. Every E value applies to the current move. | Simplest mode for a custom curve generator. `E = segmentExtrusion`. |
| [`M200`](https://marlinfw.org/docs/gcode/M200.html) | `M200 D1.75` / `M200 D0` | Enable volumetric extrusion by filament diameter, or disable with `D0`. | Use only if the machine profile explicitly expects volumetric E. |
| [`M221`](https://marlinfw.org/docs/gcode/M221.html) | `M221 S100` | Global extrusion-flow percentage applied to E moves. | Optional process override. Prefer calculating correct E directly. |
| [`G10`](https://marlinfw.org/docs/gcode/G010.html) | `G10` | Firmware retract when `FWRETRACT` is enabled. | Optional alternative to explicit negative-E moves. |
| [`G11`](https://marlinfw.org/docs/gcode/G011.html) | `G11` | Firmware recover/unretract. | Pair with `G10`; requires verified firmware setup. |
| [`M207`](https://marlinfw.org/docs/gcode/M207.html) | Profile-specific | Configure firmware retract distance and speed. | Configuration/profile command, not normally repeated in every file. |
| [`M208`](https://marlinfw.org/docs/gcode/M208.html) | Profile-specific | Configure firmware recover values. | Configuration/profile command, not normally repeated in every file. |
| [`M302`](https://marlinfw.org/docs/gcode/M302.html) | `M302 S0` | Allows cold E movement by changing/disabling minimum extrusion temperature. | Clay/paste only when explicitly approved. Dangerous in an FDM profile. |

Filament-extrusion starting formula:

```text
depositedVolume = segmentLength * beadWidth * layerHeight
filamentArea     = pi * filamentDiameter^2 / 4
segmentE         = depositedVolume / filamentArea * flowMultiplier
```

For clay, paste, concrete, chocolate, syringe or pump extrusion, use a process-specific `IExtrusionModel`. Do not apply the filament-area formula unless E genuinely represents incoming filament length.

### 3.3 Temperature commands

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`M104`](https://marlinfw.org/docs/gcode/M104.html) | `M104 S200` | Set hotend target and continue without waiting. | Begin nozzle heating in parallel with other safe setup actions. |
| [`M109`](https://marlinfw.org/docs/gcode/M109.html) | `M109 S200` | Set hotend target and wait when heating. `R` waits for heating or cooling. | Required before FDM extrusion unless validated otherwise. |
| [`M105`](https://marlinfw.org/docs/gcode/M105.html) | `M105` | Report current and target temperatures. | Host diagnostics; generally unnecessary inside SD-card G-code. |
| [`M140`](https://marlinfw.org/docs/gcode/M140.html) | `M140 S60` | Set bed target and continue without waiting. | Begin bed heating early. |
| [`M190`](https://marlinfw.org/docs/gcode/M190.html) | `M190 S60` | Set bed target and wait when heating. `R` waits for heating or cooling. | FDM start protocol when a heated bed is used. |
| `M104 S0` | `M104 S0` | Set active hotend target to zero. | End protocol heater shutdown. |
| `M140 S0` | `M140 S0` | Set bed target to zero. | End protocol bed shutdown. |

`M109` and `M190` can block command processing while waiting. Hosts can only interrupt reliably when the firmware is configured appropriately. Do not emit temperatures outside the `MachineProfile` limits.

### 3.4 Fan commands

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`M106`](https://marlinfw.org/docs/gcode/M106.html) | `M106 S128` | Set default print-fan PWM from 0 to 255. `P` selects a fan index. | Material/layer-dependent cooling. `S255` is full duty. |
| [`M107`](https://marlinfw.org/docs/gcode/M107.html) | `M107` | Turn off the selected/default fan. | Start and end protocol, or process-specific cooling changes. |

### 3.5 Motion limits and overrides

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`M201`](https://marlinfw.org/docs/gcode/M201.html) | Profile-specific | Set axis acceleration limits. | Machine configuration; avoid overriding without approval. |
| [`M203`](https://marlinfw.org/docs/gcode/M203.html) | Profile-specific | Set maximum feedrates by axis. | Machine configuration; validate against it rather than rewriting it. |
| [`M204`](https://marlinfw.org/docs/gcode/M204.html) | `M204 P800 R1200 T1500` | Set printing, retract and travel acceleration. | Optional job-level tuning from a verified profile. |
| [`M205`](https://marlinfw.org/docs/gcode/M205.html) | Profile-specific | Advanced motion settings: junction deviation or classic jerk, minimum rates and segment time. | Firmware/configuration-dependent. Do not emit generic values. |
| [`M220`](https://marlinfw.org/docs/gcode/M220.html) | `M220 S100` | Global feedrate percentage applied to G-code moves. | Optional reset to 100% at start; machine policy decides. |
| [`M221`](https://marlinfw.org/docs/gcode/M221.html) | `M221 S100` | Global E-flow percentage. | Optional reset to 100% at start. |
| [`M400`](https://marlinfw.org/docs/gcode/M400.html) | `M400` | Wait until every queued move is complete. | Before shutdown, tool change, beep, pressure stop or final state change. |

### 3.6 Bed levelling and coordinate compensation

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`M420`](https://marlinfw.org/docs/gcode/M420.html) | `M420 S1` | Enable previously stored bed-levelling compensation when supported. | Usually after `G28`; exact behaviour depends on configuration. |
| [`G29`](https://marlinfw.org/docs/gcode/G029.html) | Printer-specific | Probe, generate, load or manipulate a levelling mesh depending on the configured system. | Copy known working start code. Do not guess. |
| [`M851`](https://marlinfw.org/docs/gcode/M851.html) | Profile-specific | Set probe offset. | Calibration only; never generated per job. |
| [`G53`](https://marlinfw.org/docs/gcode/G053.html) | `G53 G0 X0 Y0` | Perform a move in native machine coordinates when enabled. | Advanced parking only with verified firmware/profile support. |

### 3.7 Tool, steppers, job state and messages

| Command | Typical syntax | Description | Generator use |
|---|---|---|---|
| [`T0`-`T7`](https://marlinfw.org/docs/gcode/T.html) | `T0` | Select active tool/extruder. | Multi-tool profile only. Tool changes need associated temperature, offset, purge and travel logic. |
| [`M17`](https://marlinfw.org/docs/gcode/M017.html) | `M17` | Enable stepper motors. | Usually unnecessary because moves enable them; optional start code. |
| [`M18` / `M84`](https://marlinfw.org/docs/gcode/M018.html) | `M84` | Disable steppers, optionally after a timeout or by axis. | Common final command after parking. Position becomes untrusted. |
| [`M75`](https://marlinfw.org/docs/gcode/M075.html) | `M75` | Start the print-job timer. | Optional start protocol. |
| [`M77`](https://marlinfw.org/docs/gcode/M077.html) | `M77` | Stop the print-job timer. | Optional end protocol. |
| [`M73`](https://marlinfw.org/docs/gcode/M073.html) | `M73 P50` | Set print progress when enabled. | Optional periodic generator output. |
| [`M117`](https://marlinfw.org/docs/gcode/M117.html) | `M117 Printing` | Display a message on the printer LCD. | Optional start/end status. |
| [`M118`](https://marlinfw.org/docs/gcode/M118.html) | `M118 message` | Send text to the host/serial interface. | Optional host diagnostics. |

## 4. Diagnostic and safety commands

These commands are useful during commissioning, but most should not appear automatically in every generated file.

| Command | Description | Policy |
|---|---|---|
| [`M114`](https://marlinfw.org/docs/gcode/M114.html) | Report the current tool position. | Manual test/host diagnostic. |
| [`M115`](https://marlinfw.org/docs/gcode/M115.html) | Report firmware name, version and capabilities. | Use when building/detecting a machine profile. |
| [`M119`](https://marlinfw.org/docs/gcode/M119.html) | Report endstop states. | Commissioning and homing diagnosis. |
| [`M503`](https://marlinfw.org/docs/gcode/M503.html) | Report active settings. | Capture limits/settings for a machine profile. |
| [`M500`](https://marlinfw.org/docs/gcode/M500.html) | Save current settings to EEPROM. | **Do not emit automatically.** It permanently changes printer configuration. |
| [`M501`](https://marlinfw.org/docs/gcode/M501.html) | Reload settings from EEPROM. | Maintenance/profile operation only. |
| [`M502`](https://marlinfw.org/docs/gcode/M502.html) | Load firmware defaults into RAM. | **Do not emit automatically.** May discard expected tuning until settings are restored. |
| [`M0` / `M1`](https://marlinfw.org/docs/gcode/M000-M001.html) | Stop and wait for user interaction. | Optional deliberate operator checkpoint. |
| [`M108`](https://marlinfw.org/docs/gcode/M108.html) | Break out of supported waiting loops. | Host emergency-parser operation, not normal file output. |
| [`M112`](https://marlinfw.org/docs/gcode/M112.html) | Emergency shutdown and halt. | Emergency host/operator action. Not a normal end command. |
| [`M410`](https://marlinfw.org/docs/gcode/M410.html) | Quick-stop planner motion. | Emergency/host action. Machine position may become invalid. |
| [`M999`](https://marlinfw.org/docs/gcode/M999.html) | Restart from a stopped state. | Manual recovery only after the cause is understood. |

## 5. Common generated move patterns

### Absolute XYZ with relative extrusion (`G90` + `M83`)

```gcode
G21                         ; millimetres
G90                         ; absolute XYZ
M83                         ; relative E
G0 X20.000 Y20.000 Z0.300 F4200
G1 X60.000 Y20.000 Z0.300 E1.28450 F1800
G1 X60.000 Y60.000 Z0.300 E1.28450
```

### Travel, retract, Z-hop, approach and recover

```gcode
G1 E-1.00000 F1800          ; relative retract under M83
G0 Z1.300 F1200             ; raise from printing Z=0.300
G0 X100.000 Y80.000 F4200   ; travel without E
G0 Z0.300 F1200             ; return to printing height
G1 E1.00000 F1800           ; recover
```

### Absolute extrusion (`M82`)

```gcode
M82
G92 E0
G1 X20.000 Y20.000 Z0.300 E0.50000 F1800
G1 X40.000 Y20.000 Z0.300 E1.14225
G1 X60.000 Y20.000 Z0.300 E1.78450
```

When using `M82`, each value is the cumulative E coordinate. Never output a per-segment E amount directly.

### Arc deposition when supported

```gcode
G17                         ; XY plane, if explicit plane selection is enabled/needed
G2 X80.000 Y60.000 I20.000 J0.000 E2.10000 F1800
```

Arc emission requires:

- verified `ARC_SUPPORT` in Marlin;
- planar arc geometry;
- correct clockwise/counter-clockwise direction;
- correct centre offsets or radius mode;
- full-circle and tolerance handling;
- linear `G1` fallback when any requirement is not satisfied.

## 6. FDM start protocol template

The values below are examples only. Replace temperatures, levelling code, purge path, feeds and coordinates with tested values for the exact printer.

```gcode
; ===== CASA CRETA START - EXAMPLE FDM PROFILE =====
M117 Preparing
G21                         ; millimetres
G90                         ; absolute XYZ
M83                         ; relative extrusion
M107                        ; print fan off
M220 S100                   ; feed override 100% - optional policy
M221 S100                   ; flow override 100% - optional policy

M140 S60                    ; begin bed heating, no wait
M104 S200                   ; begin nozzle heating, no wait
G28                         ; home approved axes
M420 S1                     ; restore stored levelling if supported
M190 S60                    ; wait for bed heating
M109 S200                   ; wait for nozzle heating

G92 E0
G0 Z5.000 F1200
G0 X10.000 Y10.000 F4200
; Add a printer-approved purge/prime sequence here.
M75                         ; optional print timer
M117 Printing
; ===== GENERATED MOTION FOLLOWS =====
```

Alternative levelling workflows may use `G29` instead of `M420 S1`. Use only the printer's known working sequence.

## 7. Clay/paste start protocol template

This assumes the material system maps deposition to Marlin's E axis. Pump, relay, pressure or custom-controller systems may need different commands.

```gcode
; ===== CASA CRETA START - EXAMPLE CLAY/PASTE PROFILE =====
M117 Preparing clay job
G21                         ; millimetres
G90                         ; absolute XYZ
M83                         ; relative process/extrusion value
M107                        ; fan off unless process requires it
G28                         ; machine-approved homing procedure
M302 S0                     ; allow cold E motion - PROFILE MUST APPROVE
G92 E0
G0 Z10.000 F1200
; Add machine-approved pump/nozzle preparation here.
M75
M117 Printing
; ===== GENERATED MOTION FOLLOWS =====
```

> Never place `M302 S0` in a generic FDM profile. Cold extrusion can strip filament, damage an extruder, or create unsafe unexpected motion. It belongs only in an explicitly approved non-thermal extrusion profile.

## 8. End protocol templates

### FDM with relative E (`M83`)

```gcode
; ===== CASA CRETA END - EXAMPLE FDM PROFILE =====
M400                        ; complete queued motion
G1 E-2.00000 F1800          ; retract
G91
G0 Z10.000 F1200            ; relative Z lift
G90
G0 X10.000 Y200.000 F4200   ; approved park coordinate
M107                        ; fan off
M104 S0                     ; hotend off
M140 S0                     ; bed off
M77                         ; optional stop timer
M117 Complete
M84                         ; disable steppers after parking
```

### Clay/paste end

```gcode
; ===== CASA CRETA END - EXAMPLE CLAY/PASTE PROFILE =====
M400                        ; finish all deposition moves
G1 E-1.00000 F600           ; profile-defined pressure relief
G91
G0 Z20.000 F1200            ; safe relative lift
G90
G0 X10.000 Y200.000 F3000   ; approved park coordinate
M77
M117 Complete
M84
```

Do not copy example park coordinates to a real machine without confirming the build envelope, origin, kinematics and already-printed geometry.

## 9. Required generator validation

Before serialization, reject or warn on:

- invalid, null or zero-length curves;
- `NaN`, infinity or values outside numeric precision policy;
- incorrect Rhino-to-millimetre scale conversion;
- XYZ coordinates outside the machine envelope;
- Z below the safe minimum or above the machine maximum;
- deposition before homing/position establishment in the chosen protocol;
- extrusion during a travel transition;
- negative absolute E values under `M82`, unless deliberately supported;
- mismatched `M82/M83` calculation mode;
- feedrates above axis/process limits;
- extrusion/flow above the process limit;
- temperatures outside the machine/material profile;
- unsupported `G2/G3`, `G10/G11`, `G29`, `M420`, tool or feature commands;
- a missing start or end protocol;
- unsafe first move to the first curve;
- unapproved cold extrusion;
- shutdown before `M400` when queued motion must finish.

## 10. Command emission policy

### Always emit or establish

```text
G21
G90 or deliberately selected XYZ mode
M82 or M83
Known machine start protocol
Generated G0/G1 motion
Known machine end protocol
```

### Emit only when enabled by `MachineProfile`

```text
G2/G3 arcs
G10/G11 firmware retract
G29 / M420 bed levelling
M200 volumetric extrusion
M204/M205 motion overrides
M302 cold extrusion
Tn tool selection/change
G53 or workspace coordinate systems
M73/M75/M77 job reporting
```

### Never emit automatically from the curve compiler

```text
M500 EEPROM save
M502 factory defaults
M851 probe calibration changes
M92 steps-per-unit changes
M201/M203 machine-limit changes unless explicitly approved
M112 emergency stop as a normal end command
M999 restart
Firmware update, pin-control or board-debug commands
```

## 11. Minimal C# postprocessor state

The serializer should track at least:

```text
UnitsMode                 Millimetres / Inches
XyzPositionMode           Absolute / Relative
ExtrusionMode             Absolute / Relative
CurrentX, CurrentY, CurrentZ
CurrentE
CurrentFeedrate
ActiveTool
FanState
HotendTarget
BedTarget
LevellingState            Unknown / Enabled / Disabled
SteppersTrusted           Unknown / Homed / Untrusted
```

Recommended internal pipeline:

```text
RhinoCommon Curve
  -> ToolpathPath
  -> PlannedMove
  -> validated MotionProgram
  -> MarlinPostProcessor
  -> GCodeBlock list
  -> final text
```

Do not build G-code by concatenating strings directly inside the curve-sampling loop. First create typed moves, validate them, and only then serialize them.

## 12. Official references

- [Marlin G-code index](https://marlinfw.org/meta/gcode/)
- [G0/G1 linear moves](https://marlinfw.org/docs/gcode/G000-G001.html)
- [G2/G3 arc moves](https://marlinfw.org/docs/gcode/G002-G003.html)
- [G28 homing](https://marlinfw.org/docs/gcode/G028.html)
- [G90 absolute positioning](https://marlinfw.org/docs/gcode/G090.html)
- [G91 relative positioning](https://marlinfw.org/docs/gcode/G091.html)
- [G92 set position](https://marlinfw.org/docs/gcode/G092.html)
- [M82 absolute extrusion](https://marlinfw.org/docs/gcode/M082.html)
- [M83 relative extrusion](https://marlinfw.org/docs/gcode/M083.html)
- [M104 hotend temperature](https://marlinfw.org/docs/gcode/M104.html)
- [M109 wait for hotend](https://marlinfw.org/docs/gcode/M109.html)
- [M140 bed temperature](https://marlinfw.org/docs/gcode/M140.html)
- [M190 wait for bed](https://marlinfw.org/docs/gcode/M190.html)
- [M106 fan speed](https://marlinfw.org/docs/gcode/M106.html)
- [M204 acceleration](https://marlinfw.org/docs/gcode/M204.html)
- [M205 advanced motion settings](https://marlinfw.org/docs/gcode/M205.html)
- [M220 feedrate percentage](https://marlinfw.org/docs/gcode/M220.html)
- [M221 flow percentage](https://marlinfw.org/docs/gcode/M221.html)
- [M302 cold extrusion](https://marlinfw.org/docs/gcode/M302.html)
- [M400 finish moves](https://marlinfw.org/docs/gcode/M400.html)

---

**Casa Creta - G_code_engine**  
**Document:** Marlin-Flavour G-code Syntax Reference  
**Revision:** 2026-08-14
