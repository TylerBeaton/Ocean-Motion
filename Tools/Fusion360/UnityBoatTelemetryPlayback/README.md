# Unity Boat Telemetry Playback for Fusion 360

This Fusion 360 Python script reads CSV produced by `BoatTransformCsvRecorder.cs` and applies each neutral-relative pose to an existing occurrence such as the Stewart platform's moving top plate.

## Install and run

1. In Fusion, open **Utilities → Add-Ins → Scripts and Add-Ins**.
2. On **Scripts**, add an existing script and select `UnityBoatTelemetryPlayback.py` in this folder; do not create a new template script.
3. Open the Stewart-platform design and ensure the moving platform occurrence is not grounded.
4. Run **UnityBoatTelemetryPlayback**.
5. Choose the Unity CSV, enter the CSV's calm Unity Y value in metres, then enter the occurrence name shown in Fusion's browser, such as `Platform:1`.
6. Inspect the final pose while the completion dialog remains open. Choosing **OK** restores the starting pose by default.

A `*.fusion-joint-ranges.csv` report is written beside the input file. It reports minimum, maximum, and travel for slider/cylindrical joint displacement in Fusion internal centimetres and revolute/cylindrical rotation in radians when those joint values are exposed by the constrained model.

## Configuration

Edit the constants near the top of `UnityBoatTelemetryPlayback.py`:

- `TARGET_OCCURRENCE_NAME`: set it to skip the name prompt.
- `TRANSLATION_SCALE`: scales translation amplitude; rotation remains the recorded rotation.
- `PLAYBACK_SPEED`: `2.0` plays twice as fast.
- `LOOP_PLAYBACK`: repeats until the script is stopped.
- `RESTORE_NEUTRAL_AFTER_PLAYBACK`: set `False` to leave the final pose applied.
- `WRITE_JOINT_RANGE_REPORT`: enables the joint report.

## Coordinate and unit contract

- Unity axes: +X right, +Y up, +Z forward.
- Fusion mapping: Unity `(X,Y,Z)` becomes Fusion `(X,Z,Y)`.
- Unity metres are converted to Fusion API centimetres (`100 cm/m`).
- The calm Y prompt subtracts a value from the CSV's **neutral-relative** `pos_y_m` before translation scaling. For the `boat-telemetry-test-calm-to-bow-20261005-143419.csv` capture, calm is `0.352202` m. Use `0` when the CSV is already zero at calm; do not enter a raw world-space Y value unless it is also the CSV value.
- Rotation is transformed by the same basis change using the recorded relative quaternion.
- The occurrence's transform at script start is the Fusion neutral pose.

## Model requirements and limitations

- The target must be an occurrence, not only a component definition.
- The target must not be grounded or locked by a rigid relationship.
- This script sets the top occurrence's transform directly. In the tested rotary-linkage assembly, this did not move the connected rods even though manual dragging does; `adsk.doEvents()` and `design.computeAll()` did not make the joints follow. Joint-driven articulation is a separate milestone.
- This prototype does not compute Stewart inverse kinematics and must not be used to command physical actuators.
- Keep the design at its known neutral pose before each run. The script captures whatever pose exists at launch as neutral.

`telemetry_math.py` has no Autodesk dependency and is covered by the tests under `tests/`.
