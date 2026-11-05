# Ocean Motion — current resume point

Updated2026-11-05 (America/St_Johns). Branch `feature/unity-fusion-telemetry`, this checkpoint packages the retained OI3 integration, current scenes and November5 trim for publication. Use the latest branch commit for the published state.

## Current setup

- **Boat:** Ocean Motion → SP09 → Open Boat IK Range Test.
- **Static/manual:** Ocean Motion → SP09 → Open IK Pose Range Sweep.
- **Firmware:** Arduino/09_IK_RANGE_PROBE, protocolOI3. Both current scenes start USB disabled. Keep servo supply off for upload; support plate before PWM OFF. Never open USB from agent tests.
- Same solved target drives coloured linkage view and six independently mapped servo pulses. SP05 physics and SP06 processing remain unchanged. Rotary prototype does not close the final Update08 architecture gate.
- OI3 pulse offsets25..250us in25us steps, neutral/disarmed only, matching ARM echo. Physical saved neutrals `[1500,1600,1550,1600,1500,1500]us`; pulse-to-angle estimate11us/degree. Raw dataset `Measurements/provisional-physical-geometry.json` uses estimated−5mm axial offset,35mm radius,184.5mm rods and neutral top-centreZ181.4mm. Branch/zero/height/calibration remain provisional.
- Rotation gain0..1; heave gain0..0.02; shared pose allowance1..10° per axis(default/reset5°), translation±10mm. Gains/limits only change at disarmed board neutral. Entire invalid pose HOLDs; never clip individual channels or silently scale it.
- Firmware moves1us/20ms(50us/s max),250ms watchdog; explicit rearm. Home timeout7s covers max5s neutral return. Candidate/board offsets are target/ramped commands, not sensors.

## Latest physical observations

User qualitatively accepted static pitch and roll sweeps through±8° at250us;±9° was rejected, consistent with the provisional command map. No measured angle accuracy, load/current envelope or complete mixed workspace was established.

Latest reported boat gain0.68 works well for Bow and Side, but Crossed drops out. **Next diagnostic:** obtain the exact Last stop/rejection. Combined pitch+roll can exceed pulse/pose bounds even when each separate axis fits. Do not assume a power fault or automatically expand the cap. Previous advice was to return neutral/disarmed and tryCrossed0.60, then0.55 if needed, at unchanged limits. Verify current heave gain, pose allowance and selected pulse cap from the panel before continuing.

The earlier repeated0.45 stop was explicitly identified as channel1 needing100.2us with100usallowed;0.40 worked. A separate serial `Device not configured` warning occurred with no established cause. Supply reported5V/3A,~0.3A display, estimated6Amaximum(not confirmed); no confirmedCC/voltage-sag event. Run in Background already1. Separate USB failures, focus/frame watchdogs, pose guard and pulse-limit HOLDs.

## Deferred visual/centering investigation

User initially perceived Bow as dipping then merely leveling in both physical and coloured views. Screenshots at+4.01° and−4.20° showed all six candidate offsets reversing signs, ARMED at200us/rotation0.50/heave0.002, with board lag up to30us. Oblique-camera projection gives near-edge screen angles approximately1.3°,4.9°neutral,8.6°opposite pose. This supports a perspective explanation for the visual appearance, not proof of physical centering. Neutral-outline overlay and side-view camera remain proposed, not implemented. Do not recenter from screen-horizontal alignment or change ramp blindly.

## Command-space estimates (not physical validation)

| Single-axis amplitude | Pitch peakus | Roll peakus |
| --- | --- | --- |
| ±4° | 110.2 | 105.3 |
| ±5° | 139.6 | 132.9 |
| ±6° | 169.8 | 161.3 |
| ±7° | 201.0 | 190.4 |
| ±8° | 233.4 | 220.4 |
| ±9° | 267.2 | 251.5 |
| ±10° | 302.6 | 283.8 |

Controls reach10° but pulse/IK checks can refuse the path before movement. Static one-shot sweeps are0.5°/s with2s commanded-settled dwells, return neutral and disarm. User wants autonomous, concise testing without per-degree builds or repeated chat rounds.

## Cleanup and verification

On2026-11-05 user requested keeping latest work and discarding superseded local probes. Retain the two scenes above, five current runtime adapters, current setup/check scripts, current reports, provisional geometry/materials and OI3 firmware with self-contained test stubs. Original committed numerical/visual/SP07/SP08 archives are preserved. Temporary verification scenes are deleted by CurrentSceneChecks; no change to accepted runtime settings or existing scene GUIDs.

Current builders regenerate directly from the committed visual foundation/current boat scene, not retired probes. Reports remain software-only evidence. Cleanup verification passed: self-contained firmware host tests; both scenes loaded and rebuilt as temporary copies with no missing scripts; boat Play checks and full simulated±5°static cycle passed. No hardware USB was opened. Recovery archive for removed local work: `/private/tmp/ocean-motion-sp09-before-cleanup-2026-11-05.tar.gz` (temporary storage, not a permanent project dependency).

See README, BOAT_IK_RANGE_TEST.md and POSE_ALLOWANCE_TEST.md for current operation. Earlier Nov4 Notion checkpoint captures the perspective investigation; subsequent pose allowance,±8°acceptance and Crossed0.68 issue are newer. Notion has not been updated for the November5 cleanup; the user requested a commit/push after the final trim.

## Final trim and publication — November5

Removed the inactive SP05 telemetry canvas and SP06 comparison subtree from the current boat scene after verifying no retained component referenced them. Retained object/component payloads and scene GUID are unchanged; only removed UI and the scene-root list differ. Current scene builder applies the same trim. Boat YAML4579→1729lines. Offline report2233→185lines; default generation now summarizes fixtures, neutral references and continuity. `--full-report /tmp/ocean-ik-full-report.json` recreates detailed output exactly; generated full report matched the previous JSON. Original measurement datasets, parity fixtures and committed visual/milestone archives remain.
