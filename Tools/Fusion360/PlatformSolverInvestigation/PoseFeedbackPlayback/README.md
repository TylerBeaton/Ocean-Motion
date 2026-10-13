# PoseFeedbackPlayback — runnable experimental candidate

**Native Fusion validation is pending.** This is a real Fusion Script entry point, not a plan or a claim that the finalized model has animated correctly. Host tests prove numerical/control behavior on explicit software plants only.

Unlike top reconnection, this candidate only drives the six existing ordinary revolutes. Unlike `UnityJointVisualPlayback`, it measures a new **full six-DOF** Jacobian at every correction and includes unwanted world X/Y translation and orientation/yaw in feedback. It does not import or alter either candidate.

## Morning run

1. Open an **independent disposable copy** of the finalized assembly in Fusion. Keep the original closed. Do not save any candidate-driven document; close this copy without saving after observation.
2. Design workspace → Utilities → Scripts and Add-Ins → Scripts → add/select this **PoseFeedbackPlayback folder**, then Run. Keep `PoseFeedbackPlayback.py`, its matching manifest, `core.py`, and `controller.py` together.
3. Read the unknown-graph warning and type **`DISPOSABLE COPY`** exactly to consent to bounded drives. Cancel or any other text does not drive.
4. Select root occurrence **`Top Platform:1`**, then root **`Base Hexagon:1`**. The base must already be grounded. No grounding changes are made. Selections must match these exact root paths.
5. Accept the CSV path default, which resolves to `../../UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv`, or enter another CSV with the exact schema below.
6. Accept **`PREVIEW`**: up to 31 evenly spaced recorded samples spanning the capture, including the endpoints. Enter **`FULL`** to process every row (278 in the supplied recording). FULL is optional and potentially slow; every mode captures detailed measurements. Reports can become large because all root-context occurrence transforms are recorded repeatedly.
7. First the script attempts a nonzero proof: **+0.5 mm world-Z heave, +0.10° / −0.08° local tilt, held world heading**. It requires target tracking, a stationary base, and **all six distinct servo endpoint pairs rotating relative to each other** by >0.002°. It then verifies complete restoration before starting the CSV. If this proof fails, **no CSV playback follows**.
8. Watch the progress dialog. Cancel requests a stop at the next checked boundary; native API calls cannot be preempted. On completion, the final-pose message holds the pose for observation. Click OK for bounded restoration, then read the final restoration result and report path.
9. **DO NOT SAVE** if any restoration is unverified. Close the disposable copy without saving. No failure should be worked around by increasing limits or disabling checks without investigating its JSONL report.

No Python package install is needed inside Fusion. It uses the standard library plus Fusion's installed `adsk` API. The host Python environment cannot execute the native Fusion side.

## What the script will and will not inspect/change

- Positively locates exactly one ordinary, readable revolute in each root `Leg Assembly:1` through `Leg Assembly:6`. Uses contextual ordinary joints and **`RevoluteJointMotion.cast(...)`** before reading/writing `rotationValue` (API radians, controller degrees).
- Requires readable distinct body endpoints in each leg's root assembly context and records their full paths, initial angles, and enabled limits. Ambiguous/missing joints, unreadable endpoint/angle/limit data, or missing occurrence transforms abort before probes.
- Reads every `rootComponent.allOccurrences` transform via `transform2`, as well as all six angles, to assess motion and restoration. This is not a full constraint-health audit.
- Does **not** inspect root unknown-joint endpoint getters, enumerate `allAsBuiltJoints`, or touch contextual as-built suppression. Prior observed failures for root **Rigid 4 / Rigid 10 endpoints** and six contextual **Rigid 7 suppression getters** remain explicitly unresolved. The consent dialog repeats that warning. Avoiding failing getters does not mean those constraints are healthy.
- No locking, unlocking, constraint suppression, joint redefinition, deletion, grounding edits, top/occurrence transform writes, document saves, or actuator/network output. Only existing ordinary joint `rotationValue` values are written.

## Telemetry contract

Exact header:

```csv
schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w
```

Schema version must be 1, sequence must be increasing integral values, time must increase, and every value must be finite. Quaternion norm must be reasonable before normalization. The parser exercises the actual source file, not synthetic replacement telemetry.

