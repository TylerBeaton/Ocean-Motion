# Ocean Motion

Ocean Motion is a Unity and mechatronics project exploring how the movement of a virtual boat can be reproduced by a physical motion platform.

The project is being developed through small, numbered experiments. Each milestone proves one part of the system—from sending Unity data to an Arduino, through boat and water simulation, to coordinating the actuators of a Stewart platform.

## Main goal

The main goal is to build and demonstrate a functional Stewart platform whose movement is driven by safe, bounded motion data from a boat simulated in Unity.

The first integrated platform may focus on **pitch, roll, and heave** before expanding to all six degrees of freedom.

Not every milestone must become part of the final machine. Some are independent experiments, while others produce reusable software, measurements, or design knowledge. The final actuator architecture—rotary servos or feedback linear actuators—will be selected during Milestone 08 rather than assumed in advance.

## AI-Assisted Development (Updated)

I use AI-assisted tools throughout Ocean Motion to help plan and coordinate milestones, research technical questions, compare possible approaches, develop and review code, and maintain project documentation.

AI is particularly useful as a technical collaborator and auditor. It helps me challenge assumptions, inspect code, identify missing tests, compare documentation with the implementation, and highlight potential risks involving physics, control systems, hardware, power, and safety.

Whether code is written directly by me or developed with AI assistance, I am responsible for every change I accept into this project. I review AI-assisted code, ensure that I understand it, and evaluate it against the same standards of correctness, quality, safety, and maintainability as any other contribution.

AI output is treated as a recommendation or hypothesis—not as proof that something works. Final design decisions remain human decisions, and hands-on testing in Unity or on physical hardware is required before a milestone is considered complete. AI-generated conclusions do not replace testing, measurements, engineering judgment, physical validation, or safe operating limits.

## Milestone roadmap

A checked milestone has completed the prototype demonstration described below. Unchecked items marked **In progress** already have working components, with the remaining validation or deliverable stated explicitly. Detailed tasks, test results, design notes, and publication status are maintained privately in Notion.

**Current checkpoint — November 9, 2026:** processed Unity boat motion drives the rotary Stewart prototype through six independent IK commands. Timed hardware trials, per-run telemetry, selectable motion limits/speeds, and smooth wave-preset transitions are working. Measured physical pose accuracy, the final actuator architecture decision, and the full usable workspace remain open. See the [current SP09 handoff](Assets/Subprojects/09_STEWART_IK/HANDOFF.md) for evidence and operating limits.

- [x] **01 — Unity talks to Arduino**
  Establish a reliable Unity-to-Arduino serial connection and send a changing value.

- [x] **02 — Send live transform data**
  Transmit useful position and rotation data from Unity and verify that it can be parsed reliably.

- [x] **03 — One axis becomes physical**
  Map one Unity motion channel to a physical output such as a hobby servo.

- [x] **04 — Map pitch and yaw**
  Control two physical channels independently and together from Unity pitch and yaw.

- [x] **05 — Put the boat on water**
  Create a stable boat-and-water simulation that produces useful pitch, roll, heave, and diagnostic yaw motion.

- [x] **06a — Build the motion pipeline**
  Turn raw boat movement into scaled, filtered, and bounded motion commands.

- [x] **06b - Design the Stewart Platform with CAD software**
  Use CAD software to design the prototype giving attention to part measurements and positioning of parts and simulate it.

- [x] **07 — First mechanical linkage**
  Built and exercised the servo, crank, joints, and pushrod linkages in the assembled rotary prototype. Representative payload/load characterization continues under Milestone 13.

- [x] **08 — Design the Stewart platform**
  Designed and assembled the six-servo rotary prototype, recording geometry, neutral references, directions, and bench pulse-range observations. This completes the prototype design experiment; the final rotary-servo versus feedback-linear-actuator architecture decision remains open.

- [x] **09 — Solve inverse kinematics**
  Implemented and tested analytical Python/C# rotary IK, coordinate conversion, branch continuity, visual previews, and live six-servo commands. Measured pulse-to-angle calibration and full physical-workspace validation continue under Milestone 13.

- [x] **10 — Coordinated platform movement**
  Demonstrated coordinated, unloaded six-servo movement, bounded manual poses/sweeps, gradual neutral return, and explicit HOLD/PWM-off controls. These trials establish the prototype proof; general workspace and final safety acceptance continue below.

- [x] **11 — Boat drives the platform**
  Processed Unity boat motion now drives the physical platform through the same six independent IK solutions shown in the preview. Bow, Side, Crossed, and preset transitions have been exercised with timed trials and recorded command telemetry.

- [ ] **12 — Filter and scale the ride**
  **In progress:** runtime rotation/heave gains, bounded command speeds, and 1–3 second source-wave transitions are working; telemetry shows reduced command-tracking lag. Repeatable demo settings and final response tuning remain under evaluation.

- [ ] **13 — Calibration, limits and safety**
  **In progress:** neutral references, whole-pose rejection, absolute pulse bounds, watchdog HOLD, session/sequence checks, and manual rearming are implemented. Final measured calibration, joint/collision limits, power/load characterization, and physical fault/emergency-stop acceptance remain open.

- [ ] **14 — Integrated boat-motion demo**
  **In progress:** the integrated Unity-to-hardware bench demonstration works. Select and repeat the filming configuration, capture synchronized simulation/platform footage, and publish the finished demo video.

- [ ] **15 — What worked and what didn’t**
  **In progress:** test logs, tracking plots, fault diagnoses, and iteration notes are being collected. Complete the final results, latency/cost review, design trade-offs, and priorities for a second version.

## Project organization

Ocean Motion uses **Unity 6.5 with the Universal Render Pipeline**.

The Subproject 07 Stewart-platform demo uses the third-party **Final IK** Unity Asset Store package. It is not included in this repository; install it separately in Unity before opening or testing that scene and prefab. This repository documents milestone experiments as a project journal and portfolio, not a ready-to-use product or a fully portable project setup. The IK dependency may be replaced with Unity-native tooling in a later experiment.

Numbered experiments are kept under `Assets/Subprojects`. Each implemented subproject contains its own scene, scripts, supporting assets, and local documentation where practical.

Completed experiments are preserved rather than overwritten. This makes it possible to revisit each working milestone while later parts of the project continue to evolve.

Detailed milestone planning is managed privately in Notion. The tracker records individual tasks, completion status, hands-on test results, settings, observed issues, and whether each milestone’s public update has been posted. This README provides a lighter public summary of that roadmap.

## Motion convention

The current motion convention is a **work in progress** and may evolve as the simulation, platform geometry, and actuator architecture are developed.

For now, the project uses the following Unity boat and platform convention:

| Motion | Unity representation |
|---|---|
| Pitch | Local X rotation |
| Yaw | Local Y rotation |
| Roll | Local Z rotation |
| Heave | Vertical Y displacement from a calibrated neutral waterline |

Actuator-facing orientation is measured relative to a calibrated neutral pose. Raw wrapped Euler angles are not used as the final motion-control contract.

The coordinate frames, forward kinematics, and inverse kinematics will be documented in greater detail as the physical platform design is selected and the project becomes more complete.

## Safety

Simulation values must not be sent directly to physical actuators unless they have been calibrated, bounded, and validated.

Power limits, travel limits, payload, pinch points, watchdog behaviour, emergency stopping, and actuator failure states are treated as requirements throughout the project—not as final-stage polish.
