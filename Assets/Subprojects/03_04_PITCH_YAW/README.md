# Subprojects 03–04 — Pitch and Yaw

## Goal
Model and control a two-servo serial kinematic chain in which the pitch servo is physically mounted on the yaw servo.

## Pairing
- Unity scene: `Scenes/SP03_04_PitchYaw.unity`
- Unity script: `Scripts/SP03_04_PitchYaw.cs`
- Arduino sketch: `Arduino/03_04_UNITY_PITCH_YAW/03_04_UNITY_PITCH_YAW.ino`

## Unity hierarchy

```text
RigBase
└── Servo1Joint                 Yaw, local Y
    ├── Cylinder (Display)
    └── Servo2MountOffset       (0, 0.502, 0)
        └── Servo2Joint         Pitch, local X
            ├── Cylinder (Display)
            └── PayloadOffset   (-0.25, 0, 0)
                └── Cube (Display)
```

`Servo1Joint` is positioned at `(0, 0.25, 0)` under `RigBase`. Objects marked `(Display)` are rendering-only and are not sampled for control.

The modeled forward transform is:

```text
objectTransform =
    baseTransform
    * servo1Rotation
    * servo2MountOffset
    * servo2Rotation
    * payloadOffset
```

## Coordinate conventions
- Servo 1/yaw: `Servo1Joint.localEulerAngles.y`
- Servo 2/pitch: `Servo2Joint.localEulerAngles.x`
- RigBase world placement is not transmitted
- Servo 2's local axis moves with Servo 1 through hierarchy inheritance

## Calibration pipeline
Unity is the single calibration authority for each joint:

1. Read the designated local joint angle.
2. Wrap relative to its neutral using `Mathf.DeltaAngle`.
3. Apply the joint inversion setting.
4. Clamp to the joint's mechanical limits.
5. Scale around the configured minimum, neutral, and maximum servo commands.
6. Send the final integer commands using latest-state delivery.

Default calibration:

| Joint | Neutral local angle | Inverted | Mechanical range | Servo commands |
|---|---:|---:|---:|---:|
| Pitch | 0° | No | -90° to +90° | 0 / 90 / 180 |
| Yaw | 0° | Yes | -90° to +90° | 0 / 90 / 180 |

These values are starting defaults and must be replaced with measured safe values before loading the mechanism.

## Serial interface
- Baud: 9600
- Line ending: newline
- Latest-state command: `SERVO,<pitchCommand>,<yawCommand>`
- Ordered command: `STOP`

Arduino validates and clamps the received commands, writes them directly to the servos, and does not repeat Unity's wrapping, inversion, or scaling.

## Hardware
- Pitch servo signal: pin 10
- Yaw servo signal: pin 9
- LCD: 16x2 I2C at address `0x27`
- `STOP` or a one-second timeout returns both servos to their configured neutral commands

Use an appropriate external 5 V supply for both servos and connect its ground to Arduino ground. Validate each unloaded axis independently before testing combined motion.

## Dependency and reuse
This integration milestone builds on Subproject 02's single-servo path and the latest-state Ardity transport. The hierarchy and local-joint conventions are reusable; the hobby-servo mechanics remain an experiment and do not decide the final Stewart-platform actuator architecture.
