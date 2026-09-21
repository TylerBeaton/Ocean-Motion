# Subproject 06 — Motion Pipeline and Serial Transport

## Purpose and boundary

Convert Subproject 05's calibrated boat telemetry into deterministic, bounded platform-neutral pose commands:

```text
SP05 telemetry → gains → pose limits → velocity limits → acceleration limits
→ optional smoothing → bounded pose command → versioned serial packet
```

This milestone does **not** select an actuator architecture, solve Stewart-platform inverse kinematics, or drive physical hardware. It now transports the architecture-neutral pose contract to an Arduino receiver for communication validation, but the firmware intentionally contains no actuator objects, output pins, PWM writes, or motor commands.

## Dependency and reuse status

- Depends on `BoatMotionTelemetry` from Subproject 05 as its calibrated simulation source.
- Preserves Subproject 05 unchanged; the SP06 scene references reusable SP05 assets and scripts.
- `MotionPoseSample`, `MotionPipelineSettings`, `MotionPoseProcessor`, `MotionPoseCommand`, and `MotionLimitFlags` are reusable downstream.
- `MotionTransportProtocol` and `MotionTransportSession` provide a versioned, architecture-neutral transport boundary reusable by later mechanism-specific stages.
- Paired receiver firmware is archived at `Arduino/06_UNITY_MOTION_TRANSPORT/06_UNITY_MOTION_TRANSPORT.ino`; its implementation is in the companion `MotionTransportFirmware.cpp` to avoid host-specific Arduino prototype generation.
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

## Serial transport contract

- Board: Arduino UNO R4 WiFi.
- Baud rate: `115200`, newline-delimited ASCII.
- Send cadence: `20 Hz` using unscaled host time.
- Queue policy: latest-state delivery; stale unsent poses are replaced rather than accumulated.
- Source freshness: the source sequence must advance within `250 ms` of unscaled realtime. This remains enforced when `Time.timeScale == 0`; a stalled source produces STOP rather than refreshing the firmware watchdog with a frozen pose.
- Handshake: Unity sends `OM1,HELLO` once per second until a wire-confirmed HELLO causes firmware to reply `OM1,READY`. Firmware sends no unsolicited READY at boot and rejects POSE before HELLO.
- Pose: `OM1,POSE,<transportSeq>,<sourceSeq>,<heave>,<pitch>,<yaw>,<roll>`.
- Acknowledgement: `OM1,ACK,<transportSeq>`.
- Stop: Unity sends `OM1,STOP` every `250 ms` until firmware replies `OM1,STOPPED`; pose transmission cannot resume before that reply.
- Watchdog: firmware reports `OM1,WATCHDOG` after `250 ms` without a valid pose.
- Errors: firmware reports `OM1,ERR,<reason>` for malformed, non-finite, out-of-range, oversized, pre-handshake, or out-of-order records. Every protocol error latches the receiver safe and requires a fresh handshake.
- Recovery: a protocol error or transport-sequence rollover closes the pose gate and requires a fresh HELLO/READY exchange. Ardity also clears and gates its queues across reconnect so neither a stale POSE nor stale ACK can cross connection generations.

The firmware independently rejects commands outside the saved SP06 software envelope: heave `±0.25 m`, pitch `±10°`, yaw `±5°`, and roll `±10°`. These remain provisional software-test limits, not approved physical mechanism limits.

The saved scene contains `SP06 Serial Transport (Hardware Disabled)`. Its `SerialController` is disabled and uses `/dev/cu.usbmodem-SET-ME` so opening the scene cannot accidentally claim a port. Select the actual port and enable that component only during the hardware test.

## Unfiltered and smoothed response measurements

The responsive preset was measured with deterministic step tests at 50 Hz. The unfiltered test also produced identical timings at 100 Hz.

| Axis | Unfiltered t90 | Smoothed t90 (`0.1 s`) | Added delay |
|---|---:|---:|---:|
| Heave | `0.460 s` | `0.580 s` | `0.120 s` |
| Pitch | `0.260 s` | `0.420 s` | `0.160 s` |

