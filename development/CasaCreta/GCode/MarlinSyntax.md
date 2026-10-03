# Marlin G‑Code Syntax (Reference for Casa Creta)

## 1. Start Sequence (Initialization)
```gcode
;FLAVOR:Marlin
;Generated with Cura_SteamEngine 5.12.0
M104 S25           ; Set hotend target temp to 25C (standby/ambient)
M105               ; Report temperatures
M109 S25           ; Wait for temp to reach 25C
G28                ; Auto home all axes (X, Y, Z)
G90                ; Set all coordinates to ABSOLUTE positioning
G1 Z10 F300        ; Lift nozzle 10mm at 300 mm/min (5 mm/s) for bed clearance
M82 ;absolute extrusion mode
G92 E0             ; Reset extruder accumulator to 0
G92 E0             ; Duplicate reset (safety redundancy)
;LAYER_COUNT:342
;LAYER:0
M106 S255          ; Enable fan / auxiliary relay at 100% duty cycle
```

## 2. Motion & Extrusion Formatting Rules
### 2.1 Travel Moves (`G0`)
- **Syntax**: `G0 F{speed_mm_min} X{x} Y{y} Z{z}`
- Nominal travel feedrate: `F1020` (≈ 17 mm/s).
- Z‑axis travel feedrate: `F300` (≈ 5 mm/s).

### 2.2 Extrusion Moves (`G1`)
- **Syntax**: `G1 X{x} Y{y} E{cumulative_e}`
- Feedrate `F` is **modal** – once set, it stays active until changed.
- Example of dynamic feedrate for bottom solid layers: `G1 F665.7 …` then `G1 F709.4 …`.

### 2.3 Number Precision & Formatting
- Coordinates (`X`, `Y`, `Z`): **3 decimal places** (e.g., `X383.073 Y507.670 Z1.300`).
- Extrusion (`E`): **5 decimal places** (e.g., `E0.64598`).
- Feedrates (`F`): integer or single decimal (e.g., `F1020` or `F665.7`).
- **Locale**: Always use `.` as decimal separator (`CultureInfo.InvariantCulture`).

## 3. Layer Transition Example
```gcode
;MESH:NONMESH
G0 F300 Z2.6               ; Step up Z at 300 mm/min (5 mm/s)
G0 F1020 X386.711 Y477.763 ; Rapid travel to new start point
G0 X382.948 Y482.817       ; Precision alignment move
;TIME_ELAPSED:1907.273108
;LAYER:1
;TYPE:WALL-OUTER
G1 X390.838 Y488.371 E13.12956   ; Resume extrusion (M82 absolute)
```

## 4. End Sequence (Shutdown & Park)
```gcode
G91                ; Switch to RELATIVE positioning mode
G1 E-1 F300        ; Retract 1mm at 300 mm/min (5 mm/s) to relieve nozzle pressure
G1 Z10             ; Lift Z 10mm to avoid collision with top layer
G28 X0             ; Home X axis (parks carriage away from print)
M82 ;absolute extrusion mode  ; Restore ABSOLUTE extrusion mode
M104 S0            ; Turn off hotend heater
;End of Gcode
```

*These snippets capture the essential Marlin dialect used for the large‑format Soller paste extrusion workflow.*
