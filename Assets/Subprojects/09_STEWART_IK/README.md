# SP09 — rotary Stewart IK and Unity visual preview

This stage now has a runnable analytical solver for the current rotary prototype. It produces six signed horn-angle solutions and reconstructed linkage points for a requested pose. It has **no PWM conversion, serial transport or hardware output**. Earlier Arduino experiments, SP07 scenes and source measurement files remain preserved. This experiment does not freeze the project's final actuator architecture.

## Start here

- [Current handoff / next physical-calibration steps](HANDOFF.md). Visual motion is accepted; physical output remains disabled.
- For processed boat motion, choose **Ocean Motion → SP09 → Open Boat Motion IK**, then **Play**. Let the boat settle in **Calm**, click **Calibrate boat neutral**, then select **Bow**, **Side** or **Crossed**. Boat motion appears on the left and the solved platform on the right. **Pause / Neutral** holds the visual target at neutral while the boat keeps simulating; **Resume boat** reconnects the processed pose.
- In Unity, choose **Ocean Motion → SP09 → Open IK Preview**, then press **Play**. Start with **Heave +1 mm**, **Neutral**, then **Pitch +1°**. The six coloured linkages solve individually for the requested top-plate pose.
- [Preview scene](Scenes/SP09_IkPreview.unity) and [rendered neutral geometry](Reports/unity-preview-neutral.png). The scene is independent of the archived milestones; geometry is generated on entering Play mode.
- The Game-view panel has sway/heave/surge sliders (±10 mm) and pitch/yaw/roll sliders (±5°). These are demonstration ranges, not verified mechanical travel limits. **Test rejection: +1 m heave** deliberately rejects a pose, hides solved rods/horns and shows a red desired plate. **Neutral** recovers. Nothing is clipped into an apparently valid result.
- For Scene handles, enable **Use Target transform**, select the child **Target - neutral-relative pose**, and move/rotate it during Play mode. Keep its scale at one. The contract reads a neutral-relative quaternion; the slider Euler angles are only input controls. Use a sufficiently large Game view (roughly 1200×800 or larger) for the full panel.
- Hardware output remains absent. This is the visual geometry gate before connecting the boat-motion source or Arduino.
- [Numerical check report](Reports/offline-check-report.md) and [full JSON diagnostics](Reports/offline-check-report.json).
- [Editable offline geometry](Measurements/offline-geometry.json).
- [Solver](Offline/stewart_ik.py), [CLI](Offline/run_ik.py), and [tests](Offline/tests/test_ik.py).

From the repository root:

```sh
cd Assets/Subprojects/09_STEWART_IK/Offline
python3 run_ik.py
python3 run_ik.py --translation-mm 0 0 1
python3 run_ik.py --unity-pose examples/unity-small-pose.json
python3 validate_ik.py
python3 -m unittest discover -s tests -v
```

Python3.9+ standard library only. Default is neutral; Fusion translation arguments are neutral-relative MILLIMETRES and Fusion rotation uses quaternion x,y,z,w. The Unity JSON example instead contains neutral-relative `position_m` and `rotation_xyzw`; it requests1 mm heave. This is an offline file interface, not a live Unity connection or CSV player. An unsolved or invalid input emits a diagnostic JSON with no partial six-leg result and exits2. Successful mathematical solves exit0, always with hardware output disabled.

## Processed boat → visual IK

`Scenes/SP09_BoatMotionIk.unity` is a separate copy of the archived SP06 simulation with a visual IK view added. SP05 boat/water physics, calibrated telemetry and SP06 processing are reused unchanged. The copied scene removes `MotionSerialTransport` and `SerialController`, disables the old comparison canvas/proxies, and starts with Calm water and an uncalibrated source. The original SP06 scene and manual SP09 preview remain independent.

`Integration/ProcessedBoatIkBridge.cs` consumes `MotionPipelineController.LatestCommand` in LateUpdate. It maps neutral-relative translation metres at gain `0.02` and signed X pitch / Y yaw / Z roll degrees at default gain `0.45`, then constructs a quaternion using the same composition as SP06's processed display. The general preview boundary accepts metres plus a quaternion; it has no dependency on the boat or processing layer. No additional smoothing or physics changes are introduced.

Examples: 100 mm processed heave becomes 2 mm visual heave; 8° processed pitch becomes 3.6° visual pitch. The bridge rejects an entire mapped pose beyond ±10 mm per translation axis or ±5° per rotation input, rather than clipping it. The Game panel exposes rotation gain `0..1` (`1` means the processed angle); the Inspector has the same range. Translation gain remains `0..0.1`, default `0.02`. Gain `0.45` is five times the previously tested `0.09`; raising gain still rejects poses exceeding the unchanged ±5° envelope. Those are visual demonstration bounds, not physical workspace limits. The existing SP06 pose/rate/acceleration flags remain visible in the panel. Its ±250 mm source heave bound maps to ±5 mm here.

