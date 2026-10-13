# Platform solver investigation

Status: runnable isolated diagnostic; NOT yet validated inside Fusion. No full CSV playback or successful Stewart-platform solve is claimed.

Current next step: run the script with the new default `AUDIT`, not `RUN`. AUDIT needs no component selections and performs no model writes. It records assembly-context errors alongside native endpoint reads, reference validity, assembly context, and available occurrence paths. A successful audit means the report was collected, NOT that the mechanism or its joints passed validation. Native paths remain component-relative; they are not substituted into the motion experiment.

The second native report found eight unreadable as-built endpoints: six servo mounts plus Rigid 4 and Rigid 10 attached to the top. The top also has six readable nub attachments; the assumption of exactly six total top connections is not established. Do not delete or suppress the extra/unreadable joints to fit the experiment. Resolve their identities and roles first.

## What is different

This tests the historical community workaround documented in RESEARCH.md: suppress the top's attachment joints, place and temporarily ground the top, reconnect those joints, yield to Fusion, then release temporary grounding. It does not claim that a repaint or computeAll forces inverse kinematics.

The experiment requests +1 mm world Z in six absolute steps, without lateral translation or rotation. Each step must pass measurements before the next runs. The pause is 0.5 seconds per step plus Fusion's own solve time. This is a heave prerequisite, not yet pitch/roll or animation-speed validation.

## Run one bounded test

1. Open a disposable copy of the platform design at its normal connected pose. Do not use the original or a document with work you need to keep.
2. In Fusion's Scripts and Add-Ins dialog, add this folder as a script:
   `/Users/tylerbeaton/Documents/Unity Projects/Ocean Motion/Tools/Fusion360/PlatformSolverInvestigation`
3. Run `PlatformSolverInvestigation`. Read the warning and type `RUN` only on the disposable copy.
4. Select the moving top platform COMPONENT, then the stationary base COMPONENT. Use component entries in the Browser; do not select the entire assembly as the base.
5. If the six steps pass, inspect ALL SIX rods/cranks and both ends of every linkage while the final dialog holds the pose. Check that the base stayed still. Press OK to attempt restoration.
6. Copy the final message or error and note the report path. Close the disposable copy WITHOUT SAVING, even if restoration passed.

If preflight says there are not exactly six active top attachment joints and six active unlocked revolutes, stop and provide the report. Do not delete, unlock, or suppress other joints to make the check pass. The topology assumption needs inspection first.

## Evidence and safeguards

- Both ordinary and as-built joints are inventoried using root-context proxies.
- No servo drive angles are commanded. Joint readbacks are supplementary evidence.
- Relative transforms of each servo's two components distinguish actual relative rotation from shared rigid motion or a top-only move.
- All six relative rotations must exceed 0.001 degree from baseline. This is a motion-detection threshold, not a physical safety limit.
- Selected base may move at most 0.05 mm / 0.02 degree; top error at most 0.1 mm / 0.05 degree.
- Signed joint readback excursions and component-relative rotation must remain within 10 degrees from baseline; enabled Fusion angle limits are also checked. These are diagnostic bounds, not measured hardware limits. Native intermediate solves are not bounded or atomic by this script.
- Originally suppressed joints stay suppressed. Top grounding and attachment suppression flags are restored and checked even on an exception.
- If flag cleanup fails, further pose mutations stop. A warning requires closing the copy without saving.
- Otherwise, the starting top pose is requested through the same reconnection path. Restoration checks every captured occurrence (0.1 mm / 0.05 degree) and six angle readbacks (0.05 degree). This is best-effort restoration, not transaction rollback.
- No saves, snapshots, joint-lock changes, geometry-definition changes, Unity modifications, CSV reads, or actuator output.

Raw component poses and joint angles are appended and flushed to a unique `reports/probe-<id>.jsonl` before/after temporary grounding release, with step summaries and restoration results. Reports remain in this folder. Run errors also go to that report when storage is available.

## Limits of this diagnostic

Python tests cover orchestration, cancellation, report writing, finite measurement math, early stopping, angle casting/bounds, and flag cleanup. API stand-ins do NOT emulate or validate Autodesk's native assembly solver. The real Fusion run and visual closure check remain required.

Automatic joint-anchor/rod-end closure verification is not implemented. Six rotating servo components plus an accurate top pose are useful evidence, not proof that every rod remains connected. The held-pose visual check is mandatory. Do not expand to pitch/roll or CSV until that gate and restoration have been reviewed.

## Local verification

From the live repository root:

    python3 -m unittest discover -s Tools/Fusion360/PlatformSolverInvestigation/tests -v
    python3 -m compileall -q Tools/Fusion360/PlatformSolverInvestigation

Latest local run: 12 tests passed. Existing Fusion files were checked against all 40 hashes in `baseline_hashes.json`, with no differences. Resume details are in `HANDOFF.md`.

## First native-run blocker

Fusion 2705.1.25 raised `InternalValidationError : res` while reading an as-built joint's `occurrenceTwo`, before the experiment made any model changes. The original report contains only start/error events, so it cannot identify the offending joint.

Inspection now reads each endpoint once, preserves per-property errors alongside joint names/families/indices, and writes the inventory before checking it. An unreadable joint still blocks motion; the script does not silently ignore it or guess its missing endpoint. Rerun on a disposable copy and supply the new report to identify the joint. This is a diagnostic correction, not a fix for Fusion's internal accessor failure or proof of working motion.
