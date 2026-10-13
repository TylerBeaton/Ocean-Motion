# PlatformReconnectPlayback — experimental Unity CSV animation

**This is a runnable Fusion script, not another audit. Native Fusion playback has NOT been validated.** It attempts a +0.5 mm heave proof, then animates multiple poses from the real Unity recording if both numerical and human gates pass. It is not validated inverse kinematics or a hardware controller.

## Run in the morning

1. Open a **disposable copy** of the intended hexapod design in Fusion. Preserve your working model separately. Do not save the experimental copy afterward.
2. In **Utilities → Scripts and Add-Ins → Scripts**, add/select this folder, `PlatformReconnectPlayback`, and Run. Keep it in this repository location: it loads frozen parent helpers by absolute file path derived from `__file__`.
3. Read the warning and type exactly **`EXPERIMENT ON DISPOSABLE COPY`**. Empty/default/cancel makes no model changes.
4. Leave **`PREVIEW`** selected for the first attempt. This uses the first 3 source seconds: **7 CSV poses**, not a synthetic sine wave. `FULL` uses the complete source capture: **27 sampled CSV poses across 13.839995 seconds**, including sequence 277. Full mode still requires the proof and visual gate.
5. Select the actual **`Top Platform:1`** occurrence in the Browser, then the already-grounded **`Base Hexagon:1`** (or **`Base Hexagon Bottom:1`**). Do not select the assembly root. Exact identities and mount endpoints are checked. The older captured audit uses `Base Hexagon Bottom:1`; the current corrected model may use `Base Hexagon:1`. No renaming/grounding of the base is performed by this script.
6. The first mutation is **+0.5 mm world-Z heave**, with full rotation unchanged. Every known attachment is reconnected with the top temporarily grounded, then the original grounding flag is restored and the **released** pose is measured. All six servo crank/housing pairs must exhibit actual relative-body rotation; a moving plate or changed joint angle alone is insufficient.
7. Only after the numerical proof passes, inspect all six rods/cranks **at both ends**. If all remain connected and the base is fixed, type **`CONNECTED`**. Otherwise cancel: CSV is blocked and restoration is attempted. This visual gate is necessary because relative-body motion does not prove every linkage endpoint is closed.
8. The script returns to neutral and plays the selected CSV poses automatically, then attempts the original pose and flag restoration. Do not change documents or manually edit/drag the model during playback. The viewport is refreshed between solves. Solver time is added to source-time waits: **this is deliberately slow stepped animation, not real-time playback**. A native setter can block; there is no claim of an interruptible native solve.
9. Read the completion/error dialog and JSONL report. **Close the disposable copy WITHOUT SAVING even when restoration passes.** If flags fail to restore, further model mutations—including pose restoration—are stopped. Do not retry on the damaged copy; reopen a clean disposable copy.

## Explicitly partial constraint experiment

Only these six readable, active, rigid **as-built** attachments can be suppressed/reconnected:

| Joint | Nub endpoint | Other endpoint |
|---|---|---|
| Nub 1 Rigid | Nub:3 | Top Platform:1 |
| Nub 2 Rigid | Nub(Mirror):1 | Top Platform:1 |
| Nub 3 Rigid | Nub:8 | Top Platform:1 |
| Nub 4 Rigid | Nub(Mirror):2 | Top Platform:1 |
| Nub 5 Rigid | Nub:9 | Top Platform:1 |
| Nub 6 Rigid | Nub(Mirror):3 | Top Platform:1 |

Names alone are insufficient: family, unique match, root-context endpoints, validity, readable unsuppressed state, and actual rigid-motion cast must match. Six ordinary revolutes must connect each `Leg Assembly:1..6+Crankshaft:1` to its `Servo:1`, be valid/active/unlocked, and have readable revolute values/limits. All matching `Servo N Rigid` mounts must terminate at the selected grounded base.

**Unknown constraints are NOT solved, ignored as harmless, or disabled.** The earlier report contains unreadable `Rigid 4` / `Rigid 10` endpoint getters and six `Rigid 7` suppression getters. Read errors remain in the inventory report. These joints are never mutated by this candidate; they may continue constraining the top and cause the proof to abort. This is intentionally not the parent's global clean-graph gate and is **not a fully validated graph**.

The only model writes are the six known attachments' `isSuppressed`, the selected top's `transform2`, and its temporary `isGrounded`. No saves, joint deletion/redefinition, revolute driving, joint lock writes, timeline movement, or physical output. This is an independent Update 07 experiment, not an Update 08 actuator-architecture choice.

