# Flow Control

Image: `23.png`

## Default operation

- Flow changes the extrusion override relative to the amount commanded by the G-code.
- `100/100` indicates normal flow at 100%.
- `Decrease` and `Increase` reduce or raise flow by the selected step.
- `5%` indicates the change applied per press.
- `Normal` restores the normal 100% setting.
- Flow may be adjusted during printing; change it gradually and observe the following layers.
- Increase flow for consistently under-filled layers. Decrease it for consistent over-extrusion.
- `Back` returns to the previous screen.

## Precise confirmation required

1. On this machine, does Flow change auger rotation/commanded extrusion, or does it control another output?

   Answer:

2. What minimum and maximum flow percentages are approved for normal operation?

   Answer:
