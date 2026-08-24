# Ocean Motion — Repository Instructions

## Project goal

Ocean Motion is progressing toward a functional Stewart platform through numbered milestone experiments. A milestone may remain independent, contribute reusable parts, or integrate directly into the final system. Do not force every milestone into the final hardware architecture; preserve and document its dependency and reuse status.

The final actuator architecture remains intentionally undecided until the Update 08 design gate. Do not assume either a six-rotary-servo linkage platform or a six-feedback-linear-actuator platform has already been selected.

## Git safety

- Inspect the complete working tree, current branch, and upstream state before modifying files.
- Preserve unrelated local work. Never discard, overwrite, stash, reset, or commit unrelated changes without explicit approval.
- Synchronize from the correct remote baseline before troubleshooting new milestone work.
- Prefer fast-forward-only synchronization. Do not substitute a merge, rebase, or force-reset without explaining why it is necessary and obtaining approval.
- Keep Hermes work isolated on the requested branch. Stage and commit only files belonging to the current task.

## Unity project conventions

- Use the existing Unity 6.5 / URP project unless a milestone explicitly changes that baseline.
- Preserve completed milestone archives. Create new work in its numbered subproject directory rather than overwriting an earlier scene or archive.
- Before adding a new simulation or hardware integration, confirm that the last known-good archived scene still compiles.
- Keep scenes, scripts, prefabs, materials, diagnostics, and milestone notes organized within the numbered subproject where practical.
- Prefer small, inspectable stages over generating an entire system before testing its foundations.

## Motion-axis convention

Use and document the established Unity boat/platform convention:

- Local X rotation = pitch
- Local Y rotation = yaw
- Local Z rotation = roll
- World/local Y displacement relative to a calibrated neutral waterline = heave

For telemetry or actuator-facing data, calculate orientation relative to a calibrated neutral rotation. Do not use raw wrapped `transform.eulerAngles` as the contract; signed-angle handling must prevent visible 0/360-degree jumps.

## Milestone and interface boundaries

- Keep simulation, motion processing, inverse kinematics, and physical output as separable stages.
- Preserve explicit data contracts between stages so replaceable experiments do not unnecessarily couple the system.
- Label required work separately from stretch work, and complete the required proof before adding polish.
- Record settings, calibration conventions, test duration, observed issues, and reusable outputs for each milestone.

## Safety

- Do not send unstable, unbounded, or uncalibrated simulation values to physical actuators.
- Do not hide unstable source physics with downstream filtering.
- Treat power, travel limits, payload, pinch points, emergency stopping, and actuator limits as explicit safety concerns whenever physical hardware is involved.
- Ask before performing destructive Git operations or changing established project-wide architecture.
