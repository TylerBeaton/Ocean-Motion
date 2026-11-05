# Expanded boat IK range (OI3)

Open **Ocean Motion → SP09 → Open Boat IK Range Test**. Current OI3 scene is rebuilt directly from the committed visual baseline. Superseded local OI2 probes were pruned on2026-11-05. Protocol OI3 requires one upload of `Arduino/09_IK_RANGE_PROBE/09_IK_RANGE_PROBE.ino`. After that, gain and allowance changes need no compilation/upload.

## First physical stage

User qualitatively accepted±1°,±2°, then±3° separate pitch/roll sweeps and boat motion at0.40 rotation gain. These observations do not validate all combined poses. Start this new scene at **heave0.002 / rotation0.40 / ±150us**.

1. Servo supply off, supported plate, Unity/Serial Monitor closed: upload the range sketch. Query? at115200/Newline: `OI3,STATUS`, driver_ready1,armed0,pwm_on0, six zero progress/target values,active_limit_us25,hard_offset_limit_us250. Close Serial Monitor.
2. Open the new menu, Play, set port, Connect. Stay Calm and settle; Calibrate boat neutral. Connect/HELLO reset selected allowance25us.
3. Select±150us, keepheave0.002, use **Rotation0.40** preset button. With supported unloaded plate, restore servo power and explicitly ARM at Calm/board neutral. Test Bow, then Side and Crossed while observing.
4. After acceptance, select Calm / Pause-Neutral and wait for neutral/disarm before changing to±175us androtation0.45. Software sampled a172us peak at0.45, so175 has little margin; some combinations may still HOLD. Choose±200us only as a subsequent observed stage, never automatically in response to rejection. Gains change at neutral/disarmed only. Resume boat inCalm before explicit rearm.

## Controls and limits

OI3 offers25..250us in25us increments. No automatic gain increase, pulse-limit expansion, individual clipping or pose scaling. Per-Arm limit echo must match. Runtime profile is captured at Start so editing the Inspector mode cannot swap protocol/hard limits while armed. All six offsets must fit the selected allowance; absolute1000..2000us guard stays. Hard250us is a software ceiling within earlier individual pulse observations, not a validated combined workspace. Firmware ramp remains1us/channel/20ms,250ms watchdog HOLD. Home timeout7s covers the maximum5s neutral return after reconnect from250us, even when selected allowance resets25.

Rotation gain control reaches1.0 in this new profile, with±0.05 buttons and0.40/0.45/0.50 quick selections. Gain1.0 means the full processed angle; it is not a promise the platform can reach every simulation pose. Mapped±5° and±10mm pose envelope, IK reachability and pulse limits still apply. Translation gain remains0..0.02. Same provisional−5mm horn offset,11us/degree map, assumed neutral/branch and approximate plate geometry remain. Colour view and pulses still share the same solver result. Distinguish requested numerical pose from actual held hardware commands in the right panel.

## Evidence and diagnostics

The repeated stops reported during the earlier0.45 test were explicitly identified by the user as **whole pose rejected: channel1 needs100.2us (limit100us)**;0.40 worked. Do not infer that a larger supply is the fix for those stops. A separate `Device not configured` exception was seen in the serial log, without an established cause. User reported~0.3A/5V, estimated supply maximum6A; no confirmed current-limit or voltage-sag event. Project runInBackground is already1. Keep USB failures, watchdog/frame stalls, source pose bounds, IK failure and pulse-limit rejection distinct.

Firmware compiled UNO R4 WiFi:66752bytes flash/9472bytes RAM. Host tests passed all ten allowable limits, asymmetric/random six-offset requests, hard/active bounds,≤1us ramp, malformed/session/sequence handling, watchdog and neutral recovery after allowance reset. Fresh SP07 baseline passed. No-USB wave characterization at gain0.45/allowance200us produced peak172us plus39 rejected frames in this run; tests checked both shared accepted IK commands and HOLD on rejection, neutral-only config, echoed allowance, immutable runtime profile and no auto-rearm. This is numerical characterization, not a claim all waves fit or hardware validation. See `Reports/boat-ik-range-check.json`. Physical expanded range observations pending; no USB port opened by tools.

Historical verification before pruning: the earlier OI2 boat scene passed its Bow/Side/Crossed and stop-gate regression after OI3 was introduced. That superseded local scene is no longer required or retained.

## Current follow-up (2026-11-05)

Shared pose allowance1..10° is now explicit in the left panel; default/reset5°. User accepted separate static sweeps through±8° at250us, and reports Bow/Side working atrotation0.68 with Crossed dropouts. Confirm the exact rejection and current heave/pose/pulse settings; combined motion is not established by individual-axis tests. Superseded physical-probe code/scenes were pruned; current factories and tests are self-contained.
