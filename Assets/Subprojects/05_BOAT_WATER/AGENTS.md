# Subproject 05 — Boat Water

## Scope

Build a small, deterministic Unity boat-and-water simulation that produces useful raw pitch, roll, heave, and diagnostic yaw telemetry. This milestone is a simulation source only. It does not require new physical hardware and does not select the final Stewart-platform actuator architecture.

Update 05 ends at stable raw boat-motion data. Update 06 owns later scaling, filtering, limiting, washout, and output. Stewart-platform inverse kinematics and physical actuator commands are out of scope.

## Archive

Work within:

```text
Assets/Subprojects/05_BOAT_WATER/
├── Scenes/SP05_BoatWater.unity
├── Scripts/
├── Prefabs/
├── Materials/
└── README.md
```

Do not overwrite, rename, or reorganize previous milestone archives unless explicitly requested.

## Baseline gate

Before implementing or debugging this scene:

1. Inspect the complete Git working tree and preserve unrelated work.
2. Confirm the requested branch and its relationship to `origin/main`.
3. Ensure the local baseline has been safely synchronized.
4. Confirm the archived Subproject 03/04 scene compiles.
5. Record the Unity version, URP baseline, and working serial baseline.

Do not troubleshoot new water behavior on top of an unsynchronized or already-broken baseline.

## Required simulation architecture

Use one shared deterministic wave sampler for both:

1. The visible water representation
2. Water-height sampling for buoyancy

Identical world XZ position and simulation-time inputs must return identical water-height outputs.

Begin with one gentle directional sine wave. Add the second low-amplitude component only after the single-wave case is stable. Expose these values per wave in the Inspector:

- Amplitude
- Wavelength or frequency
- Direction
- Speed
- Phase offset

Provide repeatable presets for flat/calm water, bow-on waves, side-on waves, and crossed/combined waves.

Do not spend this milestone on realistic fluid simulation, wakes, spray, or an infinite-ocean renderer.

## Boat physics

Use one Rigidbody boat with a visibly identifiable bow and four attached buoyancy/sample points:

- `FrontLeft`
- `FrontRight`
- `RearLeft`
- `RearRight`

The four points do not produce four transforms and do not map to Stewart actuators. They apply distributed forces to one Rigidbody, which produces one boat pose.

During `FixedUpdate`, for each sample point:

1. Sample water height at its world XZ position.
2. Calculate submersion depth.
3. Apply an upward spring force if submerged.
4. Apply vertical damping based on point velocity.
5. Apply the result at the sample position with `Rigidbody.AddForceAtPosition`.

Bring the system up in this order:

1. Flat water: tune until the boat settles at its neutral waterline.
2. One gentle bow-on wave: verify pitch and heave.
3. One side-on wave: verify roll and heave.
4. Small initial orientation offsets: verify recovery.
5. Two crossed waves: verify bounded combined motion.
6. Run the final combined preset continuously for at least 60 seconds.

Stop and retune if the boat explodes upward, tunnels through the water, accumulates energy, oscillates indefinitely, flips continuously, or responds materially differently at different rendering frame rates.

## Coordinate and telemetry contract

Use the documented boat-local convention:

- Pitch: signed rotation around local X
- Yaw: signed rotation around local Y; diagnostic/display-only in Update 05
- Roll: signed rotation around local Z
- Heave: vertical displacement relative to a calibrated neutral waterline

At scene start or through an explicit `Calibrate Neutral` action, store:

- Neutral boat position or waterline
- Neutral boat rotation
- Reference heading

Produce one calibrated boat-motion state from the Rigidbody pose containing at least:

- Sequence
- Simulation/wave time
- Pitch
- Roll
- Heave
- Yaw
- Current preset

Recommended serialized form:

```text
BOAT,<sequence>,<time>,<pitch>,<roll>,<heave>,<yaw>
```

Do not use raw `transform.eulerAngles` as the downstream contract. Calculate relative orientation and signed angles so crossing neutral cannot create a 0/360-degree discontinuity. Keep raw and calibrated values available in diagnostics.

The required output is a stable Unity data object. Serial transmission is optional. If added, use latest-state delivery so stale poses cannot accumulate in a FIFO queue.

## Diagnostics and verification

Display:

- Pitch
- Roll
- Heave
- Yaw
- Current wave preset
- Simulation/wave time
- Rigidbody speed

Optional diagnostics include sampled water heights, point submersion, sample markers, and force gizmos.

Verify deliberately:

1. Flat water and neutral calibration
2. Bow-on wave producing pitch and heave
3. Side-on wave producing roll and heave
4. Crossed waves producing combined pitch, roll, and heave
5. Flattened or paused waves allowing the boat to settle

Record whether values are bounded, repeatable, and free from wrap discontinuities.

## Required completion criteria

- An isolated Update 05 scene exists without overwriting an earlier archive.
- One shared deterministic wave definition drives visuals and buoyancy sampling.
- The Rigidbody responds through four distributed sample points.
- Bow-on and side-on tests produce the expected motion axes.
- Pitch, roll, heave, and yaw are displayed relative to a documented neutral pose.
- Signed-angle handling prevents visible telemetry jumps.
- Calm, bow-wave, side-wave, and combined presets are repeatable.
- The boat remains bounded during a 60-second combined-wave test.
- A screen-recordable demo clearly shows boat response and telemetry.
- Final wave settings, Rigidbody and buoyancy values, fixed timestep, neutral convention, test duration, and observed issues are recorded.

## Stretch work — not required

Do not let these delay the required proof:

- Better boat mesh or materials
- Advanced water shading or normals
- Camera follow or Cinemachine
- Serial transmission
- Automated latency profiles
- Force and sampled-height gizmos
- Motion scaling or ride filtering
- Stewart-platform inverse kinematics

## Physics and safety caveats

- Visual water is not proof of physical agreement; visuals and buoyancy must share the same sampler.
- Buoyancy behavior is coupled to Rigidbody mass, point placement, fixed timestep, spring force, damping, and drag. Change one category at a time.
- Establish stable raw physics before adding filtering.
- Never send unstable or unbounded simulation values to physical hardware.
- Wave-only vertical buoyancy may produce little meaningful yaw; that is acceptable for this milestone.
