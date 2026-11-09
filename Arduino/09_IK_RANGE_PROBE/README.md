# Independent IK range receiver — OI5

Upload this updated sketch once for expanded pulse allowances. Older OI3/OI4 firmware cannot support this range contract and is intentionally refused by the new host. Outputs start OFF. No IK runs on the Arduino.

- Absolute1000..2000µs bounds are checked on each whole pose before accepting any target, and on every ramp tick. Saved neutrals limit positive offsets on channels1/3 to400µs and channel2 to450µs even with500µs selected. No per-channel clipping.
- Signed six-channel offsets from saved neutrals; hard ±500µs, selectable allowance25..500µs in25µs steps; absolute1000..2000µs guard.
- Motion speed50..500µs/s in50µs/s steps (1..10µs per20ms tick). Default/reset50. This is a provisional software test ceiling, not a measured servo maximum.
- HOME always uses1µs/20ms (50µs/s), regardless of selected motion speed. A500µs neutral return can take10seconds; Unity allows12seconds.
- Shared vector interpolation; no catch-up burst after a delayed loop. Existing250ms watchdog HOLD and explicit manual rearm remain.
- HELLO holds the current commands, resets allowance25/speed50, and preserves retained offsets for HOME. Configuration changes only on a valid neutral-only ARM, with both allowance and speed echoed exactly. Faults HOLD, never auto-arm.

115200 baud, newline-delimited records:

```
OI5,HELLO,<session>
OI5,READY,<session>,<six current offsets>
OI5,ARM,<session>,<allowance_us>,<motion_speed_us_per_s>
OI5,ARMED,<session>,<same allowance>,<same speed>
OI5,POSE,<session>,<increasing sequence>,<six signed offsets>
OI5,ACK,<session>,<sequence>,<six current offsets>
OI5,HOME,<session>
OI5,PING,<session>
OI5,HOLD,<session>
OI5,OFF,<session>
```

`?` reports OI5 STATUS including motion_speed_us_per_s and home_speed_us_per_s. HOME requires PWM previously enabled; OFF is not mechanical homing. Keep the plate supported and servo supply OFF during upload. No upload is needed between speeds after installing OI5.

Host tests (no USB):

```
clang++ -std=c++11 -IArduino/09_IK_RANGE_PROBE/tests/stubs Arduino/09_IK_RANGE_PROBE/tests/range_test.cpp -o /tmp/ocean-speed-test
/tmp/ocean-speed-test
```

Tests exercise all ten speeds at all twenty pulse caps, random vectors, whole-packet validation, fixed slow HOME including500µs retained offsets, fractional zero-crossing reversal, neutral-only configuration, reset behaviour, malformed speeds, old-protocol refusal, watchdog and no automatic rearm.
