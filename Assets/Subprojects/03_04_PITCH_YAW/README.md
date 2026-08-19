# Subprojects 03–04 — Pitch and Yaw

## Goal
Archive the combined pitch-and-yaw milestone as one paired Unity and Arduino subproject.

## Pairing
- Unity scene: `Scenes/SP03_04_PitchYaw.unity`
- Unity script: `Scripts/SP03_04_PitchYaw.cs`
- Arduino sketch: `Arduino/03_04_UNITY_PITCH_YAW/03_04_UNITY_PITCH_YAW.ino`

## Interface
- Serial: 9600 baud, newline terminated
- Telemetry: `ROT,<x>,<y>,<z>`
- Ordered command: `STOP`
- Unity sends world-space Euler rotation using latest-state delivery

## Dependency and reuse
Directly builds on Subproject 02's rotation-to-servo path. Subprojects 03 and 04 are grouped because pitch and yaw were developed as one combined milestone.

## Current archived baseline
The Unity scene and sender are separated from Subproject 02 and ready for the combined milestone. The Arduino sketch is copied from the last verified single-servo baseline; it currently maps Unity Y rotation to one servo on pin 9. Add the verified second-axis pin, mapping, inversion, and neutral behavior here before treating this archive as the final two-axis firmware snapshot.

## Hardware notes
Use an appropriate external 5 V supply for multiple servos and connect its ground to Arduino ground. Do not infer that a setup safe for one unloaded servo is adequate for two loaded servos.
