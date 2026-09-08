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

A checked milestone has been completed. Detailed tasks, test results, design notes, and publication status are maintained privately in Notion.

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

- [ ] **06 — Build the motion pipeline**
  Turn raw boat movement into scaled, filtered, and bounded motion commands.

- [ ] **07 — First mechanical linkage**
  Test one representative actuator, driver, joint, and linkage under a realistic mechanical load.

- [ ] **08 — Design the Stewart platform**
  Define the platform requirements and select a candidate rotary-servo or linear-actuator architecture.

- [ ] **09 — Solve inverse kinematics**
  Calculate the six required leg lengths or servo angles and validate the mechanism throughout its usable workspace.

- [ ] **10 — Coordinated platform movement**
  Coordinate all six physical channels to produce controlled, unloaded platform movement.

- [ ] **11 — Boat drives the platform**
  Connect the Unity boat simulation to the physical platform through the complete motion pipeline.

- [ ] **12 — Filter and scale the ride**
  Tune motion scaling and filtering so the physical result remains useful and within the platform’s limits.

- [ ] **13 — Calibration, limits and safety**
  Finalize homing, calibration, travel limits, fault handling, and emergency-stop behaviour.

- [ ] **14 — Integrated boat-motion demo**
  Demonstrate the complete Unity-to-hardware system operating as one functional platform.

- [ ] **15 — What worked and what didn’t**
  Document the results, failures, latency, cost, design trade-offs, and priorities for a second version.

## Project organization

Ocean Motion uses **Unity 6.5 with the Universal Render Pipeline**.

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
