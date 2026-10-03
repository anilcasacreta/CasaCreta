# BabyStep

Images: `5.png`, `6.png`, `7.png`

## Confirmed operation

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

## Safety

- Keep BabyStep correction within approximately `0–5 mm`.
- Do not press `Down` when the nozzle is already close to the bed. A collision can damage the nozzle, motors, bed, or other machine parts.
