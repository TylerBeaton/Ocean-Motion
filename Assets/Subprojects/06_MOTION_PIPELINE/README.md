# Subproject 06 — Motion Pipeline and Serial Transport

## Purpose and boundary

Convert Subproject 05's calibrated boat telemetry into deterministic, bounded platform-neutral pose commands:

```text
SP05 telemetry → gains → pose limits → velocity limits → acceleration limits
→ optional smoothing → bounded pose command → versioned serial packet
```

This milestone does **not** select the final actuator architecture or solve Stewart-platform inverse kinematics. The Unity pipeline and serial contract remain architecture-neutral. The paired SP06.2 demonstration firmware now drives two unloaded hobby servos from pitch and roll while displaying telemetry on an LCD; this is a bounded integration demo, not the final platform actuator layer.

## Dependency and reuse status

- Depends on `BoatMotionTelemetry` from Subproject 05 as its calibrated simulation source.
- Preserves Subproject 05 unchanged; the SP06 scene references reusable SP05 assets and scripts.
- `MotionPoseSample`, `MotionPipelineSettings`, `MotionPoseProcessor`, `MotionPoseCommand`, and `MotionLimitFlags` are reusable downstream.
- `MotionTransportProtocol` and `MotionTransportSession` provide a versioned, architecture-neutral transport boundary reusable by later mechanism-specific stages.
- Paired receiver firmware starts at `Arduino/06_UNITY_MOTION_TRANSPORT/06_UNITY_MOTION_TRANSPORT.ino`. `MotionTransportFirmware.cpp` owns the protocol loop and safety state, while `MotionHardware.h/.cpp` own the two-servo PCA9685 mapping, LCD telemetry, and RGB status.
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
| Pose limit | `(0, 0.25, 0)` m | `(70, 5, 70)` deg |
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

The firmware independently rejects commands outside the saved SP06 software envelope: heave `±0.25 m`, pitch `±70°`, yaw `±5°`, and roll `±70°`. Pitch maps to PCA9685 channel `0` and roll to channel `1`. Their experimental `235…379` pulse envelope is centred at `307`; those endpoints remain calibration candidates rather than approved mechanism limits.

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
- host-native firmware parser and receiver-state tests for valid, malformed, non-finite, `±70°` range boundaries, sequence-overflow, exact-duplicate, conflicting-duplicate, and out-of-order cases;
- host-native hardware-mapping tests for both servo channels, endpoints, centre, midpoint interpolation, clamping, and neutral targets;
- UNO R4 WiFi firmware compilation with PCA9685, LCD, and RGB hardware output isolated in `MotionHardware.cpp`;
- saved serial scene references, `115200` baud, disabled-by-default state, placeholder port, and acknowledgement queue depth.

After accounting simplification: `60/60` isolated Unity EditMode tests passed, and the serial Editor check routine completed successfully, including custom-delimiter output, reconnect-buffer reset, component re-enable, and unavailable-listener teardown regressions. Its success marker is not a count of independently reported tests. `git diff --check` passed and SP05 remains unchanged.

Earlier firmware verification (firmware unchanged by this simplification): native C++ tests passed with `-Wall -Wextra -Werror`, and the UNO R4 WiFi build succeeded. The reported build used `60,412` bytes of flash (`23%`) and `7,228` bytes of RAM (`22%`); the replacement ARM toolchain emitted newlib syscall linker warnings. The transport-only firmware was subsequently uploaded and exercised in the Unity-to-UNO validation below.

### SP06.1 Unity-to-UNO transport result — 2026-09-21

The physical validation used Unity `6000.5.7f1`, one Arduino UNO R4 WiFi at `/dev/cu.usbmodem3CDC754A58082`, and `115200` baud. Servos, displays, actuator drivers, relays, and actuator signal wires remained disconnected. The firmware remained the transport-only SP06 baseline; `STOP` and watchdog reports were treated as protocol state, not physical neutral or power removal.

| Check | Observed result |
|---|---|
| Five-minute focused run | Passed. The POSE sequence high-water mark and acknowledged-pose count were `5915 / 5915`, equivalent to `19.72 Hz` over `300 s`. The final sampled application-level ACK latency was `8.40 ms`; this is neither an average nor a worst-case bound. Watchdog trips and protocol errors were both `0`. |
| ACK continuity | No ACK gap was indicated during the focused run: `5915` acknowledged poses against last ACK sequence `5915` (`0%` observed missing ACKs). This diagnostic result is not a timestamped wire capture and does not prove zero physical packet loss. |
| Stale source | Passed. Setting `Time.timeScale = 0` produced `OM1,STOPPED`, cleared the pending STOP state, and settled the ACK count at `2530`. Restoring time scale resumed ACK growth without reconnecting. |
| Focus interruption | Six deliberate switches away from Unity produced six watchdog reports because `runInBackground: 0` lets the update loop pause for longer than the firmware's `250 ms` timeout. Returning focus resumed transport. These events are separate from uninterrupted reliability. |
| USB disconnect/reconnect | Passed without restarting Play mode. Unity observed disconnect and reconnect, completed a fresh `OM1,READY` handshake, restored connected/ready state, and resumed ACK growth with `0` protocol errors. One `OM1,WATCHDOG` occurred at the deliberate reconnect boundary after READY; it did not affect the focused run's zero-watchdog result or prevent recovery. |
| Play-mode teardown | Passed after making Ardity's final disconnect notification tolerant of an already-disabled listener. Play mode exited without `SendMessage OnConnectionEvent has no receiver!`, and `lsof /dev/cu.usbmodem3CDC754A58082` returned no owner. |
| Saved baseline | Passed. The serial Editor check completed, the scene was restored to its disabled placeholder-port state, and the authoritative Mac working tree was clean. |

