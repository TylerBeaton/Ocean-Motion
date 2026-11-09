# Adjustable pose allowance and sweeps to10°

Current receiver update: use OI5 firmware for the current boat/manual scenes (offset allowances to500µs with absolute1000..2000µs bounds). Motion speed is selectable in the boat scene; static/manual sweeps retain50µs/s. Earlier OI3 evidence below remains historical.

User resumed after the shelved perspective investigation and reported gain>0.50 rejected as beyond the visual envelope. They authorized explicit runtime pose bounds, static4°/5° checks, then requested controls through±10° in one update to avoid repeated builds. Servo power off/Unity and Serial Monitor closed was confirmed before preparation. **No Arduino upload required: keep OI3 `09_IK_RANGE_PROBE`.** The firmware±250us cap,1us/20ms ramp and250ms watchdog are unchanged.

## Boat controls (existing range scene)

Open **Ocean Motion → SP09 → Open Boat IK Range Test**. The left panel now shows **Shared pose allowance: ±5° per axis / ±10mm**, with1..10° buttons. It gates the same pose used by the coloured preview and physical mapping. Selection requires OI3 and disarmed commanded board neutral. Default is5°; new connection/disconnection resets5°. Gain is a scale factor; angle allowance is a distinct shared-target bound, not a pulse limit or measured mechanical workspace.

To change: Calm→Pause/Neutral, wait for board neutral/disarm; choose the angle allowance and gains; Resume boat inCalm; explicitly ARM. Choose a pulse allowance separately on the right. Whole-pose IK, numeric finite-input, angle/translation and pulse checks all remain. Larger angle allowances can reveal the next pulse/IK limit. No source-physics changes, added filtering, individual clipping, automatic gain reduction or automatic rearm.

Errors now distinguish **Rotation pose allowance exceeded**, with requested P/Y/R and selecteddegrees, from **Translation pose allowance exceeded**, with requestedmm and10mm bound. The old “visual test envelope” wording was misleading because it gated hardware too. Legacy visual scenes keep their default5° behavior; their shared pure mapping helper's default remains5°.

## Static controls and automation

New independent **Ocean Motion → SP09 → Open IK Pose Range Sweep**, scene `Scenes/SP09_IkPoseRangeSweep.unity`, copies the preserved sweep archive into an OI3 scene. All manual angular sliders reach±10°, and sweep targets include0.5/1/2/3/4/5/6/7/8/9/10°. Handles in this scene also obey a hard±10° per-axis /±10mm source envelope. The current OI3 static scene is independent of retired physical probes; committed visual/numerical archives remain available.

**Precheck selected path** works before ARM and reports pitch and roll feasibility at the selected pulse allowance. It samples both directions across the full requested range. Starting a sweep independently runs the same check; an oversized request is refused before source movement. One pass only: neutral→positive→neutral→negative→neutral at0.5°/s, with2s dwell after commanded offsets settle within1us; completion requests gradual neutral/disarms. Per-phase timeout scales with amplitude, so5–10° paths are not incorrectly stopped by the previous12s timer. Manual sliders are locked while running. Reset/HOLD/Home/OFF/source/connection/watchdog stop cancels; no auto-resume. Reset cancellation survives disabling/re-enabling the sweep component.

First physical check: servo supplyoff/supportedplate; open new static scene, Play/Connect, select±150us and±4°→Precheck. Restore supply and ARM at exact source/board neutral, Sweep pitch, then rearm at neutral and Sweep roll. After acceptance, use±5° with the same150us allowance. Observe throughout. Further degrees can be selected and prechecked at neutral without another build/upload or chat round trip; retain progressive operator observation. These software settings do not validate physical angles.

## Expected command-space limits of the provisional model

Independent Python-core path sampling, same provisional geometry and11us/degree estimate, gives the following maximum absolute pulse offsets across each single-axis path. Values exclude mixed heave/yaw/roll/pitch effects, tolerances and physical model error:

| Requested amplitude | Pitch peakµs | Roll peakµs |
| --- | --- | --- |
| ±4° | 110.2 | 105.3 |
| ±5° | 139.6 | 132.9 |
| ±6° | 169.8 | 161.3 |
| ±7° | 201.0 | 190.4 |
| ±8° | 233.4 | 220.4 |
| ±9° | 267.2 | 251.5 |
| ±10° | 302.6 | 283.8 |

Therefore the±10° controls do **not** guarantee10° hardware travel: ±9/10° paths currently exceed the unchanged250us cap and are correctly refused. C# preflight first failures at−10° were pitchchannel1≈296.2us and rollchannel3≈279.4us; these are the first rejected samples, not full-path maxima. Approximately±8° fits numerically as separate paths at250us; physical acceptance has only been reported through±3° so far. Combined boat poses may require more than these single-axis figures. Do not raise firmware bounds simply to force a precheck to pass.

## Verification

Fresh SP07 archived baseline passed. Pure mapping tests cover default5° compatibility, exact1..10° boundaries, over-limit requests, nonfinite/invalid allowances and translation rejection with a valid gain. New static scene passed4/5° pitch/roll prechecks, a full simulated±5° pitch cycle, requested10° startup refusal according to pulse preflight, Scene-handle11° rejection, neutral recovery without rearm and USB disabled. Report `Reports/ik-pose-allowance-check.json`. Actual boat OI3 checks passed with armed pose-setting lock, neutral selection, connection reset5° and existing wave/rejection paths. Physical4–10° tests pending; no USB port opened by tools. Firmware unchanged.

Original OI2 sweep Play regression also passed, including reset cancellation after disabling/re-enabling the sweep component. No USB port opened.

## Later observations and cleanup

User subsequently accepted separate pitch/roll through±8° at250us;±9°was rejected. This does not establish mixed-wave limits. Superseded uncommitted OI2/manual probes and their regression runners were pruned on2026-11-05; the current static scene builder no longer requires them. CurrentSceneChecks verifies both scenes and clean rebuilds.