Manual visual comparison confirmed that enabled smoothing softens the processed proxy without the excessive slowdown caused by feeding smoothed output back into limiter state.

## Diagnostics

`MotionPipelinePoseDisplay` drives independent raw and processed proxy roots from their authored neutral local poses. Both proxies may overlap for direct comparison; contrasting materials and visual scale differences make them distinguishable.

Runtime telemetry, command, and transport diagnostics are shown through read-only custom Inspectors backed directly by the current records. Transport shows connection/readiness, STOP confirmation, ACK count and last acknowledged sequence, watchdog trips, protocol errors, and the last device message. ACK and fault counts remain cumulative across reconnects; the last sequence and latency sample reset at a new handshake. Diagnostics are not serialized into the scene.

Application-level ACK latency is sampled only when an ACK matches the latest generated pose. It includes host queue and Unity frame delays, not just serial transit. Older ACKs can advance the count but do not update this sample; zero means no sample yet. This deliberately does not measure packet loss, per-packet timeouts, or average/maximum wire RTT. Successful-write notifications remain solely to gate HELLO/READY, not to classify every pose's delivery outcome.

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
- display mapping and hierarchy separation;
- invariant packet formatting, exact firmware-envelope validation, realtime source freshness, handshake/reconnect gating, `20 Hz` scheduling, retried STOP/STOPPED recovery, application-level acknowledgement sampling, duplicate/stale ACK rejection, and latest-state queue replacement;
- host-native firmware parser and receiver-state tests for valid, malformed, non-finite, range, sequence-overflow, exact-duplicate, conflicting-duplicate, and out-of-order cases;
- UNO R4 WiFi firmware compilation with actuator output APIs absent;
- saved serial scene references, `115200` baud, disabled-by-default state, placeholder port, and acknowledgement queue depth.

After accounting simplification: `60/60` isolated Unity EditMode tests passed, and the serial Editor check routine completed successfully, including custom-delimiter output, reconnect-buffer reset, and component re-enable regressions. Its success marker is not a count of independently reported tests. `git diff --check` passed and SP05 remains unchanged.

Earlier firmware verification (firmware unchanged by this simplification): native C++ tests passed with `-Wall -Wextra -Werror`, and the UNO R4 WiFi build succeeded. The reported build used `60,412` bytes of flash (`23%`) and `7,228` bytes of RAM (`22%`); the replacement ARM toolchain emitted newlib syscall linker warnings. Physical upload and serial behavior remain unverified until the checklist below is run.

### Apple-silicon command-line toolchain

Arduino's bundled Renesas compiler and `bossac` uploader on this development host are Intel-only. Compatible native compile tools are installed at:

- compiler: `~/Library/Arduino15/native-tools/gcc-arm-embedded/bin/`
- ctags: `$(brew --prefix ctags)/bin`

Use those paths through Arduino CLI build-property overrides. The compile command below has been exercised. A compatible native `bossac` installation was attempted but did not install successfully, so command-line upload is not yet verified; board enumeration and upload remain part of the physical test.

```bash
CLI="/Applications/Arduino IDE.app/Contents/Resources/app/lib/backend/resources/arduino-cli"
"$CLI" compile --fqbn arduino:renesas_uno:unor4wifi --warnings all \
  --build-property "build.compiler_path=$HOME/Library/Arduino15/native-tools/gcc-arm-embedded/bin/" \
  --build-property "runtime.tools.ctags.path=$(brew --prefix ctags)/bin" \
  --build-property "compiler.c.extra_flags=-Wno-error=return-mismatch" \
  Arduino/06_UNITY_MOTION_TRANSPORT
```

Manual Play-mode checks completed:

- raw telemetry matches SP05 calibrated values;
- gain, clamp, velocity, and acceleration diagnostics behave as intended;
- invalid input recovers without a large jump;
- clamped output reverses promptly without sticking;
- raw and processed proxies visualize the expected neutral-relative motion;
- smoothing defaults off and behaves as measured when temporarily enabled.

