# Manual rotary-servo pair calibration

**Latest result:** all three pairs passed the disconnected ±25 µs direction/return check on 2026-10-31. See [CALIBRATION.md](CALIBRATION.md) for the six current reference pulses and direction signs. Mechanical travel limits are still unknown. Before assembled-platform movement, the next controller stage must support all-six neutral holding; the current pair-only sketch does not.

Independent Update 08 bench experiment; no Unity/IK input or final actuator architecture decision. Hardware: UNO R4 WiFi, PCA9685 0x40 / 50 Hz, Deegoo FPV MG996R-class servos, external supply reported as 5 V / 3 A. Six-servo loaded operation is not validated.

## Restart — 2026-10-31

User replaced the faulty servo and reconfirmed shared sides A=[0,5], B=[1,2], C=[3,4]. Numbers mean PCA9685 channels. Pair A is available again; startup selects A. Restart calibration for each servo, including the replacement. Prior visually level values below are historical observations, not automatically applied settings.

Use `08_PAIR_CALIBRATION.ino` here; the original `../08_PAIR_A_CALIBRATION/` sketch is preserved separately. Current sketch starts with outputs OFF and **25 µs coarse steps**. Each command is one character per Serial Monitor submission at **115200 baud / Newline** (CRLF also accepted). No automatic sweep.

| Command | Action |
| --- | --- |
| `a`, `b`, `c` | Select pair; requires outputs disabled; resets level and direction settings |
| `0`–`5` | Select a servo belonging to active pair |
| `e` | Enable selected servo at nominal 1500 µs; may move abruptly |
| `+`, `-` | One signed step, then hold |
| `g` | Coarse: 25 µs per step; no immediate movement |
| `f` | Fine: 5 µs per step; no immediate movement |
| `r` | Reverse selected servo direction; individual mode only; no movement |
| `s` | Save selected servo's current pulse as level in RAM |
| `p` | Enter pair mode after both enabled servos have saved levels; no movement |
| `?` | Print commands, pulses, saved levels, directions, offset and step size |
| `x` | Disable PWM on ALL 16 channels and clear calibration; does not cut supply power |

Step size can change in pair mode. Changing it does not move anything or change calibration. When returning to neutral, use the printed pulse/offset, not remembered command counts: one coarse step equals five fine steps. Pair selection resets direction signs to +1/-1; verify each physically. Individual movement clears that servo's saved level. Reset/upload loses all calibration. Values must be recorded externally.

## Initial proof, one stage at a time

1. Support the unloaded top plate. Switch servo power OFF and disconnect the tested pair's pushrods. Check labels against channels. Keep hands clear of horns/linkages. Use common ground and an accessible physical power disconnect.
2. Upload with servo power off. Resetting Arduino alone may leave previous PCA9685 output active until setup runs. Open Serial Monitor and send `?`; expect pair A, selected 0, both disabled, step_us=25.
3. Apply servo power. Select `0`, then `e` separately. Nominal 1500 µs is not calibrated level. Inspect settling and horn geometry. Check a coarse step only with horn clear; use `f` for precise alignment. Verify which sign raises the attachment point; `r` changes the sign convention without moving. Return to the desired neutral and `s` to save. Repeat for channel 5.
4. With both neutral commands saved and both directions checked, enter `p`. Check coordinated disconnected horn motion with one coarse step at a time. Stop after every command to inspect. Return to offset 0 by deliberate steps; no automatic return-to-level command exists.
5. Repeat for B and C only after completing and recording the previous pair: support plate, `x`, servo power OFF, prepare pushrods, select new pair. Do not force linkage connections into place.

Required initial proof: correct channel mapping, quiet individual settling, neutral/direction confirmation, coordinated small horn movement, reversible return to neutral. Then plan assembled linkage travel testing with all six neutral positions established and the top plate controlled/supported. Disconnected horn travel does not establish platform extents.

## Exploration bounds versus physical extents

Current absolute pulse window: **1400–1650 µs**. The initial ceiling was 1600 µs; it was raised by 50 µs after channel 1 appeared level at that ceiling during the restart. Pair offset window: **±50 µs from the saved neutral**, also restricted by each absolute pulse window. These are deliberately narrow exploration bounds, NOT measured safe mechanical limits. A command outside either window is rejected for both servos without partial movement. Switch to fine steps if a coarse command is blocked near a boundary.

