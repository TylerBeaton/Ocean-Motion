# Solver-path research

## Confirmed from the installed Autodesk API wrapper
Source: `/Users/tylerbeaton/Library/Application Support/Autodesk/webdeploy/production/3b43ec78d72add6e53632c3a56d2981bc5441d8b/Autodesk Fusion.app/Contents/Api/Python/packages/adsk/` (`fusion.py`, `core.py`). These are API signatures/documentation, not runtime solver evidence.

- `Design.computeAll()` (fusion.py:103389) is equivalent to Compute All. Its True return does not establish even that all timeline items computed successfully. No documented guarantee that it solves a dragged assembly pose.
- `Occurrence.transform2` (fusion.py:52968) gets/sets position and orientation **in its assembly context**. Context must be checked for nested occurrence measurements.
- `Component.transformOccurrences(occurrences, transforms, ignoreJoints)` (fusion.py:89339) requires the root component and root-context occurrence proxies. With False it promises best-effort transformation honoring joints; it does not promise exact target attainment. Existing user tests have not confirmed articulation through this path.
- `Component.allJoints` and `allAsBuiltJoints` return root-context proxies when accessed on root (fusion.py:89355+). Both joint families must be considered when auditing connectivity; ordinary joints alone are not the full graph.
- `Command.doExecutePreview()` fires the preview event (core.py:7641); it is not documented as an assembly-solver entry point.
- `Command.isPositionDependent` (core.py:7651) controls the Capture Position/Revert prompt before position-dependent commands. It is not documented as a solver-enablement flag.
- `Snapshots.revertPendingSnapshot()` (fusion.py:73183) reverts ALL pending positional changes. Do not call this casually on a user's live document to clean up a test: it can discard pre-existing unsaved positioning.
- No generic public `solveAssembly()` / `updateSolvers()` method was found in the inspected Application, Design, Component, Occurrence, Joint, or Command classes. This is a scoped search result, not proof that no usable path exists.

## Retrieved historical source
https://forums.autodesk.com/t5/fusion-api-and-scripts-forum/major-stability-issues-when-moving-joints-via-api/td-p/8088318

The 2018 delta-robot author reported unstable scalar joint driving and a workaround using joint locks plus avoiding angular wrap boundaries. This is anecdotal/version-specific and concerns **joint-driven forward motion**, not dragging the end effector by transform. Do not misrepresent it as evidence for the requested platform-driven solver path. No lock changes are authorized for existing designs by default.

## Current hypotheses (unconfirmed)
1. Transform writes may bypass the interactive drag solve path even when the plate transform matches exactly. Predict: top moves while servo-body relative transforms and passive endpoint positions remain unchanged.
2. API calls may need the native command/transaction context to produce the same solve as interactive dragging. Predict: identical bounded target with identical root-context occurrence differs between script and command execution. No supporting Autodesk example verified yet.
3. Context/assembly graph or movement of the reference/base may mask the outcome. Predict: explicit world-space base snapshots, joint endpoint records and manual-drag comparison reveal the difference.

Do not implement guessed text commands, add snapshots, or treat a repaint as a proven solve. Constraint suppression is allowed ONLY in the explicitly disclosed, user-confirmed disposable-copy experiment below, with state restoration and readback. All future artifacts belong to this folder only.

## Exact historical match and selected experiment

Parent-verified sources:

- https://forums.autodesk.com/t5/fusion-api-and-scripts-forum/using-transformoccurences/m-p/8971154 — Brian Ekins reports a two-component test in which `ignoreJoints=False` still ignored the joints. Historical evidence, not proof of the same bug in today's installed build.
- https://forums.autodesk.com/t5/fusion-api-and-scripts-forum/how-to-move-a-component-that-has-joints/td-p/8885869 — accepted solution by community contributor quality5B59E, 2019-07-02. The author wanted to move the end effector and recompute the joints, not drive joints. Reported working sequence: suppress the end-effector joints; set occurrence transform; ground end effector; unsuppress those joints; doEvents(); unground. They reported connected parts then moved and joint angles could be read. This is a community workaround for a robot arm, NOT a supported inverse-kinematics guarantee or a verified closed-chain Stewart result.

Select that reconnect/temporary-ground sequence for ONE bounded +1 mm heave experiment. It is an actual new solver trigger candidate backed by a concrete report, unlike speculative command wrapping. Use six equal small steps only after each prior step is measured; stop on pose/base/joint-limit/closure failures. Preserve original suppression and grounding states, do not change joint locks or definitions, and do not run without a disposable-copy confirmation. Exact top movement without independent servo-body motion is inconclusive.

The parallel researcher found no verified command-context trick that turns occurrence transforms into drag solving. `executePreview` rollback, snapshots, and MoveFeature persistence are separate issues, not substitutes for this proof.
