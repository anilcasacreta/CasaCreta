# ClayBot Mega Machine Interface Explanation

## BabyStep

Images: `5.png`, `6.png`, `7.png`

### Confirmed operation

- BabyStep adjusts the Z position without moving the Z sensor or making a physical machine adjustment.
- It may be used during a print when the nozzle-to-bed clearance needs correction.
- Select `0.01 mm`, `0.1 mm`, or `1 mm`, then press `Up` or `Down` to move Z by that amount.
- `Up` moves the nozzle upward and increases the nozzle-to-bed gap. `Down` moves it toward the bed and decreases the gap.
- `BabyStep 0.00` means no fine Z adjustment has been applied from the sensor-defined Z position.
- `Home Offset` shows how far the adjusted Z position is above or below the original Z-zero position.
- Unsaved adjustments are lost after the printer restarts. `Save` stores the offset for future use.
- Do not use `Save` for a temporary print correction. Save only a confirmed offset intended for continued use.
- `Reset` clears the BabyStep adjustment and restores the Z offset to `0.00`.
- `Back` returns to the Home screen.

### Safety

- Keep BabyStep correction within approximately `0–5 mm`.
- Do not press `Down` when the nozzle is already close to the bed. A collision can damage the nozzle, motors, bed, or other machine parts.

---

## Extruder

Images: `8.png`–`15.png`, `28.png`

### Confirmed operation

- The Extruder screen manually extrudes clay while the printer is idle or while a print is paused.
- Proper extrusion requires `10–12 bar` cylinder pressure.
- `E0` and `E1` represent the printer's two extruders/nozzles. `Nozzle` switches the selected extruder between them.
- `5 mm`, `10 mm`, `100 mm`, and `200 mm` set the commanded extrusion amount for each press.
- `Slow`, `Normal`, and `Fast` set the extrusion speed.
- `Load` runs the selected extruder using the chosen amount and speed. For example, pressing `Load` twice at `100 mm` commands a total of `200 mm`.
- `Unload` briefly reverses the auger to pull clay inward. Use it only for a small reverse movement because the clay path has insufficient space for substantial retraction.
- Use `Slow` or `Normal` for routine extrusion. Avoid `Fast` unless specifically required.
- The centre value shows the commanded extrusion value; `0.00 mm` indicates that no extrusion movement has yet been commanded.
- `Back` returns to the Home screen.

### Before extrusion

- Confirm that the compressor is on and cylinder pressure is applied.
- Confirm that the hose and nozzle are connected correctly, the air pipes and connectors are working, and the outlet area is clear.
- Confirm that enough clay remains in the cylinder before starting a print or manually extruding.
- Keep the nozzle approximately `100 mm` above the bed when testing clay flow.

### Safety

- Do not continue extrusion after the cylinder becomes empty. Escaping compressed air can damage the print and create a hazardous discharge through the material path.

### Precise confirmation required

1. If old clay is too hard to purge normally, what must the operator do after detaching the nozzle head?

   Answer:

---

## Move

Images: `1.png`, `2.png`, `3.png`, `38.png`

### Confirmed operation

- The Move screen manually moves the machine along the positive or negative X, Y, and Z directions.
- `X−`/`X+`, `Y−`/`Y+`, and `Z−`/`Z+` move the nozzle along the corresponding axis and direction.
- The displayed X, Y, and Z values are the current axis coordinates.
- Select `0.1 mm`, `1 mm`, `10 mm`, or `100 mm`, then press an axis-direction button. The selected distance is applied to each press.
- `Back` returns to the Home screen.

### Safety

- Before moving an axis, confirm that the bed is level and the nozzle has safe clearance from the bed and surrounding parts.
- Maintain safe Z clearance before making X or Y movements.

Use the smallest movement increment near the bed or an axis limit.

---

## Movement Menu

Images: `4.png`, `36.png`

### Default operation

- `Home` accesses axis homing.
- `Move` opens manual X/Y/Z movement controls.
- `Extrude` opens manual extrusion controls.
- `BabyStep` opens fine Z-offset adjustment.
- `Leveling` accesses bed-levelling controls.
- `Back` returns to the previous screen.

### Precise confirmation required

1. Does `Home` start homing immediately, or open the Home screen?

   Answer:

2. What exactly does `Disarm All` release, and which parts may then be moved safely by hand?

   Answer:

3. Is `Leveling` automatic, sensor-assisted, or manual? Briefly state what happens after it is pressed.

   Answer:

---

## Print Selection

Images: `32.png`, `33.png`, `34.png`

### Default operation

- Select a print source, browse to the required G-code file, and select it.
- The down arrow scrolls through the file list. The return arrow goes back to the preceding folder or screen.
- `OK` confirms the selected file; `Cancel` closes the confirmation without starting it.
- `Back` returns to the previous screen.
- Do not remove the USB drive while its print job is running.

### Precise confirmation required

1. At Casa Creta, is printing normally done from `TFT SD` or `TFT USB`? What physical storage does each option represent?

   Answer:

2. What information does `Brief` display?

   Answer:

3. State any required USB format, folder location, filename, or G-code file rules. Write `None` if there are no special rules.

   Answer:

---

