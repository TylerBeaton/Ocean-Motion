# Handoff — initial servo pair proof complete, 2026-10-31

User prefers short, one-stage-at-a-time guidance. They requested pushing these sketches and calibration results to the existing branch before proceeding to assembled-linkage testing.

## Latest physical checkpoint

Read CALIBRATION.md for the definitive observations. Neutral by PCA9685 channel 0..5: [1500,1600,1550,1600,1500,1500] µs; raise direction signs [+1,-1,+1,-1,+1,-1]. User reconfirmed shared sides A=[0,5], B=[1,2], C=[3,4]. Faulty physical servo 5 was replaced before restart. All three pairs moved up/down quietly with disconnected pushrods at offsets ±25 µs and returned to saved neutral, confirmed by serial status.

Latest status at 11:03:15: PAIRED C selected=4 offset_us=0 step_us=25. Channels 3/4 both enabled, command_us=level_us=1600/1500, directions -1/+1. Agent then instructed user to support the plate, send x and turn servo power OFF, leaving pushrods disconnected. Shutdown completion has not been confirmed.

No physical extents, assembled neutral pose, calibrated angle-to-pulse maps, full workspace or IK integration have been established. The values above are visually aligned horn reference commands, not actual angle measurements. Preserve that distinction.

## Next stage

Current pair-only sketch disables the other four channels and loses calibration on pair change/reset/x. Before assembled-platform travel testing, create a small separate next-stage sketch that holds all six at their recorded references and validates that foundation before adding pair movement. Keep existing sketches as independent stage records. Do not instruct the user to connect and move the whole platform while relying on four unpowered servos.

First confirm supported plate, disconnected pushrods and power OFF before upload/preparation. Then guide verification of all-six reference positions and quiet holding, supply/distribution suitability, and assembled neutral geometry without preload. Only after that proof proceed to supported linkage travel checks with conservative staged bounds. Never seek hard stops or stalls. No new controller stage is implemented yet.

## Current files and commands

Use 08_PAIR_CALIBRATION.ino in this folder. Original ../08_PAIR_A_CALIBRATION/ is preserved as the initial two-servo experiment. Current sketch defaults to A with all PWM outputs disabled. Serial 115200 / Newline; one character per submission.

- a/b/c select pair only when outputs disabled; reset neutral and direction signs.
- 0..5 select a channel belonging to active pair; e enables it at nominal1500 µs.
- g selects coarse25 µs; f selects fine5 µs in individual or paired mode without moving.
- +/- make one step and hold. Individual movement invalidates that servo's saved neutral.
- r reverses individual direction without motion; s saves level in RAM.
- p enters paired mode after both levels saved; x disables ALL PWM and clears calibration; ? prints status.

Current absolute window1400..1650 µs; paired offset±50 µs. Initial ceiling1600 was raised after finding channel1 visually level at1600. Only±25 offsets have been physically checked; software bounds are not measured safe travel. Reset/upload clears calibration; documented settings are not auto-loaded. No automatic sweep/neutral jump, position feedback or serial-disconnect shutdown.

## Hardware and verification

UNO R4 WiFi, PCA9685 at0x40 /50 Hz, Deegoo/TowerPro-labelled MG996R-class servos with unverified authenticity, supply reported5 V /3 A. All-six loaded operation remains unvalidated. Old servo5 repair was abandoned after swap testing and inconclusive inspection; do not resume diagnosis unless asked.

Final window revision compiled for arduino:renesas_uno:unor4wifi (core1.6.0, Adafruit PWM Servo Driver3.0.3), using65868 bytes flash /9292 bytes RAM. Temporary host checks with simulated PWM passed all-pair channel mapping, startup-off behavior, calibration prerequisites, pair-switch lockout, coarse/fine movement, reversal, bounds, no partial outputs on rejection, exact B restoration and return to neutral. User uploaded and verified final ceiling before B/C paired checks. Tests do not verify physical position or load capability. No Unity assets changed; existing successful Unity compilation log inspected.

Branch feature/unity-fusion-telemetry, origin fetched and synchronized before publication. Only the two Arduino calibration directories belong to this work. Preserve unrelated work and follow root AGENTS.md. See Git history for the publication commit.