An invalid or disabled source, nonfinite input/gain, reversed source sequence/time or a sequence frozen for over 250 ms of real time hides the solved geometry. Freezing `Time.timeScale` also expires the source. Fresh data recovers automatically; a sequence/time restart requires Pause/Resume. Pause/Neutral explicitly displays mathematical neutral without stopping boat physics. Calibration must be done after the boat settles in Calm; wave buttons reuse the established Bow/Side/Crossed presets.

Validation: [live test report](Reports/unity-boat-ik-check.json), [live IK render](Reports/unity-boat-ik.png). The initial 20-second crossed-wave run at gain `0.05` passed with zero pose rejections. The report records the latest rerun at the increased default rotation gain; observed heave remains bounded by 5 mm from the existing SP06 source clamp. Checks also passed uncalibrated-source rejection, frozen-time staleness, disabled-source rejection, recovery and pause-neutral. Mapping checks cover all three translation/rotation axes, metre scaling, invalid/NaN input and gain rejection, exact gain endpoints (including the corrected `0.1f` boundary), fivefold rotation without changing heave, envelope rejection, scene references, missing scripts and absence of serial components. This proves the software connection, not physical motion or unlimited source accuracy.

The bridge lives outside the preview assembly because the existing SP06 runtime controller is in Unity's default assembly. Its dependency is one-way: SP06 → integration adapter → generic SP09 preview API. `BoatIkSceneBuilder.Open` is available from the menu; `BoatIkChecks.Run` and `BoatIkPlayModeCheck.Run` are Editor batch verification entry points, with the latter exiting Unity when complete.

The user visually accepted the processed-motion connection and gains `0.02 / 0.45` on 2026-11-02. Preserve these as the visual baseline. Physical bring-up starts with reconciling the deferred horn-ball geometry and neutral pose, then measuring each servo's signed angle-to-pulse mapping and enforcing calibrated bounds. Validate small static poses and stop/watchdog behavior before boat playback; the visual gains do not approve a physical workspace.

## Geometry currently used

| Input | Source/status |
| --- | --- |
| Shaft centres and axes | Corrected six P/Q samples in servo-shafts-fusion.json; first wrong-feature set preserved under Measurements/archive |
| Top attachments | Corrected T0..T5, including correctedT3; user clarified these are FINAL locations for corresponding channels0..5, no permutation needed |
| Top local frame | Centre at reported CAD(0,0,191.712), axes chosen parallel to Fusion global at neutral; local jointZ=0 |
| Physical neutral estimate | Rough measured plane separation165.4 mm, yielding provisional control centre(0,0,181.4) using shaft datumZ16; CAD raw positions remain unchanged |
| Horn radius / rods | Reported35 mm /184.5 mm, provisionally common to all6 |
| Horn radial reference | Derived from reported CAD H points; mathematical zero, NOT physical servo zero |
| Signed horn axial offsets | About+6.85 mm from CAD; **UNVERIFIED PLACEHOLDERS** because physical ball joints mount on the other side and user deferred remeasurement |
| Servo angle bounds / pulse maps | Missing; null angle limits are explicit,1000..2000 µs records are not converted to degrees |

Edit per-channel `axial_offset_mm`, `reference_radial_direction`, `horn_radius_mm`, `rod_length_mm`, `top_local_mm` and neutral pose in offline-geometry.json as measurements are reconciled. Never blindly negate CAD offset6.85 to represent the other horn side. Configured optional `angle_limits_deg` are bounds relative to that channel's configured radial reference; use measured calibration before populating them. These options do not enable physical output.

`build_geometry.py` regenerates the provisional config from original measurements. Existing output files are preserved unless `--force` is explicitly supplied. Regeneration deliberately restores CAD horn placeholders, so do not overwrite later physical corrections accidentally:

```sh
python3 build_geometry.py --output ../Measurements/new-offline-geometry.json
python3 run_ik.py --geometry ../Measurements/new-offline-geometry.json
```

## Mathematical contract

For shaft point P, unit axis a and signed axial offset d, the horn circle centre is `C=P+d*a`. Let u be the configured radial reference projected perpendicular to a, and `v=a×u`. At angle theta, `H=C+r*(u*cos(theta)+v*sin(theta))`. The rod constraint is `|T-H|=L`, where T is the rotated/translated local top joint. This reduces to two analytic candidate angles per leg. Initial neutral branch is the one nearest the CAD radial reference, then that algebraic branch stays locked and angles unwrap near the last solution. This branch assumption needs visual/physical confirmation.

The solver rejects nonfinite/malformed inputs, unreachable circle/sphere intersections, indeterminate or tangent individual-leg solutions, configured angle-limit violations and an optional requested continuity-step violation. It reconstructs all rods after solving; any leg error rejects the whole pose without partial output or actuator clipping. Optional previous-angle input supports numerical sequence continuity, not validation of a physical path between samples.

