# DurBeen ClayBot Mega — Operation Manual Draft

## 1. Machine Overview

| Item | Specification |
|---|---|
| Machine | DurBeen ClayBot Mega |
| Type | Ceramic extrusion 3D printer |
| Build volume | 1000 × 1000 × 1000 mm |
| Nozzle range | 1–10 mm |
| Clay capacity | 10 L / approximately 23 kg |
| Extrusion system | Pneumatic-fed, motor-driven auger extruder |
| Cylinder feed pressure | 10–12 bar |
| Maximum print speed | 30 mm/s |
| Maximum travel speed | 50 mm/s |
| Input power | 220–240 V AC |
| Voltage stabilizer | 2–3 kVA |
| Controls | TFT touchscreen |
| File input | SD card, USB drive, or USB cable |

### Main Components

- Machine frame and motion axes
- Print bed/build plate
- Clay cylinder
- Air regulator and pressure gauge
- Material hose
- Motor-driven auger extruder
- Nozzle
- TFT touchscreen/controller
- Voltage stabilizer

### Labelled Photos Required

- Full machine
- Controls and emergency stop
- Clay cylinder and air regulator
- Hose connections
- Extruder, auger, and nozzle
- Print bed and home position

## 2. Before You Start

1. Remove everything from the printable area before homing.
2. Check the air pipes and connectors for correct operation and leakage.
3. Switch on the compressor and confirm 14–16 bar tank pressure.
4. Load prepared clay and set the cylinder feed pressure to 10–12 bar.
5. Home the machine and confirm the nozzle position above the bed.

### Opening the Clay Cylinder

1. Release all cylinder pressure using the regulator control.
2. Confirm the cylinder is fully depressurized.
3. Remove the air pipe from the top of the cylinder.
4. Only then open the cylinder cap.

**DANGER:** Never open a connected or pressurized cylinder. Pressure can eject the metal cap and cause broken bones, severe head injury, or death.

## 3. Start a Print

1. Prepare the model as STL, OBJ, or 3MF.
2. Slice it using the approved ClayBot Mega profile.
3. Keep the toolpath inside the 1000 × 1000 × 1000 mm build volume.
4. Export G-code and load it using SD card, USB drive, or USB cable.
5. Confirm nozzle size, layer height, speed, extrusion rate, and print position.
6. Set cylinder feed pressure within the recorded 10–12 bar operating range.
7. Run the auger manually and purge until clay flow is continuous.
8. Select the correct file and start the print.
9. Watch the first layers continuously.

**Do not remove the pen drive/USB drive while the print job is running.**

**DATA REQUIRED:** approved slicer profile, start/end G-code, layer height, auger rate, origin, and touchscreen commands.

## 4. During Printing

Check for:

- Continuous clay flow
- Uniform bead width and layer height
- Good bonding between layers
- No bubbles, gaps, or nozzle blockage
- Stable walls without leaning or collapse
- Free movement of the hose and printhead

Do not:

- Reach into the motion area
- Touch moving parts or the nozzle
- Open the pressurized cylinder
- Disconnect pressurized hoses
- Move the bed or printed piece
- Make large pressure or setting changes during printing

## 5. Finish the Print

1. Confirm printing, motion, and extrusion have stopped.
2. Stop the auger.
3. Park the printhead using the machine controls.
4. Isolate the compressed-air supply.
5. Depressurize the clay system and confirm the gauge reads zero.
6. Remove the piece with its build plate where possible.
7. Move it on a flat, rigid support to the drying area.
8. Label and record the print.

### Print Record

- Job/part ID
- File name and revision
- Date and operator
- Clay batch/recipe
- Nozzle size
- Layer height and speed
- Auger setting and air pressure
- Print result and defects

## 6. Cleaning

1. Stop the machine and fully depressurize the clay system.
2. Remove remaining clay before it dries.
3. Clean the nozzle, clay-contact parts, build plate, and spills.
4. Inspect the nozzle, auger, hose, seals, and fittings.
5. Dry cleaned parts before reassembly or storage.

