# Subproject 05 — Boat Water

## Purpose and boundary

Build a deterministic Unity boat-and-water simulation that produces stable, raw pitch, roll, heave, and diagnostic yaw motion. This subproject is a simulation source only: it does not include motion scaling, washout/filtering, Stewart-platform inverse kinematics, or physical actuator output.

## Current baseline

The simulation now has a shared water source and a Rigidbody-based buoyancy baseline:

- `WaveField` owns the wave configuration and calls the shared `WaveSampler`.
- `WaveSurface` draws the visible water mesh by sampling that same `WaveField`.
- `BoatBuoyancy` samples `WaveField.SampleHeight` at each buoyancy point during `FixedUpdate`.
- A single boat `Rigidbody` receives distributed upward spring and vertical damping forces through `Rigidbody.AddForceAtPosition`.

Do not duplicate the sine-wave equation in boat code. Physics and visuals must always sample the same `WaveField` at the same world XZ position and simulation time.

## Wave model

Each wave component contributes:

```text
amplitude * sin((2π / wavelength) * (dot(worldXZ, normalizedDirection) - speed * time) + phaseOffset)
```

The water height is the water object's world Y position plus its base-height offset and all active wave components.

Initial authored values are intentionally gentle starting values, not final tuning targets:

| Component | Amplitude | Wavelength | Direction XZ | Speed | Phase offset |
|---|---:|---:|---|---:|---:|
| Primary | 0.15 | 8.0 | (1.0, 0.0) | 0.8 | 0.0 |
| Secondary | 0.06 | 5.0 | (0.35, 0.94) | 0.45 | 0.7 rad |

## Boat buoyancy model

Use **one Rigidbody** and four active buoyancy points:

```text
FrontLeft       FrontRight
RearLeft        RearRight
```

Place the active points symmetrically on the underside / expected wetted area of the Zodiac hull. They model where the boat displaces water, not the visual mesh bounding-box corners if those corners fall outside the hull.

For each submerged point, `BoatBuoyancy`:

1. Samples the water surface height at the point's world position.
2. Calculates submersion depth.
3. Applies an upward spring force proportional to depth.
4. Applies vertical damping from the point's vertical Rigidbody velocity.
5. Applies the result at that point with `AddForceAtPosition`, creating both lift and the natural pitch/roll torque.

The two optional middle buoyancy transforms may remain in the hierarchy for a later six-point comparison, but are deliberately excluded from the active array for the required four-point baseline. A six-point version should use three left/right pairs (bow, middle, stern), not centerline-only points, and must be tuned as a separate experiment because additional points increase total lift.

### Height bias

`floatHeight` is now an explicit visual ride-height bias in the depth calculation:

```text
depth = sampledSurfaceY - pointY - floatHeight
```

It changes the apparent draft / ride height. It is not a physical waterline solution and should be adjusted separately from spring and damping tuning.

### Observed early tuning

The initial flat-water test used:

```text
Mass: 250
Spring strength: 1000
Vertical damping: 50
```

This settled successfully but produced a deep draft: a 250-unit mass has roughly 2453 N of weight, or about 613 N per four buoyancy points. With a 1000 spring per point, the equilibrium submersion is about 0.61 units before any height bias.

Increasing spring strength into the 5000–8000 range produces a visually higher ride, but requires proportional re-tuning of damping. These are exploratory values, not an approved final configuration. Record the final Rigidbody, spring, damping, height-bias, and fixed-timestep values after the bounded-wave tests.

## Physics bring-up and results so far

Completed manually:

- Flat-water buoyancy: the boat floats and settles instead of falling indefinitely.
- One-wave test: the front responds before the rear as a crest travels under the hull; this is the expected distributed-force behavior.
- The boat visibly bobs with increased wave amplitude.

Next required tests, in order:

1. Reconfirm flat water settles at the chosen neutral waterline.
2. Use one gentle bow-on wave; verify the bow rises at a crest and produces pitch plus heave.
3. Use a side-on wave; verify roll plus heave.
4. Add small initial orientation offsets; verify recovery without runaway oscillation.
5. Test crossed waves.
6. Run the final combined-wave preset continuously for at least 60 seconds and record whether motion stays bounded.

Stop and retune if the boat launches, tunnels through water, accumulates energy, flips continuously, or changes materially with render frame rate.

## Input System note

Subproject 05 scripts compile inside `OceanMotion.Subproject05.asmdef`. Any code using the Unity Input System must keep an assembly reference to `Unity.InputSystem`. Unity may serialize this dependency as a `GUID:...` reference and expand the assembly-definition defaults; that is expected and should be left intact.

A temporary keyboard pilot may use an Input Actions asset with a `Boat/Drive` `Vector2` action and an arrow-key 2D Vector composite. It is a local simulation diagnostic tool only, not a later motion-control or hardware interface.

## Coordinate convention

The Rigidbody root must use the project convention:

```text
+Z = bow-forward
+Y = up
```

If an imported Zodiac mesh needs rotation to look correct, put that rotation on a visual child. Keep the Rigidbody, collider, buoyancy script, and buoyancy points on the unrotated root.

Telemetry convention for later work:

- Local X rotation: pitch
- Local Y rotation: yaw (diagnostic only in Subproject 05)
- Local Z rotation: roll
- Vertical displacement from calibrated neutral waterline: heave

Do not use raw wrapped Euler angles as the downstream telemetry contract.

## Deferred improvements

Do not add these until the required baseline is verified:

- Water-relative velocity from moving waves
- Horizontal hydrodynamic drag
- Quadratic drag or hull-dependent coefficients
- Planing, wakes, spray, or full fluid simulation
- Motion scaling/filtering, serial output, or Stewart-platform control

The next bounded improvement after baseline verification is simple per-submerged-point water resistance: lower drag along boat-forward motion and higher drag against sideways slip. It should be added separately from buoyancy tuning so its effect is measurable.

## Automated verification

Edit Mode tests in `Tests/EditMode/WaveSamplerTests.cs` cover:

- Flat / zero-amplitude water
- A predictable one-wave crest
- Two-component summation
- Wave travel from configured speed
- `WaveField` delegation to the shared sampler
- Visible mesh vertices matching `WaveField` at identical world XZ and time inputs

Manual Play-mode stability and motion-axis checks remain required.
