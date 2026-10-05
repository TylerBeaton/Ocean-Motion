# Unity Boat Telemetry Playback for Fusion 360

This Fusion 360 Python script reads CSV produced by `BoatTransformCsvRecorder.cs` and applies each neutral-relative pose to an existing occurrence such as the Stewart platform's moving top plate.

## Install and run

1. In Fusion, open **Utilities → Add-Ins → Scripts and Add-Ins**.
2. On **Scripts**, choose the green **+** and select this `UnityBoatTelemetryPlayback` folder.
3. Open the Stewart-platform design and ensure the moving platform occurrence is not grounded.
4. Run **UnityBoatTelemetryPlayback**.
5. Choose the Unity CSV and enter the occurrence name shown in Fusion's browser, such as `Platform:1`.
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
- Rotation is transformed by the same basis change using the recorded relative quaternion.
- The occurrence's transform at script start is the Fusion neutral pose.

## Model requirements and limitations

- The target must be an occurrence, not only a component definition.
- The target must not be grounded or locked by a rigid relationship.
- Existing assembly joints must be capable of solving from movement of the top occurrence. If the model does not articulate when that occurrence is moved manually, this generic driver cannot make it solve; the model needs an appropriate driving joint or a model-specific six-actuator mapping.
- This prototype does not compute Stewart inverse kinematics and must not be used to command physical actuators.
- Keep the design at its known neutral pose before each run. The script captures whatever pose exists at launch as neutral.

`telemetry_math.py` has no Autodesk dependency and is covered by the tests under `tests/`.
