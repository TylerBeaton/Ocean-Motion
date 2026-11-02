# Manual per-servo extent observations

Update08 rotary-servo bench stage after the all-six neutral-hold foundation. Existing pair and neutral-hold sketches are preserved. UNO R4 WiFi, PCA9685 0x40 /50 Hz, Serial115200/Newline, one character per submission.

User confirmed on2026-10-31 that the supported, unloaded plate with attached pushrods looked level and held completely quietly with all six enabled at reference commands [1500,1600,1550,1600,1500,1500] µs. No extended hold duration or measured supply/current data was provided. Supply reported5 V/3 A; simultaneous loaded/moving performance remains unvalidated.

## What this measures

Move ONLY the selected servo while the other five stay at their recorded neutral commands. Record last comfortable endpoints BEFORE binding, stall, buzzing, excessive load, linkage interference, unexpected motion or instability. Horn raise signs [+1,-1,+1,-1,+1,-1] define +/-; they do not imply a pure platform heave/tilt motion.

These are configuration-specific observations for the connected assembly with five reference commands fixed. They are not individual unloaded servo specifications, proof of a collision-free workspace, or final IK limits. Combining individually observed endpoints may be unreachable. Future operating limits need conservative margins, angle-to-pulse calibration and whole-pose joint/collision/reachability checks.

## Setup and commands

Support the unloaded plate at neutral with an accessible physical servo-power cut-off. Turn servo power OFF before upload. Upload this sketch, then `?` over USB: driver_ready=1, references_valid=1 and six enabled=0. Startup resets records/windows and disables ALL16 PWM outputs; Arduino reset alone may leave prior PCA9685 commands active until setup runs. References are directly commanded when each channel is enabled, so support the assembly and inspect for preload/unexpected motion each time.

Turn servo power ON; select and enable0..5 one at a time (`0`, `e`, `1`, `e`, etc.), pausing after each enable. Only continue if all six are quiet and the supported plate is at the expected reference pose. A status message confirms commands, not physical position or torque. All six must be enabled before any travel command. Enabling a new channel or switching selection is blocked if any enabled channel is away from neutral.

| Command | Action |
| --- | --- |
| `0`–`5` | Select servo without movement; requires every enabled channel at neutral |
| `e` | Enable selected at its saved reference; others keep holding |
| `+` / `-` | Raise/lower the selected horn attachment by one signed pulse step; hold afterward |
| `g` / `f` | Coarse25 µs / fine5 µs; no motion |
| `l` | Record comfortable DOWN endpoint (nonpositive horn offset) |
| `h` | Record comfortable UP endpoint (nonnegative horn offset) |
| `n` | Explicit gradual return to selected servo's reference,5 µs every20 ms |
| `w` | Widen selected software window50 µs each way; no movement |
| `v` | Print copyable endpoint CSV for all six |
| `?` | Help/status |
| `x` | Cancel return and disable PWM on ALL16 channels; records remain in RAM |

No automatic sweep or enable-all command. Coarse steps reduce submissions; use fine steps near potential interference. Inspect after EVERY step. During `n`, only status/results/x are accepted. A deliberate return follows the previously traversed command path; if the mechanism is straining or jammed, use the physical power cut-off instead of assuming a return is safe. Return completion reports the commanded reference, not measured plate pose. Serial disconnect leaves commands holding; there is no watchdog shutdown, current feedback or stall detection.

## Software windows are not mechanical endpoints

Each channel initially permits±50 µs around its own reference. A rejected command causes no output. If it stops at a software boundary while the prior motion is comfortable, inspect geometry and deliberately use `w` for the next50 µs stage. Expansion itself does not move anything. It is never evidence that newly permitted travel is safe.

Expansion is capped by1000–2000 µs outer exploration guardrails. This is a software envelope, NOT a manufacturer-verified travel specification for these unverified MG996R variants, and NOT permission to drive to either end. Stop much sooner for physical constraints. If only a software boundary is reached, record that as software-limited; do not claim a physical min/max. Widening or pressing harder does not resolve mechanical binding. Firmware edits beyond these guardrails need a separate review.

## Efficient recording pass

Select0 at neutral. Step `-` toward a comfortable down endpoint, mark `l`; use `n` to return and wait for completion. Step `+` toward a comfortable up endpoint, mark `h`; `n` to return. Inspect reference pose before selecting the next servo. `l`/`h` only store a user observation and do not move anything. They may be overwritten deliberately. If a side is software-limited or stopped for another reason, note that context alongside the CSV.

`v` prints channel,neutral,raise_sign,down_us,up_us,min_us,max_us. The last two are numerical pulse bounds sorted from recorded up/down values; for negative-sign servos down_us is numerically larger than up_us. Zero denotes an unrecorded endpoint (min/max remain0 until both sides recorded). Records survive `x` but are lost on reset/upload/power loss. Copy CSV and observations after each servo. Never label a stall position as a comfortable endpoint.

Record test duration, supply behavior, load/support arrangement, windows/steps, last comfortable values, stop reason and actual return-to-reference observation. The user completed the individual trials and normal returns; see OBSERVATIONS.md and recorded-endpoints-2026-10-31.csv. All endpoints were software-limited. Software checks alone cannot establish physical travel.

## Verification

Build target `arduino:renesas_uno:unor4wifi`. Host tests use simulated PWM/Serial/clock. From this folder:

```sh
clang++ -std=c++11 -Itests/stubs tests/extent_test.cpp -o /tmp/ocean-extent-test
/tmp/ocean-extent-test
```

Compiled for UNO R4 WiFi/core1.6.0/Adafruit PWM Servo Driver3.0.3:66912 bytes flash,9388 bytes RAM. Persistent simulated tests passed startup/driver/configuration lockouts, all-six enabling, selected-channel isolation and signed movement for all6, neutral switching guards, coarse/fine increments, exploration bounds/widening caps, endpoint-side gating, timed neutral returns/cancellation and malformed input rejection/recovery. User upload and individual software-endpoint trials are complete, with quiet/clear motion and normal returns reported for all six. See OBSERVATIONS.md; no mechanical hard-stop limits were measured. No Unity assets changed.
