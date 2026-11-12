# Boat IK range tests — OI5

Open **Ocean Motion → SP09 → Open Boat IK Range Test**. This current scene shares one solved target between the coloured linkage view and six independent servo commands. Geometry and pulse-to-angle calibration remain provisional. See [HANDOFF](HANDOFF.md) for physical observations and [receiver details](../../../Arduino/09_IK_RANGE_PROBE/README.md) for the protocol.

## Presentation controls

Choose **Parameters → Additional settings → Water speed** to set the next run. **Start simulation** applies the settings in Calm. With a ready platform, **Start simulation + arm platform** waits for neutral and the matching firmware acknowledgement. The same button stops the simulation. Telemetry shows the active wave preset and speed throughout. Presentation runs leave recording off.

## Start and repeat a recorded test

1. If the board has an older OI3/OI4 receiver, upload `Arduino/09_IK_RANGE_PROBE/09_IK_RANGE_PROBE.ino` once, with servo supply off and the plate supported. Serial Monitor115200/Newline `?` should report `OI5,STATUS`, outputs off and hard_offset_limit_us500. Close Serial Monitor before Unity connects.
2. Play, open **Additional settings**, enter the USB port, Connect and wait for Firmware READY. Connection deliberately resets active allowance25µs and motion speed50µs/s; retained board offsets remain known for gradual neutral.
3. Choose **Next test** settings. They do not change current commands. Restore servo supply with the plate supported so any requested neutral return can physically occur.
4. Click **Prepare test**. It selects Calm with the chosen transition, resumes the source, requests gradual board neutral if needed, waits for transition plus3s of settling and board neutral, applies settings, calibrates once and waits for a fresh exact-neutral solution. Wait for **Prepared**.
5. Explicitly click **Arm at Calm + neutral**, then choose Bow/Side/Crossed. The timer starts at the first non-Calm selection, including its fade-in.
6. At the chosen duration, or using **Stop test + save**, the source pauses and the board is asked to return gradually to neutral. Observe completion. This does not switch PWM off.
7. Repeat: edit next-test settings → Prepare → Arm → waves. To finish, wait for neutral, support the plate, PWM OFF, then switch servo power off.

## Settings

| Control | Range | Default draft |
| --- | --- | --- |
| Pulse allowance | ±25–500µs, increments25 | ±500µs |
| Pose angle allowance | ±1–10° per axis | ±10° |
| Rotation gain | 0–1, increments0.01 | 0.90 |
| Heave gain | 0–0.02, increments0.0001 | 0.0125 |
| Movement speed | 50–500µs/s, increments50 | 500µs/s |
| Water speed | 0–4×, increments0.25 | 2× |
| Wave transition | 1/2/3s | 2s |
| Test duration | 1–3600s, decimals accepted;0/manual | 60s |

Choices remain selected between runs in the same Play session; a new Play restores defaults. Prepare applies motion settings at neutral. Arm is refused if draft/applied settings differ. Duration is captured on Arm and locks while preparing/armed. Pose allowance is a rejection ceiling; gain scales motion amplitude. Pulse allowance is a change from saved neutral, not an absolute pulse width. Speed controls command slew, not travel.

Saved neutrals0–5:1500,1600,1550,1600,1500,1500µs. Absolute1000–2000µs bounds remain checked before whole-pose acceptance and every firmware output tick. Thus channels1/3 stop at+400µs and channel2 at+450µs, even with500µs selected. Host checks unrounded absolute targets. There is no per-channel clipping, silent pose scaling or automatic allowance expansion.

Motion uses a shared fractional ramp,1–10µs/20ms. HOME always uses1µs/20ms(50µs/s), can take10s from500µs and has12s host timeout. A250ms watchdog HOLD requires manual recovery/rearm. HELLO retains the actual command vector while resetting selected guards. OFF is not mechanical homing. HOLD retains commanded PWM; it is not supply removal.

## Source transitions and timing

