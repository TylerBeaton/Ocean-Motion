# Subproject 06 — Motion Pipeline

## Purpose and boundary

Convert Subproject 05's calibrated boat telemetry into deterministic, bounded platform-neutral pose commands:

```text
SP05 telemetry → gains → pose limits → velocity limits → acceleration limits
→ optional smoothing → bounded pose command
```

This milestone does **not** select an actuator architecture, solve Stewart-platform inverse kinematics, or drive physical hardware. Its output remains an architecture-neutral pose contract for later serial and actuator experiments.

## Dependency and reuse status

- Depends on `BoatMotionTelemetry` from Subproject 05 as its calibrated simulation source.
- Preserves Subproject 05 unchanged; the SP06 scene references reusable SP05 assets and scripts.
- `MotionPoseSample`, `MotionPipelineSettings`, `MotionPoseProcessor`, `MotionPoseCommand`, and `MotionLimitFlags` are reusable downstream.
- The raw/processed pose proxies are display-only diagnostics and are not part of the source boat's physics hierarchy.

## Motion contract

- Translation metres: `(surge, heave, sway)`; currently only local/world Y heave is populated, so X and Z remain zero.
- Rotation degrees: local X pitch, local Y yaw, local Z roll.
- Orientation is neutral-relative calibrated telemetry from SP05, not wrapped world Euler angles.
- Each sample carries a session-local sequence, source simulation timestamp, and validity flag.

## Saved simulation baseline

| Setting | Translation | Rotation |
|---|---:|---:|
| Gain | `(1, 1, 1)` | `(1, 1, 1)` |
| Pose limit | `(0, 0.25, 0)` m | `(10, 5, 10)` deg |
| Velocity limit | `(0, 0.75, 0)` m/s | `(90, 45, 90)` deg/s |
| Acceleration limit | `(0, 3, 0)` m/s² | `(360, 180, 360)` deg/s² |
| Smoothing time | `0.1 s` | `0.1 s` |

Smoothing is disabled by default. These values are provisional simulation/software-test limits, not approved physical-platform limits. Final pose, rate, acceleration, power, and travel limits must come from the selected mechanism and measured hardware workspace.

## Deterministic conditioning

`MotionPoseProcessor` is a pure stateful processor with an explicit timestep. Each axis is handled independently.

1. Reject invalid samples, invalid timesteps, and invalid settings without advancing state.
2. Apply per-axis gains.
3. Clamp the target to the pose envelope.
4. Apply per-axis velocity limits.
5. Apply per-axis acceleration limits with braking-aware target speed.
6. Optionally apply timestep-independent exponential smoothing to the published pose.
7. Re-clamp before publication.

Limiter state and smoothed published state are separate. Smoothing therefore adds measured lag without feeding reduced output back into the velocity/acceleration limiter. Internal velocity is synchronized to actual conditioned movement so a final pose clamp cannot hide outward momentum.

The controller uses elapsed source time between fresh samples. The first sample uses `Time.fixedDeltaTime` because no previous source timestamp exists. An enabled source that publishes no fresh sequence for more than `0.25 s` produces an invalid command with `StaleInput`; consumers must not treat the retained pose as an active actuator command.

After a stale, invalid, or disabled interval, the first recovered sample uses one fixed timestep rather than integrating the entire outage. This prevents a reconnect from turning elapsed downtime into a one-frame catch-up movement.

Runtime Inspector setting changes update the processor configuration without resetting its motion state. A newly tightened velocity limit is enforced as a hard bound immediately, even when doing so takes priority over the configured acceleration limit. Disabling smoothing reconciles the limiter state to the actual published pose and rate before continuing, avoiding an unsmoothed catch-up jump. Non-finite settings or negative smoothing times produce a finite invalid command with `InvalidSettings`.

## Unfiltered and smoothed response measurements

The responsive preset was measured with deterministic step tests at 50 Hz. The unfiltered test also produced identical timings at 100 Hz.

| Axis | Unfiltered t90 | Smoothed t90 (`0.1 s`) | Added delay |
|---|---:|---:|---:|
| Heave | `0.460 s` | `0.580 s` | `0.120 s` |
| Pitch | `0.260 s` | `0.420 s` | `0.160 s` |

Manual visual comparison confirmed that enabled smoothing softens the processed proxy without the excessive slowdown caused by feeding smoothed output back into limiter state.

## Diagnostics

`MotionPipelinePoseDisplay` drives independent raw and processed proxy roots from their authored neutral local poses. Both proxies may overlap for direct comparison; contrasting materials and visual scale differences make them distinguishable.

Runtime telemetry and command diagnostics are shown through read-only custom Inspectors backed directly by `LatestSample` and `LatestCommand`. They are not serialized into the scene, preventing stale Play-mode values and flags from being mistaken for live state.

## Verification status

Automated checks cover:

- immutable sample mapping and calibration/finite-value validity;
- gain, pose, velocity, acceleration, braking, and reversal behavior;
- invalid samples, timesteps, and settings without state corruption;
- equivalent explicit-timestep response at 50 Hz and 100 Hz;
- optional smoothing behavior and measured delay;
- elapsed source-time processing after skipped samples;
- stale-source invalidation after `0.25 s`;
- bounded recovery after stale, invalid, disabled, or rejected samples;
- runtime setting changes without processor-state reset;
- hard published velocity bounds while smoothing remains enabled;
- smoothing-disable continuity while the published pose lags limiter state;
- display mapping and hierarchy separation.

Manual Play-mode checks completed:

- raw telemetry matches SP05 calibrated values;
- gain, clamp, velocity, and acceleration diagnostics behave as intended;
- invalid input recovers without a large jump;
- clamped output reverses promptly without sticking;
- raw and processed proxies visualize the expected neutral-relative motion;
- smoothing defaults off and behaves as measured when temporarily enabled.

## Next integration boundary

The next stage may serialize bounded pose commands to an Arduino at a measured target rate, with acknowledgements and a transport watchdog. Actuator outputs must remain disabled during transport validation. Inverse kinematics and physical motion remain separate downstream stages.
