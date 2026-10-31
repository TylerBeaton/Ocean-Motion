# Bench observations — 2026-10-31

Latest restart values supersede the earlier 2026-10-29 provisional values. User reconfirmed shared-side pairs A=[0,5], B=[1,2], C=[3,4], with labels matching PCA9685 channels. Faulty physical servo 5 was replaced before these checks.

## Provisional neutral and raise direction

| Channel | Pair | Visually level pulse (µs) | Pulse sign that raises attachment point |
| --- | --- | --- | --- |
| 0 | A | 1500 | +1 |
| 1 | B | 1600 | -1 |
| 2 | B | 1550 | +1 |
| 3 | C | 1600 | -1 |
| 4 | C | 1500 | +1 |
| 5 | A | 1500 | -1 |

These are user-observed horn reference positions and commanded PWM values, not measured horn angles or a verified assembled-platform neutral pose. No angle-to-pulse mapping has been established. Values are documented here but are NOT automatically loaded by the current sketch.

## Disconnected pair proof

Plate supported, tested pushrods disconnected, no payload test. Each pair was tested at one 25 µs offset in each direction. User reported both attachment points moving in the intended direction and settling quietly. All pairs returned to offset 0, confirmed by serial status.

| Pair (channel order) | Neutral commands (µs) | Up at +25 offset (µs) | Down at -25 offset (µs) | Final neutral status time |
| --- | --- | --- | --- | --- |
| A (0,5) | 1500 / 1500 | 1525 / 1475 | 1475 / 1525 | 10:31:13 |
| B (1,2) | 1600 / 1550 | 1575 / 1575 | 1625 / 1525 | 10:53:38 |
| C (3,4) | 1600 / 1500 | 1575 / 1525 | 1625 / 1475 | 11:03:15 |

Actual motion/hold duration was not measured. No buzzing, binding or settling issues were reported for these checks. Servo models are Deegoo/TowerPro-labelled MG996R-class units; authenticity and performance specifications are unverified. Supply reported as 5 V / 3 A; simultaneous loaded operation of all six is not validated.

Initial pulse ceiling 1600 µs was reached during channel 1 neutral alignment. Ceiling was increased to1650 µs, uploaded and verified in serial status before B/C paired checks. Minimum remains1400 µs, pair offset window ±50 µs, tested offset only ±25 µs. Software bounds are not physical travel limits. Current sketch loses calibration on reset/x/pair change, and only enables the selected pair.

## Next stage prerequisites

- Preserve these per-channel settings in the next controller stage; verify all six reference positions with the plate supported and pushrods disconnected.
- Before assembling and moving the linkage, provide coordinated holding of all six at neutral. Current pair-only sketch disables other channels; do not rely on it to support a fully connected platform.
- Establish assembled neutral geometry without forcing rods into place; verify appropriate supply/distribution for simultaneous operation and available physical power cut-off.
- Then test linkage travel in deliberate stages, starting small, recording last comfortable positions and conservative margins. Check return to neutral, joint articulation and interference. Do not seek a hard stop/stall.
- No physical extents, full-pose workspace, IK calibration or actuator limits have been established by the disconnected checks above.