`angle_deg_from_reference` is a right-hand angle about the P→Q shaft axis. `neutral_relative_angle_deg` is relative to the mathematically solved provisional neutral. **Neither is an Arduino Servo.write angle or PWM command.**

## Unity coordinate boundary

Established Unity convention: X rotation=pitch,Y=yaw,Z=roll,Y displacement=heave. Input quaternion/translation must ALREADY be relative to a calibrated Unity neutral pose. Proposed basis maps Unity(X,Y,Z) to Fusion(X,Z,Y), and metres to millimetres. That basis reflects handedness; rotations transform as `R_F=B*R_U*B^T`, not by swapping Euler angles or blindly swapping quaternion components. No Euler input is used. Neutral control axes are Fusion-aligned by definition. Actual Unity scene heading/scale alignment and motion scaling still need visual confirmation before integration.

The module supports a nonidentity neutral control rotation and arbitrary shaft axes/positive or negative axial offsets; numerical tests cover those cases.

## Verification and acceptance gate

- 15 automated tests passed: independent rod/horn-circle reconstruction, both analytic roots, non-axis-aligned/offset geometry, invalid input and tangent rejection, whole-pose/angle-limit rejection, branch continuity across two turns, quaternion/basis conversion, rotated neutral frames, config editability and permanently disabled hardware output. Includes250 deterministic randomized small-pose closure samples.
- 15 diagnostic fixtures passed: neutral,±1 mm translations and±1° rotations on all six Unity channels, a combined pose, and rejection of1 m heave.
- 181-sample small tilt/heave path passed; maximum adjacent angle change0.165692°, rod numerical closure error at most5.68e-14 mm. This is floating-point consistency, not physical accuracy.

The Unity C# core is separate from its preview/JSON adapter and contains no Unity engine dependencies. It uses the same configured geometry and pinned branch policy as Python. `PreviewChecks.Run` independently compares all 15 Python reference fixtures (including an unreachable pose), checks 181 continuous Unity quaternion poses, reflected rotation/translation axes, invalid inputs and whole-pose continuity rejection. Geometry SHA-256 must match the fixture snapshot. Regenerate fixtures after an intentional geometry edit with `python3 build_unity_fixtures.py`; review the updated Python results before running Unity checks.

Validation on Unity6000.5.7f1 /URP17.5.0:

- Fresh project compilation and archived SP07 scene load passed before adding runtime integration: 13 roots, no missing scripts ([baseline](Reports/unity-baseline-check.json)).
- C# vs Python fixtures passed, maximum angular difference 3.82e-14°; all 181 continuity samples passed ([numerical report](Reports/unity-preview-checks.json)). These are numerical tolerances, not physical accuracy.
- Actual Play mode passed neutral, +1 mm heave, +1° pitch, unreachable-pose rejection without stale linkages, neutral recovery, Scene target input and invariance under moving/rotating the preview root ([runtime report](Reports/unity-playmode-check.json)). Neutral geometry was rendered and visually inspected.
- No packages, project settings, archived scenes or hardware sketches were changed. Play-mode checks are an Editor batch entry point, not a runtime feature. Menu **Ocean Motion → SP09 → Run numerical checks** reruns the mathematical comparison; `PreviewPlayModeCheck.Run` is a batch-only entry point that exits the Editor on completion.

The user reviewed the manual Unity preview on 2026-11-02 and confirmed the layout and movement looked correct. The processed boat-motion connection now passes automated checks; next is hands-on comparison of the boat and IK view, including wave directions and Pause/Neutral. Actual physical horn-side offsets, angular calibration/limits, joint/collision checks, full-platform Jacobian singularity/workspace checks, supply/load/reset behavior and small static physical poses remain separate proof gates before physical output. No measured height or physical safety envelope is established by these tests. Hardware remeasurement remains deferred as requested.

## Sources and provenance

- [Rotary Stewart-platform IK derivation](https://raw.org/research/inverse-kinematics-of-a-stewart-platform/). This implementation generalizes the horn circle to arbitrary shaft axes and signed axial offsets.
- [Unity rotation/quaternion conventions](https://docs.unity.com/en-us/engine/6000.3/manual/working-with-gameobjects/gameobject-fundamentals/rotation-orientation/quaternion-and-euler-rotations-in-unity).
- Measurement JSON files preserve the user's raw coordinates, correctedT3 provenance, the superseded wrong-feature sample, rough physical height and deferred horn-side discrepancy. Do not silently replace one source frame/pose with another.
- Earlier bench notes/CSV remain in Arduino/08_*; they supply reference pulses and observed software-endpoint paths, not angle-to-pulse calibration or an IK workspace.