## CSV and coordinate contract

Source (relative to `Tools/Fusion360`):

```
UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv
```

Header must be exactly:

```
schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w
```

- All 278 rows are parsed and all requested extrema checked **before any model mutation**, including rows not sampled or outside the preview. Version 1, finite values, strictly increasing time/sequence, and near-unit quaternions are required.
- **The first CSV row is neutral**, both position and orientation. This deliberately does not use an older absolute calm-water Y calibration.
- Heave: `(pos_y_m - first_pos_y_m) × 100 × 0.005` centimetres along Fusion **world Z**. Top world X/Y stay at their selected starting values.
- Rotation: normalize quaternion, compute `inverse(q_first) * q_row`, choose the shortest equivalent quaternion, map its rotation vector by the established `C R C` convention (`C` swaps Y/Z). Since this is an improper basis, the axial vector maps as `-C*v`: Fusion local X = negative Unity local-X pitch, Fusion local Y = negative Unity local-Z roll. Drop mapped Z (Unity yaw), scale X/Y by **0.08**, reconstruct rotation, and compose on the right of the starting top orientation.
- Unity lateral translation and yaw are **intentionally excluded by contract**, not secretly filtered. Rotation-vector yaw removal is not Euler-yaw decomposition; compound rotations may couple, and the report records the exact requested values.
- No recorded servo angles are needed. Joint angles are live observations only.
- Sample real rows at least 0.5 seconds after the preceding chosen row, always including the selected range's final row. No interpolation; floating-point timestamps produce roughly 0.54–0.56 s spacing in this capture.

## Fail-closed runtime gates

| Check | Gate |
|---|---|
| Requested and measured plate excursion from start | ≤5 mm, ≤3° |
| Selected base pose drift | ≤0.02 mm, ≤0.01° |
| Full target pose error, grounded **and** released | ≤0.05 mm, ≤0.02° |
| Proof articulation | All six body-pair relative rotations >0.001° |
| Changed CSV pose articulation | At least one body-pair increment >0.001° when requested increment >0.01 mm or >0.001° |
| Near-neutral/duplicate CSV pose | Explicitly logged `changed_above_resolution=false`; no fictitious articulation claim |
| Servo relative-body rotation from start | ≤10° |
| Servo housing relative to base | ≤0.02 mm, ≤0.01° |
| Live revolute values | Finite, raw/unwrapped excursion ≤10°; every enabled Fusion min/max finite, ordered, and respected |
| Known servo joint health | Healthy only; warnings/errors abort |
| Restoration | Every captured occurrence ≤0.05 mm / ≤0.02°, live angle residuals ≤0.02°, original flags restored |

Read/API errors, pose mismatch, body non-articulation, drift, or limits abort forward playback. Ordinary failures attempt restoration while known flags remain intact; restoration bypasses forward pose/articulation gates so an imperfect recovery can still be measured. Limits/read errors still abort a solve. A flag-cleanup failure, unreadable/changed baseline flags, invalid attachment, or active-document change stops further mutations. Read-only residual reporting is attempted even then. A report I/O failure can also abort the session; its dialog is authoritative if the report is incomplete.

These tolerances are diagnostic choices, not manufactured geometry-clearance certification. Passing sampled readbacks cannot certify continuous travel or physical safety.

## Reports and offline verification

Native attempts append/flush/fsync `reports/playback-<id>.jsonl` here. Events include source settings, full readable/error inventory, baseline root-context poses/angles/flags, every requested CSV target, grounded/released snapshots, full-pose and independent body measurements, actual solve time, visual decision, and restoration residuals. `csv_complete` alone is NOT restoration success; require final `complete` and a true `restored`, and still close unsaved.

Run from this directory:

```sh
python3 -B -m unittest discover -s tests -v
python3 -B verify_csv.py
```

Tests use standard-library stand-ins; the adapter test executes the real frozen reconnection helper and independent measurement functions, and rejects a plate-only movement. **Stand-ins do not prove the native Fusion solver moves this assembly.** `verify_csv.py` reads the actual recording, checks multiple distinct poses and the final row, and prints exact source hash/frames/extrema. No NumPy or other third-party dependency.

Current offline result: 14 tests pass. Real source extrema with the above scales: **3.2622704823 mm** heave and **1.6604747191813591°** rotation. See `verification/` and `CHECKPOINT.md`. No native success report or Unity Editor compile claim is made; no Unity source or scene was changed.
