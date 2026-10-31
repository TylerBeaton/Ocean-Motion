# Pair A manual bench calibration

Independent Update 08 rotary-servo experiment, not a final architecture decision or IK integration. UNO R4 WiFi, PCA9685 at 0x40, 50 Hz, existing Adafruit PWM Servo Driver library. Pair A = [0,5]; future B = [1,2], C = [3,4]. Labels must match PCA9685 channels.

## First establish level

1. Support the unloaded platform, remove servo power and disconnect the pushrods for servos 0 and 5. Keep hands clear of horns and linkage. Use the appropriate external servo supply with common ground and an accessible physical power disconnect. Other legs must be supported or held at their independently established neutral positions; this sketch does not calibrate them.
2. Upload this sketch with servo power off. It disables PWM on ALL 16 channels at startup. This is a standalone controller: stop Unity/other serial senders. Resetting the Arduino alone may leave the PCA9685's previous output active until setup runs; remove servo power before uploading/resetting.
3. Open Serial Monitor at **115200**, line ending **Newline** (Both NL & CR also works). Send `?`. Commands are single characters, one per submission. Do not paste a sequence or hold a repeating key.
4. With pushrods disconnected, apply servo power. Send `0`, then `e`. This commands nominal **1500 microseconds**, which can move abruptly and is NOT known level. `+`/`-` changes the selected command by **5 microseconds**, then holds indefinitely. Inspect horn direction. `r` reverses what `+` means without moving. Repeat using `5`, then `e`. The initial opposite directions are a hypothesis, not a verified mapping.
5. Establish the desired neutral horn/link geometry for each servo with the platform supported at level. Adjust using small steps; do not force a rod into place. If needed, re-index horns with power removed and repeat calibration. Reconnect only when the geometry matches without preload. Send `s` for each servo when its level setting is established. `s` records the commanded pulse in RAM, not a measured angle. Individual movement invalidates that servo's saved level.

The initial absolute window is **1400–1600 microseconds**. It is deliberately narrow, not a proven safe mechanical range. If level cannot be reached, stop and review mounting and servo specifications before editing the constants. Do not substitute a generic full servo range.

## Then test the pair

Once BOTH level settings and directions are verified, send `p`. This changes modes without moving. `+` changes both pulses by one signed 5-microsecond step; `-` reverses that step. Check the whole linkage after EVERY command. These are pulse increments, not degrees or millimetres. Two I2C writes occur sequentially; motion is not feedback-synchronized.

Pair mode initially permits only **±50 microseconds from the saved level**, also subject to the absolute window. Expand these exploration windows in small, deliberate code edits only after validating the previous range; re-uploading loses calibration. Never seek a hard stop or stall. Record the last comfortable position before binding, buzzing, rod interference or excessive load. Return toward zero with individual `+`/`-` submissions before exploring the opposite direction. There is no automatic sweep or jump to level.

`x` disables PWM on all channels and clears calibration. This may release holding torque and let the platform fall: support it first. It is not a power emergency stop. Serial disconnection leaves the last command holding; use the physical supply disconnect if needed. Servos without feedback cannot detect binding or confirm actual position. Pairs B and C are not driven by this sketch.

## Record the result

| Item | Measurement |
| --- | --- |
| Servo model, supply voltage, unloaded/support arrangement | Pending |
| Servo 0 / 5 verified raise directions | Pending |
| Servo 0 / 5 level pulses (µs) | Pending |
| Last comfortable lower: offset and BOTH pulses (µs) | Pending |
| Last comfortable upper: offset and BOTH pulses (µs) | Pending |
| Window settings, step size, duration, observed issues | Pending |

Required proof: neutral calibration, direction confirmation, small paired motion, reversible return to neutral, and recorded observations. Hardware validation is pending; do not advance to larger extents before this proof.

Reusable output: per-servo neutral pulses, direction signs and observed command ranges. Later IK integration needs a calibrated angle-to-pulse mapping, conservative margins, all-six-servo bounds and linkage/workspace checks. Pair-only limits do not establish a safe combined-motion workspace. IK changes and pairs B/C are deferred.