## Hardware validation checklist

1. Connect only the UNO R4 WiFi by USB; do not connect servos, motor drivers, signal leads, or actuator power.
2. Stop Unity Play mode and close Arduino Serial Monitor/Plotter and every other serial terminal. Confirm only one program can own the port.
3. Upload `Arduino/06_UNITY_MOTION_TRANSPORT/06_UNITY_MOTION_TRANSPORT.ino` for **Arduino UNO R4 WiFi**. Start with Arduino IDE after selecting the detected `/dev/cu.usbmodem…` port. If its bundled uploader fails on this host, stop and resolve the uploader compatibility separately; a successful compile is not proof of upload.
4. With Unity stopped, open a serial terminal at `115200` using either LF or CRLF line endings. Require no unsolicited READY. Send a valid POSE before HELLO and require `OM1,ERR,HANDSHAKE`. Send `OM1,HELLO`; require exactly `OM1,READY`.
5. Send `OM1,POSE,1,1,0.10000,1.00000,2.00000,3.00000`; require `OM1,ACK,1`. Send the exact packet again and require the same ACK. Send the same transport sequence with a changed pose and require `OM1,ERR,SEQUENCE`; then send a new valid POSE and require `OM1,ERR,HANDSHAKE` until another HELLO/READY completes.
6. Send separate malformed probes, waiting at least `50 ms` between them: `BAD` → `OM1,ERR,FORMAT`; `OM1,POSE,2,2,nan,0,0,0` → `OM1,ERR,NONFINITE`; `OM1,POSE,2,2,0.25001,0,0,0` → `OM1,ERR,RANGE`.
7. Send `OM1,HELLO`, then one valid pose. Stop sending for more than `250 ms`; require one `OM1,WATCHDOG`. Send `OM1,STOP`; require `OM1,STOPPED`. Close the terminal before Unity claims the port.
8. In `SP06_Motion_Pipeline`, select `SP06 Serial Transport (Hardware Disabled)`, replace `/dev/cu.usbmodem-SET-ME` with the current port, and enable only its `SerialController` component.
9. Enter Play mode. Require `Port Connected` and `Firmware Ready`. During healthy motion, require increasing ACK count and sequence, watchdog trips `0`, and protocol errors `0`. Record available application-level ACK latency samples; they are not a worst-case latency guarantee.
10. Freeze simulation with `Time.timeScale = 0` while Play mode and serial remain active. Within roughly `0.5 s`, require `OM1,STOPPED` as the last device message and the ACK count to settle. Restore time scale and require fresh source motion to resume acknowledged poses.
11. While streaming, unplug and reconnect USB. Require the connection/readiness indicators to drop, then a fresh HELLO/READY before pose acknowledgements resume; require no protocol error or stale ACK attribution. Also disable/re-enable `MotionSerialTransport` with the port connected and require a new handshake. If macOS assigns a different device name, exit Play mode, disable `SerialController`, change the port, then re-enable it so the worker is recreated with the new name.
12. Exit and re-enter Play mode to reset cumulative diagnostics, then run for at least five minutes while moving the simulated boat. Require continued ACK progress, no unexplained stalls or disconnects, zero unexpected watchdog trips, and zero protocol errors. Record duration, ACK totals, available application-level latency samples, and observed issues. This is a functional endurance check, not proof of zero packet loss or bounded worst-case latency; those require separate timestamped capture if needed later.

Do not open Arduino Serial Monitor while Unity owns the port. Before any future actuator firmware is introduced, restore the scene's disabled serial default and define mechanism-specific travel, fault posture, power, and emergency-stop limits.

## Next integration boundary

Complete the hardware checklist above and record ACK progress, available latency samples, reconnect, and watchdog results. Only after that transport gate passes may a later stage introduce mechanism-specific inverse kinematics or actuator commands. Physical motion remains explicitly out of scope for this checkpoint.
