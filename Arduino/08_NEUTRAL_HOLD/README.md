# All-six reference holding — foundation stage

Independent Update 08 rotary-servo bench stage following [pair calibration](../08_PAIR_CALIBRATION/CALIBRATION.md). Uses UNO R4 WiFi, PCA9685 at 0x40 / 50 Hz, and the installed Adafruit PWM Servo Driver library. No Unity/IK input. Earlier sketches remain preserved.

Channel references in microseconds: **[1500,1600,1550,1600,1500,1500]**. These are the visually aligned horn commands recorded on 2026-10-31, not measured horn angles or proof of an assembled neutral pose. They are compiled into this sketch; enabling uses the channel's recorded reference directly, not a universal1500 µs. Initial enable can move abruptly. Future edits must remain inside1400–1650 µs; invalid reference configuration blocks all enabling.

## Required first proof

1. Support the unloaded plate. Turn servo supply OFF and disconnect all six pushrods so the horns move freely. Check wire labels against channels. Stop other serial senders. Upload this sketch with servo power OFF; Arduino reset alone may leave PCA9685's previous output active until setup runs.
2. Serial Monitor:115200 baud, Newline or CRLF, one character per submission. Send `?`. Expect driver_ready=1, references_valid=1, all six enabled=0 and all_enabled=0. No outputs are enabled automatically.
3. Turn servo power ON. Send `0`, then `e`. Inspect neutral horn position and quiet settling before enabling another servo. Never force a horn to check holding torque.
4. Enable channels1 through5 the same way, one at a time. Each previously enabled channel keeps its reference command. Pause and inspect all horns after each addition; note jitter, buzzing, heating, resets or supply current limiting. `e` on an already enabled channel does not rewrite the command.
5. Once all six are quietly settled, send `?`; expect all_enabled=1 and recorded references. Observe for30 seconds with pushrods disconnected, noting duration and any issues. This is the proof gate before adding any movement to this stage.
6. Support the plate, send `x` and turn servo power OFF before changing wiring or hardware. `x` disables PWM on all16 channels; it does not cut supply power and may release holding torque. Reference values stay in the program but enabling state resets to OFF on restart.

| Command | Action |
| --- | --- |
| `0`–`5` | Select channel; no motion or effect on other channels |
| `e` | Enable selected at its recorded reference; other enabled channels keep holding |
| `?` | Help/status, references, commanded enable state |
| `x` | Disable PWM on ALL16 channels |

There is no enable-all command, sweep, offset, pair movement or automatic return. Old calibration commands such as `+`, `p`, `s`, `r`, `a/b/c` are rejected. Selection/status cannot enable a channel. The parser rejects multiple commands on one line.

## Physical scope and next gate

Supply was reported as5 V /3 A. This stage checks unloaded simultaneous holding only; passing does not validate supply/distribution for six moving or loaded servos. Keep a physical servo-power disconnect accessible. Serial disconnection leaves the last command holding; there is no position/current feedback or power-loss detection. Printed enable state reports commands, not measured holding capability. I2C writes are sequential and cannot guarantee physical synchronization or detect every hardware failure.

Keep the pushrods disconnected throughout this first proof. Do not connect a loaded mechanism relying on enabled outputs surviving reset: startup intentionally disables all channels. Before connecting the linkage, establish a supported assembly procedure and verify no preload, mechanical clearance, suitable supply/distribution and safe restart behavior. Before testing travel, implement and verify the next small motion stage with all six reference commands preserved. No mechanical limits, full workspace or final IK bounds have been established.

Record physical reference alignment by channel, observed test duration, supply/current-limit behavior and any issues. On2026-10-31 the user uploaded this sketch and supplied all_enabled=1 status at11:19:45 with all recorded references. They then confirmed the supported, unloaded plate looked level and completely quiet. Pushrods were attached, differing from the preferred disconnected first-check procedure above. No extended timed hold/electrical measurements were supplied. Proceeding to manual extent observations is implemented separately in `../08_EXTENT_TEST/`; no physical extents established yet.

## Verification

Compile target:`arduino:renesas_uno:unor4wifi`. Host-side tests in `tests/` use simulated PWM/Serial to verify command routing and startup behavior, not actual mechanics. Run:

```sh
clang++ -std=c++11 -Itests/stubs tests/neutral_hold_test.cpp -o /tmp/ocean-neutral-hold-test
/tmp/ocean-neutral-hold-test
```

Use this folder as the working directory for the test commands. Compiled with UNO R4 core1.6.0 and Adafruit PWM Servo Driver3.0.3:64964 bytes flash,9260 bytes RAM. Host tests passed startup-off, failed driver/configuration lockout, exact reference writes with prior channels retained, duplicate-enable protection, inert selection/status, legacy/malformed/long-line rejection, parser recovery and disable-all. The user completed a brief supported-assembly neutral hold with attached rods and reported quiet, level holding; a timed endurance/load proof was not performed. No Unity assets changed; prior successful script compilation evidence was inspected.
