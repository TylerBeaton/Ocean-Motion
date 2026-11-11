# SP09 — Stewart IK: current hardware tests

The current rotary prototype uses six independent IK solutions, not empirical pair patterns. Boat motion and the coloured target share the same solver result. The final Update08 actuator architecture remains undecided.

## Current entry points

| Purpose | Unity menu | Scene |
| --- | --- | --- |
| Live boat motion | Ocean Motion → SP09 → Open Boat IK Range Test | Scenes/SP09_BoatIkRange.unity |
| Manual poses and one-shot sweeps | Ocean Motion → SP09 → Open IK Pose Range Sweep | Scenes/SP09_IkPoseRangeSweep.unity |

Both now use [OI5 firmware](../../../Arduino/09_IK_RANGE_PROBE/README.md). Upload the updated sketch once for ±500µs selection; older OI3/OI4 firmware is not compatible with the expanded range. Scenes start with USB disabled; Connect and manual ARM are required. Support the plate before PWM OFF. Servo supply must be off during firmware upload.

- [Boat controls and range limits](BOAT_IK_RANGE_TEST.md)
- [Manual sweeps and adjustable pose allowance](POSE_ALLOWANCE_TEST.md)
- [Latest observations and resume point](HANDOFF.md)

Current workflow: choose next-test settings → Prepare → Arm → wave preset → timed/manual Stop test + save. Source transitions offer1/2/3s (default2); timed runs default60s. Per-run telemetry lives in ignored `TestLogs/Stewart/`. See the boat guide for defaults and repeatable comparisons.

## Current limits and assumptions

- Saved neutral pulses, channels0–5:1500,1600,1550,1600,1500,1500us.
- Firmware selectable offsets±25..500us in25us steps; explicit matching ARM echo; changes only disarmed at board neutral. Absolute1000..2000us guard remains.
- Selectable movement speed50..500us/s in50us/s steps (default50), applied by Prepare and echoed on ARM; neutral return remains50us/s. 250ms watchdog HOLD and explicit rearm. These can cause tracking lag; board offsets are commanded PWM, not position feedback.
- Boat gains: rotation0..1, translation0..0.02. Shared angle allowance1..10° per axis(default/reset5°); translation allowance±10mm. Limits/gains require neutral/disarmed configuration. Gain1 does not guarantee every pose fits.
- Water travel speed0..4× in0.25 steps (default1×) is applied by Prepare and logged separately from servo speed. At0× the wave surface freezes; boat physics, transitions and test timing continue. Rate changes preserve phase through an anchored travel clock shared by buoyancy and rendering.
- [Physical-test geometry](Measurements/provisional-physical-geometry.json):−5mm estimated axial horn offset,35mm radius,184.5mm rods and approximate neutral height/branch. Pulse scale11us/degree and physical angular zero are provisional.
- A failed source, pose, IK or pulse bound rejects the whole command. No per-servo clipping, silent pose reduction or automatic limit expansion.
- Numerical single-axis paths through about±8° fit within250us;±9/10° do not fit this provisional mapping. Actual pose accuracy and general combined workspace remain unmeasured.

## Code layout

- `Scripts/Core/RotaryStewartModel.cs`: pure analytical IK, independent of Unity/hardware.
- `Scripts/Runtime/StewartIkPreview.cs`: geometry rendering and neutral-relative target boundary.
- `Integration/BoatIndividualIkSource.cs`: unchanged SP06 processed motion → bounded target pose.
- `Integration/ApproximateIkPulseMap.cs`: six solved angle deltas → provisional pulse offsets.
- `Integration/IndividualIkSweepProbe.cs`: current OI5 connection, ARM, limits, watchdog and output panel. The class retains its original name for stable Unity references.
- `Integration/ManualPlatformTarget.cs` and `IkAxisSweep.cs`: manual target, path preflight and one-shot sweeps.
- `Integration/Editor/BoatIkRangeSetup.cs` and `IkPoseRangeSetup.cs`: rebuild current scenes directly from the committed visual foundation/current boat scene. No retired probe scenes required.

## Verification

No automated check opens USB. Close Unity before batch execution.

- `CurrentSceneChecks.Run`: load both scenes, check missing scripts/USB gates and rebuild temporary copies to verify dependency closure; temporary copies are removed afterward.
- `BoatIkRangeChecks.Run`: actual Bow/Side/Crossed processing, shared IK mapping, configuration/echo locks and HOLD behavior.
- `IkPoseRangeChecks.Run`: pose mapping bounds, static preflight, full simulated±5° cycle, rejection and recovery.
- Firmware host test: compile `Arduino/09_IK_RANGE_PROBE/tests/range_test.cpp` with `-IArduino/09_IK_RANGE_PROBE/tests/stubs`, then run it.
- Historical published-baseline reports (OI3; current executable checks cover OI5): [boat](Reports/boat-ik-range-check.json), [pose](Reports/ik-pose-allowance-check.json).

## Committed numerical and visual foundation

Existing published milestone archives are preserved: **Open IK Preview** (`SP09_IkPreview.unity`) and **Open Boat Motion IK** (`SP09_BoatMotionIk.unity`) are visual-only. Their default5° demonstration bounds and CAD geometry differ from the provisional physical-test dataset above. `ProcessedBoatIkBridge.cs` remains their adapter and provides the shared pure pose-mapping helper.

The generated offline JSON report is a compact fixture/continuity summary. Regenerate detailed per-leg data outside the project when needed:

```sh
python3 Assets/Subprojects/09_STEWART_IK/Offline/validate_ik.py --full-report /tmp/ocean-ik-full-report.json
```

Original measurements, [offline CAD geometry](Measurements/offline-geometry.json), source provenance, Python solver/tests and reference reports remain under Measurements, Offline and Reports. From `Offline`, run `python3 -m unittest discover -s tests -v` or `python3 validate_ik.py`. Unity numerical fixture checks remain `PreviewChecks.Run`. Existing SP07/SP08 and other numbered milestones are untouched.

Superseded uncommitted physical probes were pruned on2026-11-05. Their observations are summarized in HANDOFF; they are not required to rebuild the current scenes.

The current boat scene also removes the inactive telemetry canvas and legacy motion-comparison objects. The published visual baseline is unchanged. Rebuilds apply the same trim, and CurrentSceneChecks refuses their return.
