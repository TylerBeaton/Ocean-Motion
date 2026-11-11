# Ocean Motion — current resume point

Updated 2026-11-09 (America/St_Johns). Current feature branch: `codex/sp09-water-speed-multiplier`, based on merged main. SP08/SP09 integration merged through PR18; README checklist through PR19.

## Current operation

- Boat: **Ocean Motion → SP09 → Open Boat IK Range Test**.
- Static/manual: **Ocean Motion → SP09 → Open IK Pose Range Sweep**.
- Receiver: `Arduino/09_IK_RANGE_PROBE/09_IK_RANGE_PROBE.ino`, protocol **OI5**. Older OI3/OI4 receivers need one updated upload, with servo supply off and plate supported. No upload is needed between runtime settings.
- Workflow: Connect → choose next-test settings → Prepare → wait for Prepared → Arm → wave preset → timed/manual **Stop test + save**. Draft edits never change active commands. Arm requires fresh source/board neutral and matching applied settings, pulse allowance and speed acknowledgements.
- Prepare cross-fades to Calm, resumes the source, requests gradual neutral if needed, waits for the fade plus three seconds of settling and board neutral, applies settings, calibrates once and waits for fresh exact neutral. It never arms or starts a wave automatically.
- Stop saves an immediate stop marker, pauses the source and requests gradual neutral. PWM remains on. Wait for neutral; support the plate before PWM OFF and switching servo supply off.

## Controls and guards

Water-speed feature: next-test multiplier0..4× by0.25 (default1×), applied in Calm by Prepare and checked before Arm. An anchored travel clock preserves phase on rate changes; both buoyancy/rendering use it. Water amplitudes, wavelengths, physics timestep and global time scale are unchanged. At0× wave travel freezes while boat physics, transitions and wall-clock timing continue. Telemetry adds waterSpeedMultiplier/waveTimeSeconds; the local plot helper labels water/servo speeds separately. No firmware upload is needed. Change only water speed between comparison runs, initially1.00→1.25→1.50×.

Verification:18/18 SP05 sampling/transition/speed EditMode tests passed. USB-disabled preparation, all17 rate selections, invalid/locked/unapplied-setting guards, timed stopping and rate telemetry passed. Optional `BoatIkRangeChecks.RunWaterSpeedChecks` also characterized raw4× Bow/Side/Crossed for15/15/60s; observed raw peaks≈8.78°pitch,9.41°roll,0.889m heave and1.97m/s speed, with no finite/bounds failures. This is a limited software characterization, not physical high-speed acceptance. Local summary: ignored `TestLogs/Stewart/characterization/water-speed-4x-2026-11-09.json`.

| Setting | Available values | New-Play default |
| --- | --- | --- |
| Pulse-offset allowance | ±25–500µs, increments25 | Draft200; connection resets active25 |
| Pose-angle allowance | ±1–10° per axis | Draft8; connection resets active5 |
| Rotation gain | 0–1 | Draft0.40 |
| Heave gain | 0–0.02 | Draft0.002 |
| Motion speed | 50–500µs/s, increments50 | 50 |
| Water speed | 0–4×, increments0.25 | 1× |
| Wave transition | 1/2/3 seconds | 2 |
| Test duration | 1–3600 seconds;0 means manual | 60 |

Saved neutrals, channels0–5: `[1500,1600,1550,1600,1500,1500]µs`. Absolute1000–2000µs checks remain independent of offset allowance, on host mapping before rounding, whole firmware packets and output ticks. Channels1/3 therefore allow at most+400µs and channel2+450µs, even when500 is selected. Reject whole invalid poses; never clip individual channels or silently increase limits.

HOME always uses50µs/s, can take10s from500µs, and has12s host timeout. HELLO retains held offsets while resetting selected allowance25/speed50. A250ms watchdog HOLD requires explicit rearm. Firmware uses a shared fractional ramp and consistent half-tie rounding to prevent a false RAMP fault on direction reversal across zero.

Timed motion begins at the first non-Calm selection after Arm, includes its transition and stops on the first Unity update at/after the deadline. Faults/manual stops cancel the timer; recovery never restarts or arms it. Wave phase and boat physics are not reset.

## Latest physical evidence

All observations below are commanded PWM tracking and user inspection, not measured platform angles or a general collision-free workspace.

