# Coordinated command-path observations — 2026-10-31

## Group software endpoints reached

After the initial ±5% proof, user independently expanded each completed boundary in5-percentage-point stages and supplied both full-progress status records. User reported "no buzzing or interference" with the endpoint results. No physical heights, tilt measurements, current measurements or payload data supplied.

| Record time | Progress | Moving | All enabled | Commands0..5 (µs) |
| --- | --- | --- | --- | --- |
| 12:13:42 | +100% | 0 | 1 | 2000,1000,2000,1000,2000,1000 |
| 12:15:44 | -100% | 0 | 1 | 1000,2000,1000,2000,1000,2000 |

Both status records showed driver_ready=1 and references_valid=1. Final allowances±100%, step5%. Both endpoints are **software-limited coordinated command-path observations**, not discovered mechanical stops or measured minimum/maximum height. The pure-heave nature of this path is unverified; interpolation is between horn commands, not Cartesian poses. General IK workspace, other combined poses, endurance and load/supply capacity remain unvalidated. No margin assigned.

After the lower endpoint user explicitly confirmed: "Platform returned to neutral normally, everything looks good." This completes the reported return check for the coordinated software-endpoint trial. Last reported physical state is normal neutral; final live commanded neutral status was not separately supplied. Shutdown (supported plate,x,supplyOFF) remains unconfirmed. Preserve these results before further firmware changes.

Supported, unloaded assembled platform; pushrods attached. Supply reported5 V/3 A. Recorded horn references and signed endpoints from the individual stage are used. This interpolated group path is not a geometry-derived pure-heave IK solution or measured height command.

## Initial proof

- User uploaded08_GROUP_TEST and provided startup status at12:08:15:progress0,target0,moving0,all_enabled0,allowances-5..+5%,step5%,driver_ready1,references_valid1.
- At12:10:19 user supplied enabled-channel commands for the first+5% target: [1525,1570,1572,1570,1525,1475] µs. User confirmed the platform physically went up.
- Agent then requested n to neutral,- once for-5%,n to neutral, with inspection. User confirmed: "Everything looks good after doing those commands." This records normal observed small upward/downward group movement and both returns; no strain/binding/unexpected tilt reported.
- Theoretical-5% commands for this firmware are [1475,1620,1523,1620,1475,1525] µs; no independent negative-target status table was supplied. Do not label calculated values as physical measurements.
- Last user-reported physical state: returned normally to neutral. Final all-six commanded neutral status was not separately supplied. Upper/lower allowed travel still±5% unless user sends further commands.
- No group endpoints, measured height/tilt, electrical/current data, payload proof or timed endurance result established. User may expand each completed boundary in5-percentage-point stages only after quiet/clear inspection. Actual combined travel may stop before100% recorded individual software endpoints.