Water speed is separate from servo speed. Choose a quarter-step multiplier, then Prepare to apply it in Calm at board neutral. The active multiplier is shown in Telemetry beside the active preset; next-test edits never alter a running field. At0× wave travel freezes, while boat physics, source transitions and the wall-clock test timer continue. At1× the original waves are unchanged. Higher multipliers advance the wave-travel clock faster without changing amplitudes, wavelengths, Time.timeScale, fixed timestep or actuator limits. Rate changes preserve phase. Rendering and buoyancy share the mapped wave time, while preset transitions keep their normal simulation-time duration.

For tracking comparisons keep gains, allowances, transition duration and servo speed fixed; vary water speed initially1.00→1.25→1.50×. Faster forcing can change boat dynamics as well as requested command speed. Inspect raw/source behaviour and distinguish pose/pulse HOLDs from tracking lag. Speed0 is a frozen surface, not a flat-water reset. New telemetry fields `waterSpeedMultiplier` and `waveTimeSeconds` identify the active rate and source travel clock; the existing motion clock still aligns graph windows.

Preset buttons linearly blend old/new sampled wave surfaces on the simulation clock. Rendering and buoyancy share the same field. Retargeting mid-transition is continuous; repeated clicks on the same in-progress destination do not restart it. Frequencies, phases, physics damping and SP06 processing remain unchanged. Existing immediate `WaveField.SetPreset(preset)` behaviour is retained for archived scenes; duration/explicit-clock overloads are opt-in. Direct component/Inspector edits remain immediate.

The timer uses monotonic wall time, starts at wave selection and includes the transition. A60s test therefore includes its1–3s fade-in. Changing presets within a run does not reset the timer. Deadline enforcement occurs on the first Unity update at/after the deadline; completion can differ by a frame. Stop/HOLD/disconnect/fault cancels it; recovery never restarts or arms automatically. Early safety stops remain early stops, not full-duration successes. Wave phase/boat physics are not reset, so equal duration does not imply identical target traces.

Prepare may also be used while armed: it disarms/requests gradual neutral first. HOLD, PWM OFF, disconnect, board stops or selecting a non-Calm preset cancel preparation. If exact neutral cannot settle within15s, preparation stops.

## Telemetry and plots

Both physical IK scenes automatically record in ignored `TestLogs/Stewart/`. A session file captures setup; every acknowledged Arm opens a new timestamped `run-NNN.txt` file. The Console/right panel show the path/name. The script is tracked; recordings and plots stay local.

JSON lines at5Hz capture active settings, requested pose, source sequence/time/validity/flags, six candidate and acknowledged board offsets, transition state, status and rejection reasons. Start/stop/HOLD/warning/error events flush immediately; normal data flushes each second. A run's file continues through neutral return/setup until the next Arm. Null/empty candidate means unavailable/rejected, not zero. Board offsets are acknowledged PWM commands, not measured physical angles or positions.

For comparison use the `MOTION STARTED` marker and `motionElapsedSeconds` through the first `TEST STOPPED` marker. Exclude Calm setup and neutral return; separate transition from settled motion using `waveTransitionSeconds`, `waveTransitionProgress` and `waveTransitioning`. Automated checks have `batchMode=true`; exclude them from physical analysis. Files cap50MiB with a warning and are never automatically deleted. Abnormal termination may lose the final buffered second.

## Latest evidence and follow-up

On November9, a2s-transition mixed-preset test at rotation0.95/heave0.0125/speed150µs/s/500µs/9° completed60s. Sampled pitch±8.04°, roll up to±8.62°, peak offset382µs and median largest command error12µs. At gain1.00, Side requested−9.01° against9° and correctly HOLDed after18.01s; the transition had already completed. Repeat0.90 for filming margin, or validate repeated0.95 runs before choosing it. These are observations, not a validated general workspace or measured pose accuracy.

Software verification:13/13 SP05 sampling/transition tests, archived baseline, USB-disabled boat preparation/timer/transition/guard checks, manual full5° sweep/recovery, firmware tests at all200 speed/allowance combinations and UNO R4 WiFi build(67156bytes flash/9476bytes RAM). Historical OI3 reports remain separate baseline snapshots. See HANDOFF for provenance, older limit diagnoses and remaining calibration work.