- At rotation0.50/heave0.002, increasing motion speed50→100µs/s greatly reduced sustained command lag: Crossed median largest six-channel error fell from about70µs to6µs. Wave phases/windows differ; this is observational evidence, not a controlled physical-accuracy measurement.
- November6: Bow/Side completed60s at rotation0.75/heave0.0125/speed150, with500µs/8° allowances. Crossed completed60s at rotation0.60/heave0.0102/speed150. Higher-gain Crossed requests hit the angle guard.
- November9 before transitions: Side at rotation0.85/heave0.0125/speed150/500µs/9° stopped after0.345s, requesting−9.08°roll.
- After the2s source transition: mixed-preset runs at0.85 and0.90 completed30s. Rotation0.95 completed one60s run containing Bow/Side/Crossed and transitions, at heave0.0125/speed150/500µs/9°. Sampled pitch±8.04°, roll up to±8.62°, peak requested offset382µs and median largest command error12µs. No recorded serial errors. This is not three independent full60s tests.
- Rotation1.00 stopped after18.01s in Side, requesting−9.01°roll against9° allowance; the transition had already completed. It was an angle-bound HOLD, not a speed or serial failure.
- For filming, repeat0.90 for greater margin, or repeat0.95 across several complete runs before choosing it. A0.95 roll peak leaves only about0.38° observed margin to9°.

## Transition design and telemetry

The current boat opts into a linear cross-fade of complete sampled wave fields. Rendering and buoyancy share the same field and simulation clock. Retargeting snapshots the current blend; repeated same-destination clicks do not restart it. Merged travelling-wave components prevent an accumulating blend chain; sampling avoids per-vertex transition arrays. The existing immediate `WaveField.SetPreset(preset)` contract remains for archived scenes; optional duration/explicit-clock overloads support current transitions and deterministic tests/replay. No downstream filtering, physics damping or actuator-limit changes were introduced.

Ignored local `TestLogs/Stewart/` contains a startup/session file and separate timestamped `run-NNN` file per acknowledged Arm. Five-Hz JSON lines record settings, mapped pose, candidate/acknowledged offsets, source validity, faults and transition state. Start/stop events flush immediately; files continue through neutral return until the next Arm. Compare `motionElapsedSeconds` from the motion-start marker to the first test-stop marker, excluding setup/neutral return. Logs include selected duration, speed and wave transition duration/progress. Automated files have `batchMode=true` and must be excluded from physical analysis. Output caps50MiB/file; files are never automatically deleted. A crash can lose the last buffered second.

Graphs/source snapshots are local under `TestLogs/Stewart/plots/`; they are intentionally not committed. Notion's latest saved checkpoint remains November4 and needs reconciliation with these newer observations.

## Verification

No automated check opens hardware USB.

- SP05 EditMode sampling/transition suite:13/13 passed, including all preset pairs,1/2/3s linearity, phase preservation, interrupted/1000 rapid selections, repeated destination clicks and invalid inputs.
- Archived SP07 baseline and boat Play checks passed: source/pulse/absolute bounds, OI5 echo/profile locks, preparation, transition completion before calibration, timer/stop logging, stale-source HOLD and no automatic rearm. The1s timed check stopped at about1.003s.
- Manual full simulated±5° sweep,10° preflight gate, handle bounds and recovery passed.
- Firmware host tests passed all200 combinations of20 pulse allowances and10 speeds, absolute endpoints/refusals, retained500µs HOME, malformed packets, sessions, watchdog and fractional reversal regression.
- UNO R4 WiFi firmware build:67156bytes flash/9476bytes RAM.
- Reports committed with the earlier OI3 baseline are historical snapshots; current executable checks cover the newer controls above.

## Remaining work and preserved history

Geometry/pulse calibration remains provisional:35mm horn radius,184.5mm rods, estimated−5mm axial offset, top-centreZ181.4mm and11µs/degree. Actual angular zero, per-servo pulse calibration, joint/collision workspace and physical tracking accuracy remain open. The rotary prototype does not close Update08's final architecture decision gate.

Earlier apparent one-sided tilt had an oblique-camera perspective explanation, without establishing physical centering. A fixed neutral outline and side camera remain useful proposed follow-ups. Earlier100.2µs/100µs stops were pulse-limit HOLDs, not proven power faults; a separate historical USB exception has no established cause. Do not infer supply failure from a pose/pulse rejection.

Numerical single-axis pitch/roll peak offsets:4°≈110/105µs;5°≈140/133;6°≈170/161;7°≈201/190;8°≈233/220;9°≈267/252;10°≈303/284. These are command-space estimates. Separate static±8° observations do not validate arbitrary combined poses.

Published numerical/visual and SP07/SP08 archives remain intact. November5 cleanup removed superseded local probes and inactive UI, retained current scene GUIDs, trimmed boat YAML4579→1729lines and generated report2233→185lines. Detailed offline output remains available through `validate_ik.py --full-report /tmp/ocean-ik-full-report.json`. Current builders rebuild from the committed visual/current boat foundation; they do not depend on retired probes. See README and BOAT_IK_RANGE_TEST for operating details.