25 µs steps need four commands for the same 100 µs change that previously took twenty 5 µs commands. This reduces submissions; it cannot guarantee finding a mechanical extent in four or five messages. Inspect after each coarse step and use fine steps near possible interference. Reaching the software boundary is not proof of a mechanical limit. Expand constants in small stages only after the previous stage is validated; do not substitute a generic full servo range.

Never drive into a hard stop or seek a stall. Record the last comfortable command before binding, buzzing, interference or excessive load; any final operating limits need margins. Pair-only limits do not establish the safe combined-pose workspace. Later IK needs calibrated angle-to-pulse mappings and whole-pose reachability, joint and collision checks rather than independent output clipping.

`x` can release holding torque and let the plate fall; support it first. Serial disconnection leaves the last command holding. These servos do not provide position feedback to this sketch; commanded pulse is not measured angle. Two servo I2C writes occur sequentially, not feedback-synchronized.

## Current pair B neutral — 2026-10-31

At 10:41:27 user serial status confirmed channel 1 current/saved level **1600 µs**, raise direction **-1**; channel 2 current/saved level **1550 µs**, raise direction **+1**, both enabled and step 25 µs. User reported quiet settling and visually level horns. These supersede older B observations. The previous software ceiling blocked further channel-1 pulse increase; no physical limit was inferred. The updated 1650 µs ceiling allows an initial ±50 µs paired check. Revised upload and the ±25 µs B paired proof are complete; final neutral status at 10:53:38 confirmed 1600/1550 µs. Upload clears calibration. No preset or automatic neutral jump was added.

## Historical observations — reconfirm during restart

### Restart result: pair A — 2026-10-31

Revised sketch uploaded by user. Channels 0 and replacement 5 settled quietly and appeared level at 1500 µs. Raise directions verified +1/-1 respectively. With pushrods disconnected, both attachment points rose quietly at offset +25 µs (1525/1475) and lowered quietly at -25 µs (1475/1525). Final serial status 10:31:13 confirmed paired offset 0, both current/saved commands 1500 µs and step 25 µs. No issues reported; actual test duration not measured. This completes initial disconnected pair-A motion proof, not a measurement of physical extents. B/C restart checks subsequently passed; see CALIBRATION.md.

- 2026-10-29: channel 0 settled quietly near 1500 µs; increased pulses raised its attachment point. Later level was saved at 1505 µs; neutral was not conclusively reconciled.
- Channel 1: provisional neutral 1565 µs, raise direction +1. Channel 2: provisional neutral 1560 µs, raise direction -1. User reported quiet disconnected horn motion at offsets ±25 µs and return to offset 0. Test duration not measured.
- Channels 3 and 4: each appeared level at 1500 µs, with raise direction +1 after reversing channel 4's initial sign. Pair test was interrupted by uncertainty about physical labels/shared sides. No mechanical extents established.
- Fault followed old physical servo 5 across channels 0/5; channel 0's servo held on both. User requested replacement; inspection and partial continuity check did not identify a repair. User reports replacement installed on 2026-10-31 and confirmed original shared-side map.

For each new test record: channel IDs, model/supply/support configuration, neutral pulses, direction signs, step/window settings, test duration, both pulses at last comfortable positions, observed issues and return-to-neutral result. Keep mechanical linkage limits distinct from disconnected horn checks and software bounds.

## Software verification

Current all-pair coarse/fine revision compiled for UNO R4 WiFi (core 1.6.0, Adafruit PWM Servo Driver 3.0.3). Simulated PWM checks passed for all three pair maps including channel 5, no startup pulses, prerequisites, switch lockout, coarse/fine movement in either mode, direction reversal, bounds and rejection without partial output. These checks do not verify physical motion. Unity assets are unchanged; existing successful compilation evidence was inspected. User uploaded the 1650 µs-ceiling revision and completed the disconnected ±25 µs checks for all three pairs. Actual mechanical travel remains unmeasured.