Do not apply water directly to:

- Touchscreen, controller, switches, or electrical cabinet
- Motors, drivers, sensors, or connectors
- Bearings, rails, belts, screws, or lubricated parts
- Voltage stabilizer or power connections

**DATA REQUIRED:** approved nozzle, hose, auger, and cylinder cleaning procedure.

### Purging Old Clay from the Material Pipe

1. Depressurize the system before disconnecting the pipe from the nozzle.
2. Secure the pipe outlet inside a collection container.
3. Confirm there is enough clay in the cylinder.
4. Apply cylinder pressure and purge the old clay from the 1.5–2 m pipe.
5. Stop before the cylinder becomes empty.

**DANGER:** Never let the cylinder run empty while purging. At 10–12 bar, compressed air can violently eject clay and propel the cylinder's metal cap/piston as a projectile. It can cause broken bones, severe head injury, or death. Keep everyone away from the pipe outlet and stop before the clay runs out.

## 7. Troubleshooting

| Problem | Main action |
|---|---|
| Clay not extruding | Check clay supply, pressure, auger, hose, and nozzle. |
| Blocked nozzle | Stop, depressurize, remove, and clean the nozzle. |
| Uneven extrusion | Purge air; check clay consistency, pressure, auger rate, and blockage. |
| Air bubbles | Use de-aired clay and refill the cylinder without air pockets. |
| Print collapsing | Use stiffer clay or reduce layer height, flow, or unsupported geometry. |
| Layers separating | Check clay moisture, layer height, print speed, and extrusion flow. |
| Machine stops | Record the error; check power, file, controller, and axis obstruction. |
| Nozzle collision | Stop immediately; check homing, origin, bed level, and nozzle offset. |
| Hose or fitting leak | Stop, isolate air, depressurize, and replace the damaged part. |
| File rejected | Re-export G-code using the approved slicer profile and storage device. |

## 8. Settings and Recipes

| Parameter | Known value |
|---|---:|
| Nozzle diameter | 1–10 mm |
| Maximum print speed | 30 mm/s |
| Maximum travel speed | 50 mm/s |
| Acceleration | 25 mm/s² |
| Cylinder feed pressure | 10–12 bar |
| Layer height | **DATA REQUIRED** |
| Auger/extrusion rate | **DATA REQUIRED** |
| Clay/water ratio | **DATA REQUIRED** |
| Casa Creta piece settings | **DATA REQUIRED** |

## 9. Maintenance

| Interval | Main work |
|---|---|
| Daily | Clean and inspect nozzle, hose, seals, fittings, bed, cables, and controls. |
| Weekly | Check rails, belts/screws, fasteners, limit switches, auger, and cylinder closure. |
| Monthly | Check alignment, bed level, homing, electrical enclosure, and pressure components. |

**DATA REQUIRED:** lubricant, lubrication points, service intervals, adjustment limits, and replacement-part numbers.

## 10. Emergency / Do Not Do

Stop immediately for:

- Person or object in the motion path
- Nozzle or axis collision
- Hose, fitting, regulator, or cylinder leak
- Abnormal noise, vibration, heat, smell, or smoke
- Uncontrolled extrusion
- Print collapse into the printhead path

Emergency actions:

1. Press the emergency stop if fitted and identified.
2. Keep people away from moving and pressurized components.
3. Isolate electrical power and compressed air when safe.
4. Do not open the clay system until the gauge reads zero.
5. Do not restart until the fault is identified.

**DATA REQUIRED:** emergency-stop location, reset procedure, main isolator location, and Casa Creta emergency contact.

### Manufacturer Contact

- +91 99227 14414
- +91 96649 40997
- durbeendesignworks@gmail.com

## Sources

- https://www.durbeen.in/ceramic-3d-printer-claybolt-mega
- Casa Creta operating and safety information recorded in this manual.
