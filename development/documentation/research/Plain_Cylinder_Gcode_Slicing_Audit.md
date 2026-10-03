# Plain Cylinder G-code Slicing Audit

## Conclusion

The printer engineer may be referring to a real **slicing/profile problem at the base**.

The G-code itself is not corrupted: layer order, Z movement, XY geometry, main-wall speed, and main-wall extrusion are consistent. However, Cura produces very different toolpaths and extrusion amounts on the first four layers.

## Likely slicing problem

| Layer | Cura paths | Commanded extrusion |
|---|---|---:|
| 0 | Outer wall + skin | 1954.67 E |
| 1 | Outer wall + inner wall + skin | 1578.46 E |
| 2 | Outer wall + skin | 1954.66 E |
| 3 | Outer wall + inner wall + skin | 1578.47 E |

Layers 1 and 3 command approximately **19.2% less clay** than layers 0 and 2, even though the cylinder diameter is unchanged.

The embedded Cura profile uses very different settings for each feature:

- Outer-wall flow: 250%
- Inner-wall flow: 60%
- Skin flow: 100%
- Outer-wall width: 8 mm
- Inner/skin widths: approximately 10 mm
- Spiralize mode: enabled

Cura changes the path classification between consecutive base layers. Because the feature flows are very different, the deposited clay volume alternates. This can produce an uneven base, pressure changes, bulging, or weak interfaces.

## What is correct

From layers 5–608:

- Width remains 258.69–258.70 mm.
- Extruding path remains 812.56–812.69 mm per revolution.
- Extrusion remains 484.96–485.04 E per revolution.
- Print speed remains 17 mm/s.
- Center remains fixed at X250.00, Y345.74.
- Layer height remains 1.5 mm.
- No missing layers or unexpected Z drops were found.

The main cylinder wall is therefore highly consistent.

## Final layer

Layer 609 slows to 9.79 mm/s and gradually reduces extrusion to zero. This matches Cura's smooth spiral finishing and does not look like file corruption. It may produce a tapered top edge.

## Checks performed

The complete 95,377-line file was checked for:

- All 610 layer markers
- Z progression and layer height
- XY dimensions and center position
- Extrusion continuity after `G92 E0` resets
- Per-layer extrusion and toolpath length
- Feed-rate changes
- Cura feature types: wall, inner wall, and skin
- Retractions and travel moves
- Embedded Cura slicing settings

Temperature commands were ignored because this is a clay printer.

## Recommended action

1. Open Cura Preview and inspect layers 0–4 by feature type and flow.
2. Make outer-wall, inner-wall, and skin flow settings closer to each other for the base.
3. Confirm whether inner-wall and skin paths are required for this plain spiral cylinder.
4. Re-slice and compare commanded extrusion on consecutive base layers.
5. Keep the layers 5–608 vase-wall settings unchanged.

## Limitation

This audit proves what the G-code commands. Confirming the exact original slicing error requires the STL or Cura project and a preview of layers 0–4.
