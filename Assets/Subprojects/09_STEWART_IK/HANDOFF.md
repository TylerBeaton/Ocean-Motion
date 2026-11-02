# Resume here — physical calibration follows the accepted visual IK

Updated 2026-11-02 (America/St_Johns). User plans to continue tomorrow, 2026-11-03. No reminder scheduled. Keep the walkthrough concise; preserve completed work and avoid repeating the original servo-by-servo neutral hunt.

## Working checkpoint

- Branch: `feature/unity-fusion-telemetry`; origin: `https://github.com/TylerBeaton/Ocean-Motion.git`.
- Pushed `ffeaadd`: Python offline IK; `83ea975`: manual Unity IK preview; `1155853`: processed boat → visual IK, gain controls and tests.
- Unity 6000.5.7f1 / URP 17.5.0. Archived SP07 scene freshly compiled/loaded with 13 roots and no missing scripts before integration. Preserve earlier scenes, firmware and the project's undecided final actuator architecture.
- User visually accepted both manual IK and processed boat motion. Accepted gains: **translation 0.02 / rotation 0.45**. Heave scales metres; rotation scales SP06's signed neutral-relative degrees before quaternion construction.
- Menus: **Ocean Motion → SP09 → Open Boat Motion IK** or **Open IK Preview**. Boat scene starts Calm and uncalibrated. Play, settle, Calibrate boat neutral, then Bow/Side/Crossed. Pause / Neutral holds the visual target while the boat continues.
- Gain range: translation 0..0.1, rotation 0..1. The Game panel exposes rotation gain. Fixed the `.1f > .1` float/double bug; exact gain endpoints are tested. A rotation gain of 0.45 is five times the user's working 0.09.
- Visual envelope remains ±10 mm per translation input and ±5° per rotation input. Whole-pose rejection hides linkages; do not silently clamp. SP06 heave already caps at ±250 mm, hence 0.02 → ±5 mm. Raising gains can cause deliberate rejection; this is not evidence of physical travel limits.
- Latest live run: 20.745 simulated seconds, 2,927 accepted render frames, zero rejected poses, maximum mapped heave 5 mm and rotation 4.095°. Invalid/uncalibrated/disabled/stale source, recovery and pause-neutral passed. Manual preview regression also passed.
- Reports: `Reports/unity-boat-ik-check.json`, `unity-boat-ik.png`, `unity-preview-checks.json`, `unity-playmode-check.json`. C# matches 15 Python fixtures and passes 181 continuous small poses. No physical output or angle-to-PWM transport exists.

## Tomorrow's first task

**Reconcile physical horn ball-joint geometry and neutral height before creating the angle-calibration sketch.** User deferred these measurements until the visual setup was ready; it now is. Begin with the real assembly, not another Fusion-only measurement exercise.

1. Establish the current hardware/sketch/power state with the user. Support the plate; wiring or uploads require servo power off. The last physical power-off and currently uploaded sketch were not confirmed in this chat. Do not issue movement commands based on an assumed state.
2. Identify the same shaft-point datum P used in `Measurements/servo-shafts-fusion.json`. Measure the actual horn ball-joint centre's offset **along the shaft axis**, relative to P. Solver definition: `d = dot(H - P, axis)`, with axis pointing P→Q. All six CAD offsets are approximately +6.85 mm, but the physical joints are mounted on the opposite horn side. They are known unverified placeholders: **do not simply negate 6.85 mm**. Record the physical side/datum and dimensions clearly; use per-channel values if hardware differs.
3. Confirm the actual shaft-axis-to-ball-centre radial radius (reported 35 mm) and rod joint-centre-to-joint-centre length (reported 184.5 mm). They were provisionally applied to all six; check whether assemblies match.
4. At the previously recorded physical neutral, refine shaft-centre-plane → top-ball-centre-plane height. Previous rough value: 165.4 mm. Shaft datum Fusion Z=16 mm makes provisional top centre Z=181.4 mm. Confirm level and horizontal/yaw alignment rather than silently treating the CAD height 191.712 mm as physical neutral.

Record measurements/provenance in Measurements/, update the runnable `offline-geometry.json`, regenerate/review Python report and Unity reference fixtures, and rerun numerical/visual checks. `build_geometry.py` restores CAD placeholders when regenerating; do not overwrite newly measured corrections with `--force`.

