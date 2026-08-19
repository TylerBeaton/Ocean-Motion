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
- Unity X/pitch drives the pitch servo on pin 10
- Unity Y/yaw drives the yaw servo on pin 9

## Mapping
- Pitch: -90 to +90 degrees maps to servo 0 to 180 degrees
- Yaw: -90 to +90 degrees maps to servo 180 to 0 degrees
- Input outside either range is clamped
- `STOP` or a one-second timeout returns both servos to 90 degrees
- LCD row 1 shows pitch/yaw input; row 2 shows both servo commands

## Dependency and reuse
Directly builds on Subproject 02's rotation-to-servo path. Subprojects 03 and 04 are grouped because pitch and yaw were developed as one combined milestone.

## Current implementation
The Unity sender keeps the compatible `ROT,x,y,z` packet. Arduino maps X to the pitch servo and Y to the yaw servo through separate pin and inversion constants, while preserving latest-state telemetry and ordered `STOP` behavior.

## Hardware notes
Use an appropriate external 5 V supply for both servos and connect its ground to Arduino ground. The pin assignments compile but must still be checked against the physical wiring. If either axis moves backward, change only its `invertPitchServo` or `invertYawServo` constant.
