# ManualDragCompare

Read-only, two-run recorder for the existing finalized model. **No script-driven motion, constraint edits, activation changes, saves, rebuilds, or pose restores.** Only local JSON reports and a temporary in-memory observer are created. This script is independent of the parent investigation helpers.

## Morning procedure

1. Open the intended design and **manually activate its root component**. Add this folder under Fusion **Utilities → Scripts and Add-Ins → Scripts**; run `ManualDragCompare`.
2. Enter **START**. Select the **Top Platform:1** and **Base Hexagon:1** occurrences (confirm your actual intended instances). The script discovers exactly six ordinary revolute joints using root `allJoints`, and records their endpoint occurrences, root-context 4×4 matrices, and readable angles. If the count differs, it rejects rather than guessing.
3. **Dismiss the START recorded dialog and let the script return.** A modal dialog prevents dragging; do not try dragging while it is open. START does not wait for the drag.
4. Manually drag the top in the normal Fusion UI. Do not edit the design/constraints, close/reopen, or run another model-mutating script between samples. Leave the resulting pose as-is.
5. Run again and enter **FINISH**. It reads the same live objects and exports the full before/after report. It never restores the starting pose. Each successful FINISH consumes the baseline; explicitly run START for another comparison.

## Identity and invalidation

The baseline is retained only in a uniquely named Python `sys.modules` state holder with the **live Fusion Document object**. FINISH checks `Document.isValid` and native `Document.__eq__`, plus the retained root, occurrence and servo-pair identities. It does **not** authenticate by document name, `creationId`, disk path, entity-token-string comparison, or an invented session-ID API. A UUID identifies the recorder baseline, not the document.

Disk START reports are evidence, **never reloadable baselines**. Fusion restart, interpreter reset/state loss, and close/reopen require a new START. The documented `Application.documentClosing` observer conservatively invalidates on **any document close attempt**, including other documents and cancelled closes. Script reload alone retains state if Fusion retains the Python interpreter; if it does not, FINISH rejects and requires START. The observer never cancels a close or saves anything.

API surface checked against the installed Autodesk Python bindings: native `core.py` implements `Document.__eq__` via `_core.Document___eq__`; API definitions document `Base.isValid`, `Application.documentClosing`, root-context `Component.allJoints`, `Component.allOccurrences`, `Occurrence.transform2`, and revolute `rotationValue`. Autodesk documents that `creationId` can be shared by copied documents, so it is deliberately not an identity gate. **Actual in-Fusion two-run lifetime behavior still needs the manual exercise above.**

## Reports and interpretation

All outputs stay in this folder:

- `reports/manual-drag-start-<uuid>.json`: full START sample and baseline ID.
- `reports/manual-drag-finish-<uuid>.json`: full before/after samples, top/base world changes, top relative to base, and all six relative endpoint rotations/translations plus raw angle differences when available.
- `reports/manual-drag-rejected-<uuid>.json`: partial observations, explicit errors and traceback when rejected. Export failure is shown in the dialog.

Matrices use centimetres; comparison translations use millimetres; raw angles use radians and rotation differences degrees. Servo rotation is the unsigned 0–180° angle between `inverse(bodyOne) × bodyTwo` before and after, not either body's world rotation. `articulated_count` uses **0.001°**; it is not a requirement that every drag moves all six axes. Angle differences are supplementary, not unwrapped, and do not replace independent body measurements.

Plate motion with zero relative servo motion is **not articulation proof**. Even six moving pairs do not certify constraint health, IK correctness, full travel, or physical safety. Two samples cannot detect motion that returns to the baseline or full revolutions. Base drift and top-relative-to-base changes are reported separately.

No global as-built inventory, health, or suppression flags are read or used as gates. Ordinary-joint type read failures and optional angle failures are explicit `read_errors`; a measured result with any such errors is `MEASURED_WITH_READ_ERRORS`, never silently clean. Missing/nonfinite matrices reject the comparison; optional unreadable/nonfinite angles become `null` with an error. An unreadable joint type means the six identified pairs are measured, but inventory completeness is not certified. Do not interpret the status `MEASURED` as solver validation.

## Local checks / checkpoint

```sh
python3 -m unittest discover -s "/Users/tylerbeaton/Documents/Unity Projects/Ocean Motion/Tools/Fusion360/PlatformSolverInvestigation/ManualDragCompare/tests" -v
```

Tests exercise math, missing/nonfinite data, plate-only versus relative servo motion, copied-name wrong documents, invalid/reopened baseline rejection, serialization, and two separate script runs using **stand-ins**. They do not execute Autodesk's solver. `verification/test-results.txt` is the executed test output. `checkpoints/ManualDragCompare-source.zip` is a folder source checkpoint, **not a Fusion model checkpoint**. No actual manual-drag result is claimed until a user completes START → drag → FINISH in Fusion.
