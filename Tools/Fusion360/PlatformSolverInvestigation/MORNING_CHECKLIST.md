# Morning test package

Status: scripts built and locally tested. Neither has been run against the real Fusion model yet. These are diagnostics toward connected playback, NOT a working automated animation fix. Keep the finalized original untouched; use a disposable copy. No need to rebuild any joints.

## Add the two scripts

In Fusion Scripts and Add-Ins, add each folder separately:

- `/Users/tylerbeaton/Documents/Unity Projects/Ocean Motion/Tools/Fusion360/PlatformSolverInvestigation/JointLocator`
- `/Users/tylerbeaton/Documents/Unity Projects/Ocean Motion/Tools/Fusion360/PlatformSolverInvestigation/ManualDragCompare`

## 1. JointLocator

Run once. It writes a fresh report for Rigid 4, Rigid 10 and every contextual Rigid 7 by inspecting component-owned joint collections (not merely repeating root.allAsBuiltJoints).

The dialog lists matching row IDs and locations. Optionally enter ONE ID to ask Fusion to select that joint; blank/Cancel keeps it diagnostic-only. The report is already saved even if selection fails. No geometry edits, timeline rolls, saves, or constraint changes.

Do not delete anything it finds. Keep the report path and note whether selection highlighted anything useful.

## 2. ManualDragCompare

1. Manually activate the root component. Run START; select Top Platform:1 and Base Hexagon:1 (the grounded lower plate).
2. Dismiss the message so the script finishes. Then manually drag the top a small amount using your normal Fusion workflow. Observe whether all rods stay connected and the bottom stays fixed. No need to reach a prescribed pose.
3. Without closing/reopening the document or editing joints, rerun the script with FINISH.

This reads top/base motion and relative motion of the six servo body pairs. It does not gate on the unrelated broken as-built getters, command motion, or restore the pose. Close the disposable copy without saving after collecting the report.

If START/FINISH rejects, stop and send the message/report rather than modifying the model to make it pass. Fusion interpreter state must survive between runs; restarting/reopening requires START again.

## Send back together

- JointLocator report path plus optional selection result.
- ManualDragCompare FINISH report path, or rejection message/report.
- Whether manual dragging actually moved connected linkages while the base stayed fixed.

Reports live in each script's `reports/` folder. A collected report or six detected rotations alone does not prove constraint closure, accurate IK, or successful automated playback. Existing PlatformSolverInvestigation RUN remains blocked; do not run it yet.

## Local verification / restart

Parent reran: JointLocator 7 tests passed; ManualDragCompare 10 tests passed; original investigation 12 tests passed. Compilation and git diff whitespace checks passed. All 40 frozen existing-file hashes matched. Tests use API stand-ins, not Fusion's native solver.

Parent review corrected ManualDragCompare to cast JointMotion to RevoluteJointMotion before angle reads and added a regression test. The child-created ZIP/test-results artifact predates this parent correction; CURRENT SOURCE and this checklist are authoritative, not that archival snapshot.

Restart entry: `HANDOFF.md` in this same folder. No commits, pushes, or CAD saves performed. No autonomous overnight continuation is configured; resume through the conversation if usage stops.