### SP06.2 two-servo demonstration status

The working monolithic telemetry demonstration was used as the behavioral reference for the modular firmware. The refactor preserves the tested transport/state classes and moves physical behavior behind `MotionHardware`: PCA9685 setup, pitch channel `0`, roll channel `1`, LCD telemetry, RGB state, centring on startup/HELLO/STOP/watchdog/fault, and pose-to-pulse mapping. The modular source and its host tests compile, but the refactored binary still needs upload and a short physical regression run before it replaces the monolithic sketch as the bench-verified build.

The current pulse calibration is experimental:

| Command | Nominal servo angle | PCA9685 pulse |
|---:|---:|---:|
| `-70°` | `20°` | `235` |
| `0°` | `90°` | `307` |
| `+70°` | `160°` | `379` |

Pulse counts determine physical travel. Validate endpoints unloaded and one axis at a time; source-level tests and a successful compile do not establish mechanical safety.

### Apple-silicon command-line toolchain

Arduino's bundled Renesas compiler and `bossac` uploader on this development host are Intel-only. Compatible native compile tools are installed at:

- compiler: `~/Library/Arduino15/native-tools/gcc-arm-embedded/bin/`
- ctags: `$(brew --prefix ctags)/bin`

Use those paths through Arduino CLI build-property overrides. The compile command below has been exercised. A compatible native `bossac` installation was attempted but did not install successfully, so command-line upload is not yet verified; board enumeration and upload remain part of the physical test.

Install `Adafruit PWM Servo Driver Library` and `LiquidCrystal I2C` through Arduino Library Manager before compiling. The Adafruit library also installs `Adafruit BusIO`.

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

## SP06.2 demo checklist

1. Stop Unity Play mode and close Arduino Serial Monitor/Plotter and every other serial terminal. Only one program may own the serial port.
2. With actuator power off, confirm pitch is on PCA9685 channel `0`, roll is on channel `1`, the LCD is at `0x27`, and the PCA9685 is at `0x40`.
3. Keep Arduino `5V` on LCD/PCA9685 logic only. Feed PCA9685 `V+` from the dedicated regulated `5 V / 10 A` supply, bypass the breadboard for servo current, and keep external-supply negative, Arduino ground, PCA9685 ground, and LCD ground common. Never connect external-supply positive to Arduino `5V` or the Arduino-powered breadboard positive rail.
4. Upload `Arduino/06_UNITY_MOTION_TRANSPORT/06_UNITY_MOTION_TRANSPORT.ino` for **Arduino UNO R4 WiFi**. A successful compile is not proof of upload; wait for `SP06.2 READY` on the LCD and both servos to centre before proceeding.
5. Test unloaded and mechanically clear. Start with moderate commands, then exercise pitch and roll one axis at a time. Stop immediately for binding, continuous buzzing, chatter, heat, non-increasing motion, supply sag, or an Arduino/LCD reset. Do not treat the `235…379` endpoints as mechanically certified.
6. With Unity stopped, use a serial terminal at `115200`. Send `OM1,HELLO` and require `OM1,READY`, then send `OM1,POSE,1,1,0,10,0,0` and require `OM1,ACK,1` plus pitch-only motion. Re-handshake and send `OM1,POSE,1,1,0,0,0,10` for roll-only motion. Close the terminal before Unity claims the port.
7. Send `OM1,STOP` and require `OM1,STOPPED`, red RGB state, a stopped LCD message, and both servos centred. After a fresh HELLO and valid pose, stop sending for more than `250 ms`; require `OM1,WATCHDOG` and the same centred posture. A malformed or out-of-range packet must produce `OM1,ERR,<reason>`, red fault state, centred servos, and a required fresh handshake.
8. In `SP06_Motion_Pipeline`, select `SP06 Serial Transport (Hardware Disabled)`, replace `/dev/cu.usbmodem-SET-ME` with the current port, and enable only its `SerialController` component.
9. Enter Play mode. Require `Port Connected`, `Firmware Ready`, increasing ACK count/sequence, green RGB state during valid poses, live LCD telemetry, pitch motion on channel `0`, roll motion on channel `1`, watchdog trips `0`, and protocol errors `0`.
10. For today's demo, keep the rig unloaded and the simulated motion moderate. End by exiting Play mode or disabling transport and confirm the STOP path centres both servos before removing actuator power.

Do not open Arduino Serial Monitor while Unity owns the port. The modular hardware boundary is ready to gain additional channels later, but this checkpoint intentionally implements only pitch, roll, LCD, and RGB behavior.

## Next integration boundary

SP06.1's transport gate remains the reusable foundation, and SP06.2 adds a narrow two-servo hardware adapter without coupling PCA9685/LCD/RGB behavior to packet parsing. Additional servos should be added through `MotionHardware` configuration and tests, not directly in `MotionTransportFirmware.cpp`. Mechanism-specific inverse kinematics still requires its own limits, calibration, fault posture, power plan, and emergency-stop gate after the actuator architecture is selected.