## Following physical stages — still required

1. Build a bounded, manual **angle-to-pulse calibration** sketch. Measure each horn's signed physical angle at known pulse values, establish the configured radial reference/physical branch and fit/check per-channel pulse maps. Existing raise signs do not establish right-hand angular signs about P→Q; existing 1000..2000 µs observations do not define degrees.
2. Enforce calibrated per-channel pulse/angle bounds with margins and whole-pose validity; add gradual movement, explicit enabling, stop/watchdog behavior. Verify joint articulation, collision constraints and power headroom (supply was reported 5 V / 3 A; capacity under simultaneous load remains unverified). Neutral, PWM-off and actuator power-off are distinct states.
3. Use a supported, unloaded assembly for small static neutral/heave/pitch/roll tests. Confirm physical direction, geometry/pose agreement and stop behavior before streaming boat animation. A valid numerical IK solve alone does not validate mechanical workspace, load, singularities or travel.

The user authorized moving toward physical bring-up, but deferred actual work until tomorrow. No hardware was commanded during Unity integration. Do not start animated hardware output directly from this checkpoint.

## Established hardware facts — reuse these

- UNO R4 WiFi; PCA9685 address 0x40, 50 Hz; MG996R-class servos; failed servo 5 replaced.
- Channels/shared-side pairs: A=[0,5], B=[1,2], C=[3,4]. Mapping confirmed; no need to repeat the earlier label confusion.
- Reference pulses channels 0..5: **[1500,1600,1550,1600,1500,1500] µs**.
- Observed horn raise signs relative to increasing pulse: **[+1,-1,+1,-1,+1,-1]**.
- All six individual trials reached 1000..2000 µs software bounds with other servos neutral; quiet/clear normal returns reported. Group endpoints: raise [2000,1000,2000,1000,2000,1000], lower inverse. Quiet, clear and neutral return confirmed. These are configuration-specific observations, not mechanical extrema or a full IK workspace.
- Prior sketches/results: `Arduino/08_NEUTRAL_HOLD`, `08_EXTENT_TEST`, `08_GROUP_TEST`, `08_PAIR_CALIBRATION/CALIBRATION.md`.

## Geometry and software boundaries

- Corrected six P/Q shaft measurements and final T0..T5 correspondence are already recorded. Servo i connects to Ti; corrected T3=(76.149,25.792,191.712) in raw CAD. Do not restart top-anchor tracing without new evidence.
- Raw CAD top centre (0,0,191.712); local anchors have Z=0. Horn CAD H data establishes radial references, not physical servo zero.
- Unity X rotation=pitch, Y=yaw, Z=roll, Y translation=heave. Unity metres/quaternion → Fusion mm via basis B swapping Y/Z; `R_F=B R_U B^T`. Do not swap wrapped Euler components as a pose contract.
- Python: `Offline/stewart_ik.py`, `run_ik.py`, `validate_ik.py`, `build_unity_fixtures.py`; C# pure core: `Scripts/Core/RotaryStewartModel.cs`.
- Integration adapter: `Integration/ProcessedBoatIkBridge.cs`, consuming unchanged SP06 `LatestCommand`. Preview accepts metres/quaternion and remains independent of boat/serial code. Boat scene has no `SerialController` or `MotionSerialTransport` components.
- Notion was updated on 2026-11-02: [Project overview](https://app.notion.com/p/38fa073d8e27803da7dad64a5c2fe05b), [Update 09](https://app.notion.com/p/3b8a073d8e27813bb28feaaf9c20da61) and [Update 08](https://app.notion.com/p/3b8a073d8e2781009ecacbce4a063c36) now record the accepted Unity integration, gains, test results, publication and next physical steps. Update 09 is In progress; physical output/calibration and the final architecture gate remain open. This handoff is the detailed repository resume source.

## Suggested next-session opening

“The visual boat-to-IK stage is complete and pushed. Today we start by checking the real horn ball-joint offsets and neutral height, then calibrate angles against pulses. Let’s first confirm the prototype is supported and identify the shaft reference point for the offset measurement.”

Inspect Git status/branch/upstream before edits; preserve unrelated work. Unity must be closed before batch checks; do not close an active user session or discard scene edits. The next model should read this handoff and SP09 README first.