## Print Status

Images: `22.png`, `26.png`, `29.png`

### Default operation

- The filename identifies the active print job.
- `Layer` shows the current print layer; the progress bar shows job completion.
- `Speed` and `Flow` show the active percentage overrides.
- `Pause` temporarily stops the job; `Resume` continues it.
- `BabyStep` opens fine Z adjustment during printing.
- `More` opens additional print controls.
- `Stop` terminates the print job.
- `Print finished` indicates completion; `Click for summary` opens the job summary.
- `Main` opens the main screen and `Back` returns to the preceding screen.

### Precise confirmation required

1. What do `T0`, `Bed`, and `F0` represent on this clay printer?

   Answer:

2. What does each of the two displayed time values represent?

   Answer:

3. Does `Stop` require confirmation before terminating the print?

   Answer:

4. What information is included in the finished-print summary?

   Answer:

---

## Ready Screen

Images: `30.png`, `31.png`

### Default operation

- `Ready` means the controller is idle and available for operation; it does not by itself prove that every axis has been homed.
- X, Y, and Z show the current axis coordinates.
- `Status` displays current machine or job messages.
- `Menu` opens the Main Menu.
- `Print` opens print-file selection.

### Precise confirmation required

1. What do `Fr.` and `Sp.` represent, and why do images `30.png` and `31.png` show different labels or values?

   Answer:

---

## Speed Control

Images: `24.png`, `25.png`

### Default operation

- This screen changes the print-speed override from its programmed value.
- `100/100` indicates a 100% speed setting relative to the programmed feed rate.
- `Decrease` and `Increase` reduce or raise the override by the selected percentage step.
- `5%` or `10%` selects the change applied per press.
- `Normal` represents the normal 100% setting.
- Speed may be adjusted during printing; change it gradually and observe the next layers.
- `Back` returns to the previous screen.

### Precise confirmation required

1. Does this control change the complete G-code feed-rate override, including print and travel moves?

   Answer:

2. What minimum and maximum speed percentages are approved for normal operation?

   Answer:

---

## Terminal

Image: `21.png`

### Default operation

- The Terminal sends manual firmware/G-code commands and displays controller responses.
- `Prev` and `Next` navigate command/history entries.
- `Clear` clears the entered command or terminal display.
- `Send` transmits the entered command to the controller.
- `ABC`, `Space`, and `Del` control text entry.
- `Back` returns to the previous screen.

### Restriction

- The Terminal should be used only by trained or authorized users. Incorrect commands can cause unexpected motion, change machine configuration, or damage the machine or print.

### Precise confirmation required

1. Are any Terminal commands approved for normal operators? If yes, list only those commands.

   Answer:

---

## Flow Control

Image: `23.png`

### Default operation

- Flow changes the extrusion override relative to the amount commanded by the G-code.
- `100/100` indicates normal flow at 100%.
- `Decrease` and `Increase` reduce or raise flow by the selected step.
- `5%` indicates the change applied per press.
- `Normal` restores the normal 100% setting.
- Flow may be adjusted during printing; change it gradually and observe the following layers.
- Increase flow for consistently under-filled layers. Decrease it for consistent over-extrusion.
- `Back` returns to the previous screen.

### Precise confirmation required

1. On this machine, does Flow change auger rotation/commanded extrusion, or does it control another output?

   Answer:

2. What minimum and maximum flow percentages are approved for normal operation?

   Answer:

---

## More Menu

Image: `27.png`

### Default operation

- `Extrude` opens manual extrusion controls.
- `Percentage` opens speed and flow percentage controls.
- `(Un)Load` opens material load/unload controls.
- `Back` returns to the previous screen.

### Precise confirmation required

1. Are `Heat` and `Fan` connected to any hardware on this clay printer, or should operators leave them unused?

   Answer:

2. What does `Feature` open or control?

   Answer:

3. What does `Machine` open or control?

   Answer:

---

## Main Menu

Image: `35.png`

### Default operation

- `Heat/Fan` opens heater and fan controls.
- `Movement` opens homing, manual movement, extrusion, BabyStep, and levelling controls.
- `(Un)Load` opens material load/unload controls.
- `Terminal` opens manual command entry and is restricted to trained users.
- `Settings` opens controller and machine settings.
- `Back` returns to the previous screen.

### Precise confirmation required

1. What exactly happens when the touchscreen `EM. STOP` button is pressed, and how is operation restored afterward?

   Answer:

2. What does `Custom` open or control?

   Answer:

3. Which `Settings` options, if any, may a normal operator change?

   Answer:

---

## Home

Image: `37.png`

### Default operation

- Homing moves an axis toward its reference sensor and establishes its machine-zero position.
- The main `Home` control homes all axes.
- `X`, `Y`, and `Z` home only the selected axis.
- `Back` returns to the previous screen.

### Safety

- Remove tools, clay, printed pieces, and all other objects from the printable area before homing.
- Keep clear of the gantry, bed, and printhead during movement.
- If an axis moves incorrectly, fails to stop, or approaches an obstruction, stop the machine immediately.

### Precise confirmation required

1. In what order are X, Y, and Z homed when the main `Home` control is pressed?

   Answer:
