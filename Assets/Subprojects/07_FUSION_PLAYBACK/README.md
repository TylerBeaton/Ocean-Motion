# Subproject 07 — Unity-to-Fusion Playback

This is an offline visualization bridge, not physical platform control. `BoatTransformCsvRecorder` records the boat's six-degree-of-freedom pose relative to a calibrated neutral pose. The companion Fusion 360 script is in `Tools/Fusion360/UnityBoatTelemetryPlayback/`.

## Unity setup

1. Add `BoatTransformCsvRecorder` to the boat or a manager object.
2. Assign the boat transform (it defaults to the component's own transform).
3. For a post-settle capture, disable **Begin Recording On Start** and invoke **Calibrate Neutral** from the component context menu while the boat is at rest.
4. In Play Mode, click **Start Recording** in the component Inspector; click **Stop Recording** when the capture is complete.
5. Set **Output Directory** in the Inspector if needed. The default is `Tools/Fusion360/UnityBoatTelemetryPlayback/recordings` relative to the Unity project root. Absolute paths are also accepted; relative paths in a built player use `Application.persistentDataPath`. The Console prints the full saved CSV path.

The CSV stores neutral-relative position in metres and neutral-relative orientation as a quaternion. It avoids wrapped Euler angles and keeps quaternion signs continuous.

## Boundary

Playback is intended to validate model geometry, linkage articulation, and travel. It does not perform Stewart-platform inverse kinematics, enforce hardware limits, or send actuator commands.
