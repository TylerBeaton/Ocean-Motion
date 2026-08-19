# Subproject 01 — 3D Rotation Telemetry

## Demonstrates
Unity streams a GameObject's world-space Euler rotation to an Arduino UNO R4 WiFi and displays X, Y, and Z on a 16x2 I2C LCD.

## Pairing
- Unity scene: `Scenes/SP01_RotationTelemetry.unity`
- Unity script: `Scripts/SP01_RotationTelemetry.cs`
- Arduino sketch: `Arduino/01_UNITY_3D_TELEMETRY/01_UNITY_3D_TELEMETRY.ino`

## Interface
- Serial: 9600 baud, newline terminated
- Telemetry: `ROT,<x>,<y>,<z>`
- Ordered command: `STOP`
- LCD: 16x2 I2C at address `0x27`

## Dependency and reuse
Builds on the serial path proven in Subproject 00. The rotation sender and Ardity latest-state delivery are reused by Subproject 02.

## Notes
Telemetry uses latest-state delivery to avoid stale FIFO backlog. Euler angles wrap through 0–360 degrees and are suitable for this display milestone, not yet a final Stewart-platform control protocol.
