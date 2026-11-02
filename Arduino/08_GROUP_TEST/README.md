# Coordinated horn raise/lower test

Update08 bench experiment following [individual endpoint observations](../08_EXTENT_TEST/OBSERVATIONS.md). UNO R4 WiFi, PCA9685 0x40 /50 Hz, Serial115200/Newline. Earlier sketches/results are preserved. No Unity/IK input.

User confirmed all six were quiet/clear during individual1000..2000 µs trials with five others at neutral, and all returned normally. That does NOT validate combined endpoints. Geometry, measured horn angles, collision/workspace limits and final IK bounds remain unknown. Supply reported5 V/3 A; six moving/loaded servos are not electrically validated. Stop for current limiting, resets, jitter, buzzing, strain or interference; do not drive to a stall/hard stop. Keep servo-power cut-off accessible.

## Meaning of progress

Each channel interpolates from its own reference to its recorded UP or DOWN endpoint using the same signed fraction. Positive progress raises all horn attachment points according to observed directions; negative lowers them. I2C writes occur sequentially, with no physical synchronization/position feedback. This command pattern is a hypothesis for platform lift/lower, not a pure-heave IK solution. The top plate may tilt or translate, and attainable motion may stop long before±100%.

| Channel | Neutral µs | +100% UP µs | -100% DOWN µs |
| --- | --- | --- | --- |
| 0 | 1500 | 2000 | 1000 |
| 1 | 1600 | 1000 | 2000 |
| 2 | 1550 | 2000 | 1000 |
| 3 | 1600 | 1000 | 2000 |
| 4 | 1500 | 2000 | 1000 |
| 5 | 1500 | 1000 | 2000 |

100% means the previously recorded software endpoints, NOT100% of physical height/travel or a proven safe joint pose. Every computed channel command remains1000..2000 µs. Do not call the resulting endpoints true platform minimum/maximum height without geometry/measurements.

## Setup and first proof

1. Unloaded plate, supported against falling without rigidly obstructing intended movement; no hands under the moving mechanism. With the current sketch at neutral, support plate, disable PWM and switch servo power OFF before uploading. Reset can release all holding torque; keep supports in place. Previous PCA9685 commands may remain until setup runs if only Arduino is reset.
2. Upload08_GROUP_TEST. Send `?` over USB; expect progress/target0, movement0, driver_ready1, references_valid1, all six disabled. Startup never enables motion or holding automatically.
3. Servo power ON; enable0..5 individually (`0`, `e`, `1`, `e`, etc.). Pause after each. Each writes its recorded reference without changing already enabled channels. Continue only if reference pose/holding is quiet and clear. All six must be enabled before group movement.
4. Send `+` ONCE. It ramps to+5% command travel, then holds. Wait for "Group target commands reached" and inspect actual motion/clearance/settling. No further target is queued during motion. First target pulses are [1525,1570,1572,1570,1525,1475] µs. Do not assume the plate moves vertically or remains level.
5. Only if clear, send `n` to return gradually to recorded neutral. Inspect actual return. Then check-5% with one `-`, followed by `n`. Complete and record this small two-direction proof before extending allowances.

## Commands

| Command | Action |
| --- | --- |
| `0`–`5` | Select channel for enabling; no motion |
| `e` | Enable selected at recorded neutral; requires neutral progress |
| `+` / `-` | Ramp group target one step higher/lower; hold at target |
| `g` / `f` | Step5% /1%; no immediate motion |
| `w` | At a completed allowed boundary, extend THAT direction's allowance5 percentage points; no motion |
| `n` | Ramp back to neutral when idle; if moving, use s first |
| `s` | Pause immediately at last written group commands; keep PWM holding, no recenter |
| `x` | Cancel motion and disable ALL16 PWM channels; may release plate; does not cut supply power |
| `?` | Help/status; accepts during motion |

One character per submission; multi-command lines rejected. While moving, only s/x/? accepted. Wait for target completion before further steps. Every20 ms at most, progress advances0.5 percentage points; each motor changes at most3 µs per tick for these records. There is no catch-up burst after a delay. Validate all six next commands before writing any. This bounds commands, not physical force/speed or hardware response. Selecting or changing step size does not move anything.

## Extending the trial

Initial allowance is-5%..+5%. At a reached+5% boundary and only after comfortable physical inspection, `w` expands UP allowance to+10% without motion. `+` then advances to+10%. Repeat in stages as warranted; use fine1% steps near constraints. Widening is blocked until a boundary has actually been commanded and reached in software; code cannot verify physical comfort. DOWN allowance expands separately at its negative boundary. Both are capped at100%; cannot be expanded past recorded pulse endpoints. `w` at100% is rejected.

At any physical restriction, stop before binding/stall; do not widen or keep commanding. `s` holds current output and cancels ramp; it does not remove a load/jam. For straining/jammed hardware use the physical servo-power cut-off with the plate supported rather than assuming a neutral return is safe. `x` also releases PWM holding and may let the plate fall.

If PWM is disabled away from neutral, re-enabling is blocked rather than snapping the mechanism to reference. Support the plate, power off and restore safe reference geometry before restarting/re-enabling. Reset loses progress/allowances and turns all outputs off; it does not physically move the mechanism back to neutral. Serial disconnect leaves last commands holding. No watchdog release, current sensing, stall detection or measured position/height is provided.

Record software progress, all six commanded pulses, approximate actual height/tilt, payload/support, supply behavior, duration and comfortable stop/return observations. The user subsequently reached both coordinated software endpoints without reported buzzing/interference and returned normally to neutral; see OBSERVATIONS.md and the dated CSV. Do not automatically convert this path's limits to a general IK workspace.

## Verification

Target:`arduino:renesas_uno:unor4wifi`. Persistent host tests with simulated PWM/clock/Serial:

```sh
clang++ -std=c++11 -Itests/stubs tests/group_test.cpp -o /tmp/ocean-group-test
/tmp/ocean-group-test
```

Run from this folder. UNO R4 WiFi compile passed with core1.6.0 and Adafruit PWM Servo Driver3.0.3:66504 bytes flash /9288 bytes RAM. Host tests passed initialization/enable guards, exact references and signed interpolation at all2001 progress values, staged boundary extension separately by direction,5%/1% targets, ramp timing and max per-channel tick delta, neutral return, pause/disable cancellation, input rejection and command guards during motion. Software results do not validate physical geometry, load, supply or synchronization. User upload, initial small two-direction proof and coordinated software-endpoint trials are complete; see OBSERVATIONS.md. No physical height extrema, load/endurance capacity or general workspace was established. No Unity assets changed.
