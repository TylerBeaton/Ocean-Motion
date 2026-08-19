# Subproject 02 — 3D Rotation Servo

## Demonstrates
Unity streams world-space Euler rotation to an Arduino UNO R4 WiFi. The Arduino displays the rotation on a 16x2 I2C LCD and maps Unity's Y rotation to a hobby servo.

## Pairing
- Unity scene: `Scenes/SP02_RotationServo.unity`
- Unity script: `Scripts/SP02_RotationServo.cs`
- Arduino sketch: `Arduino/02_UNITY_3D_SERVO/02_UNITY_3D_SERVO.ino`

## Interface
- Serial: 9600 baud, newline terminated
- Telemetry: `ROT,<x>,<y>,<z>`
- Ordered command: `STOP`
- Servo signal: pin 9
- LCD: 16x2 I2C at address `0x27`

## Mapping
Unity Y rotation from -90 to +90 degrees maps to servo angles from 180 to 0 degrees. The mapping is inverted so physical left/right motion matches the Unity GameObject. Values outside the input range are clamped; `STOP` or a one-second timeout returns the servo to 90 degrees.

## Dependency and reuse
Directly reuses the Subproject 01 rotation stream. This is a single-actuator control experiment; it does not decide the final Stewart-platform actuator architecture.

## Notes
For loaded operation, power the servo from an appropriate external 5 V supply and connect the supply ground to Arduino ground.
