# Ocean Motion checkpoint — offline IK ready for Unity preview

> Superseded resume checkpoint: see [SP09 current handoff](../../Assets/Subprojects/09_STEWART_IK/HANDOFF.md). The Unity preview and processed boat connection are now complete and accepted; next is physical geometry reconciliation and angle-to-pulse calibration. The historical details below are retained for provenance.

Updated2026-11-02. User ran the Python scripts, reported everything looks good, and requested publishing all work before proceeding. Keep replies concise and avoid restarting completed tests/measurement collection without cause.

## Current stage and next gate

Offline mathematical foundation is implemented in `Assets/Subprojects/09_STEWART_IK/Offline/`; current guide/report are in that subproject's README and Reports/. User has exercised the scripts successfully. Next is a LIVE UNITY VISUAL PREVIEW of solved geometry, with hardware output disabled. Do not interpret "online" as permission to stream these uncalibrated angles to servos.

Before adding C# runtime components or a new scene, verify the last known-good archived scene compiles per AGENTS.md. Existing successful compilation log evidence was inspected previously, but no fresh archived-scene compile was performed during the pure-Python stage. Project stays Unity6000.5.7f1/URP17.5.0. Preserve SP07/Final IK and all earlier archives; build the next preview within SP09. No C# adapter/scene/transport is implemented yet.

## Current geometry

All raw/corrected data, provenance and derived frames live under `Assets/Subprojects/09_STEWART_IK/Measurements/`.

- Use corrected servo-shafts-fusion.json, not its superseded wrong-feature archive. ShaftP/Q Z16mm, separation12.5mm, directions0/1≈(-0.866025,-0.5,0),2/3≈(+0.866025,-0.5,0),4/5=(0,1,0).
- Top coordinates final, including T3=(76.149,25.792,191.712). User explicitly clarified servo i→Ti; no further top permutation/tracing is needed. Earlier top-mapping confusion is resolved.
- CAD control centre(0,0,191.712); local top anchors have Z0. Control axes chosen parallel to Fusion global at neutral, not assumed equal to the imported component transform.
- Physical shaft-plane→top-joint-plane height roughly165.4mm, yielding provisional global neutral centre(0,0,181.4) using shaft datumZ16. Raw CAD positions remain unchanged. Horizontal position/yaw/level and measurement tolerance not independently characterized.
- Actual reported horn radius35mm and rods184.5mm provisionally apply to all6.
- CAD H points establish about+6.85mm axial offset along each shaft and CAD radial references. Physical ball joints mount on the OTHER SIDE of the horn. User explicitly defers remeasurement; keep offsets configurable/UNVERIFIED. Do not simply negate6.85mm or treat CAD horn angles as physical neutral angles. Physical axial offset remains unknown/null in source data.
- Neither horn-angle/PWM mapping nor calibrated joint-angle bounds exists. Earlier1000..2000us observations are not angle limits.

Editable `Measurements/offline-geometry.json` is the runnable snapshot. `build_geometry.py` regenerates CAD placeholders from raw sources and refuses existing output without --force; avoid overwriting later physical corrections. Numeric solver output NEVER enables hardware even if metadata is edited.

## Offline solver and verification

Analytic arbitrary-axis horn-circle solver: C=P+d*a, H=C+r*(u*cos(theta)+(a×u)*sin(theta)), |T-H|=L. Two candidates; provisional neutral branch nearest CAD radial reference, then pinned. Unwrap near previous angles. Reject entire pose without partial output for unreachable/indeterminate/tangent legs, configured angle-limit or optional continuity-step violations; verify reconstructed rod lengths.

Python3.9+ stdlib only. `run_ik.py` accepts neutral-relative Fusion mm/quaternion or offline Unity JSON metre/quaternion. Proposed basis B maps Unity(x,y,z)→Fusion(x,z,y), rotations R_F=B*R_U*B^T. Established Unity X=pitch,Y=yaw,Z=roll,Y displacement=heave. No wrapped Euler contract. Live scene orientation/scale alignment still requires visual validation. No CSV player, serial/PWM bridge or current feedback.

Tests/results already passed:
- 15 unit tests, including250 deterministic small random poses, independent rod/radius/plane invariants, arbitrary-axis and signed offsets, both roots, bad/tangent/unreachable inputs, optional angle bounds, whole-pose/step rejection,2-turn branch continuity, Unity basis/quaternion/rotated-neutral tests, editable offsets and disabled hardware output.
- 15 report fixtures: neutral,±1mm translations and±1° rotations for all6 axes, combined pose, rejection of1m heave.
- 181-sample continuous tilt/heave path: max consecutive motor-angle change0.165692°, numerical rod residual≤5.68e-14mm. Numerical consistency is NOT physical measurement accuracy or workspace validation.

Angles are signed about P→Q from the configured CAD radial vector, not Arduino Servo.write angles or PWM. No physical branch validation, collision/joint articulation/full Jacobian workspace proof or physical calibration completed. User defers horn remeasurement until setup ready; respect that scope.

## Completed hardware bench experiments

UNO R4 WiFi, PCA9685 address0x40 at50Hz, Deegoo/TowerPro-labelled MG996R-class units, unverified authenticity; supply reported5V/3A. Failed servo5 replaced. Actual shared-side pairs A=[0,5],B=[1,2],C=[3,4]. Neutral pulses channels0..5=[1500,1600,1550,1600,1500,1500]us; horn raise signs=[+1,-1,+1,-1,+1,-1].

- Disconnected pair±25us motion/return proofs completed; CALIBRATION.md holds references/directions.
- Supported unloaded assembly, rods attached: all6 neutral holding quiet and visually level per user; no timed endurance/electrical data.
- Individual trials with five fixed at neutral reached1000..2000us software guardrails for all6; user reported quiet/clear movement and normal returns. Records in Arduino/08_EXTENT_TEST/.
- Group interpolation raised path endpoint[2000,1000,2000,1000,2000,1000],lowered reverse. Both±100% recorded; user reported no buzzing/interference and normal return to neutral. Records in Arduino/08_GROUP_TEST/. No actual height/tilt measured; not an IK pure-heave or full workspace proof.
- Neutral-hold, extent and group firmware each compiled for UNO R4 and passed persistent simulated PWM/Serial/clock tests. Physical power-off after the session was not explicitly confirmed. No further motion was commanded by the agent.

Support the plate before x (releases all PWM); supplyOFF before upload/wiring. Reset starts outputs OFF and does not physically home. Serial disconnect holds last commands. No measured current/load/thermal capacity, stall detection or actuator-power emergency stop integration yet.

## Publication

Repository: origin https://github.com/TylerBeaton/Ocean-Motion.git, active branch feature/unity-fusion-telemetry. Initial pair calibration was35c9ec0. User explicitly requested pushing the remaining bench and offline IK work. See Git history for the new publication commits; preserve unrelated future work. Notion Updates08/09 already contain the earlier hardware results/IK checklist; they have not yet been updated for this new offline implementation.