- **First CSV row is neutral**, paired with the selected top's initial Fusion pose.
- Quaternion order XYZW. Compute calibrated local rotation `R_first^T R_row`; change basis with **`C R C`**, where C swaps Y/Z. C has determinant −1: rotation-vector signs must reflect that, not merely swap quaternion components.
- Unity X = pitch, Y = yaw, Z = roll. After basis change, retain Fusion-local X/Y tilt rotation-vector components, scale by **0.08**, discard mapped yaw; exponentiate to an orientation relative to the selected top's neutral rotation.
- Explicit world-heading correction preserves the neutral top-local X axis's projection into Fusion world XY, so a tilted neutral cannot inject world-Z yaw. Near-vertical heading singularities abort.
- Heave: **`(row.pos_y_m − first.pos_y_m) × 100 × 0.005` cm** along Fusion world Z.
- Unity horizontal translation and yaw are intentionally ignored; Fusion world X/Y positions and world heading remain held. This is a small visual mechanism experiment, not a complete boat-motion reproduction or hardware mapping.
- All target orientations are matrices; no wrapped Unity Euler angles are used. Full target-current orientation feedback uses a world-frame rotation-vector logarithm.

## Numerical controller and hard gates

Translation residual is in **mm**, rotation residual in **degrees**, each numerical unit weighted equally (1 mm and 1° have equal least-squares cost). Joint input units are degrees. There is no NumPy dependency.

| Gate | Setting |
|---|---:|
| Finite-difference probe | ±0.25°; choose negative if positive violates a limit |
| Individual angular update | ≤1° from current readback, including each sequential setter |
| Total angle excursion | ≤10° from each initial angle |
| Top total displacement / orientation | ≤5 mm / 3° from neutral, for targets and measured states |
| Base motion | ≤0.01 mm / 0.005° |
| Convergence | translation norm ≤0.08 mm; rotation norm ≤0.04° |
| Restoration | every occurrence ≤0.01 mm / 0.005°; every joint ≤0.01° |
| Probe input readback deviation | ≤0.02° |
| Minimum probe response norm | ≥0.0001 in scaled mm/degree residual space |
| Correction iterations per frame | 4 |
| Drive-batch budget per frame | 60, last 6 reserved for probe restoration |
| Individual angle setter budget per frame | 360 |
| Final original-angle recovery | ≤12 batches, each joint step ≤1°, then complete-pose verification |

Every enabled minimum/maximum joint limit is read and enforced, never changed. The plate envelope includes translation in **all directions** and the **whole rotation**, not only requested heave/tilt. Limits are checks on requested input and observed states, **not a guarantee against unobserved internal solver excursions or collisions**.

Each probe captures the entire pre-probe state, applies a small bounded drive, measures actual top and body response, then attempts one restoration and verifies all occurrence poses/angles. A failed restoration marks the run untrustworthy and blocks all later probes/playback and further cleanup edits. There is no invented response column or fallback fixed Jacobian. The six-by-six undamped matrix must pass a pivot/rank gate (relative pivot threshold 1e−5) before damped normal equations are solved (damping 0.05). This is a conservative numerical rank screen, not a formal native mechanism conditioning certificate. Corrections scale together to respect the 1° cap, remeasure, require improvement, and repeat within the frame budget.

**Sequential Fusion drives are not atomic.** Each setter can trigger a coupled solve; pumping events or refreshing a viewport does not change that. This candidate measures after each actual setter and logs intermediate states, but it may still pop or abort. Finite differences and restoration are visible motion. Recomputing six probes per correction costs time: timestamps provide best-effort dwell only, with no catch-up and **no real-time playback guarantee**.

## Reports and acceptance

A unique exclusive-create file is written under `reports/pose-feedback-<timestamp>-<uuid>.jsonl` and flushed after each event. Includes consent, unresolved graph warning, source/row indices, discovered joint endpoints/limits, initial all-occurrence transforms, each setter, each probe/response, complete restoration state and residual failures, Jacobians, frame targets/actuals/full residuals, articulation evidence, errors/tracebacks, and final restoration status. The file is a measurement record, not an automatic whole-graph PASS label.

A useful **native** acceptance run must show: proof gate accepted, six body-relative articulation records, base stationary, every chosen CSV frame converged, initial occurrence poses/angles restored, and satisfactory visual motion. A solver refusal is an honest negative result, not successful animation. Stop and inspect that report rather than converting a failed probe into a guessed model.

## Host verification

From this folder:

```sh
python3 -m unittest discover -s tests -v
python3 -m py_compile PoseFeedbackPlayback.py core.py controller.py
```

Tests include full-six-axis cross-coupled DLS, rank/unresponsive rejection, signed quaternion mapping, world heading with tilted neutral, actual 278-row CSV parsing/target envelopes, bounded iterative control, failed restoration stop behavior, and explicit software-plant replay of all source frames. Discovery/body-relative evidence tests use protocol fakes, not Autodesk execution. **These results are not native articulation, collision, smoothness, timing, or full-graph evidence.**
