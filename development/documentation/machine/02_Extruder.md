# Extruder

Images: `8.png`–`15.png`, `28.png`

## Confirmed operation

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

## Before extrusion

- Confirm that the compressor is on and cylinder pressure is applied.
- Confirm that the hose and nozzle are connected correctly, the air pipes and connectors are working, and the outlet area is clear.
- Confirm that enough clay remains in the cylinder before starting a print or manually extruding.
- Keep the nozzle approximately `100 mm` above the bed when testing clay flow.

## Safety

- Do not continue extrusion after the cylinder becomes empty. Escaping compressed air can damage the print and create a hazardous discharge through the material path.

## Precise confirmation required

1. If old clay is too hard to purge normally, what must the operator do after detaching the nozzle head?

   Answer: already answered ( detach extruder head and claen manully bya ny tool)
