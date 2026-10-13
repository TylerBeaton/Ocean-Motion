# JointLocator

Standalone diagnostic Fusion script. No dependency on the parent investigation script or old reports. **Does not repair or change the CAD.** Native Fusion testing is still pending; Python stand-ins do not establish Autodesk behavior.

## Tomorrow: one batch

1. Open the intended Fusion Design. In **Utilities → Scripts and Add-Ins → Scripts**, add this `JointLocator` folder and run **JointLocator** (not the parent investigation script).
2. The script first exports all matching rows to `JointLocator/reports/joint-locator-<unique-id>.json`. The dialog lists row IDs, names, joint kind, context paths, and timeline indices. Leave the optional input blank or Cancel for **diagnostic only**.
3. Only if wanted, enter **one row ID displayed by this run** to try adding that joint to Fusion's current UI selection. Existing selections are retained. False returns, unavailable contextual entities, and exceptions are reported without rewriting the diagnostic. No fallback selects a different joint. Selection is not guaranteed to reveal or scroll the browser/timeline.
4. Keep the JSON and note the final selection status. Do not save the CAD for this diagnostic. If a getter fails, retain its exact error; failure alone is not proof of damaged CAD.

## What the report means

- Fresh enumeration of root `Component.joints` and `Component.asBuiltJoints`, then the owning component's same collections for every occurrence in `rootComponent.allOccurrences`. This avoids confusing `Occurrence.joints` (joints **affecting** an occurrence) with ownership.
- Targets: **Rigid 4 / Rigid 10 / Rigid 7**. Matching ignores whitespace and case, so `Rigid4` also matches, but `Rigid40` does not. All matches are retained, including repeated occurrences of shared component definitions.
- Each row has a run-local `id`, exact `name`, `kind`, `collection_index`, `parent_component`, `context_path`, zero-based `timeline_index`, native endpoints and contextual endpoints, `selectable`, and per-field errors. A timeline index is location evidence, **not** an instruction to move the timeline marker. IDs are not stable across runs.
- `<root>` means root-owned. Nested occurrence paths come from `fullPathName`; repeated Leg Assembly instances remain distinct rows. Native endpoint paths are component-relative; contextual endpoint paths are read from a proxy made with `createForAssemblyContext`. These are separate fields, never silently substituted.
- `null` can mean absent or unreadable; errors explain thrown reads. A missing/null contextual proxy disables selection for that row. `selectable` means an entity reference exists, not that Fusion guarantees selection will succeed.
- Global `errors` records enumeration failures, so a partial/empty report is not proof there are no other joints. No old timeline index, entity token, or old report is used to resolve a joint. Only the active design's root and instantiated hierarchy are inspected; uninstantiated component definitions are outside this scope.

## Safety boundary

Only documented read APIs, contextual proxy creation, dialogs, local report writes, and (after explicit row choice) `ui.activeSelections.add(entity)` are used. No timeline roll, suppression reads/writes, deletion, grounding, transforms, document activation, viewport movement, CAD save, or joint driving. Proxy creation creates an API context wrapper, not a CAD joint. No automatic selection or selection clearing occurs.

The installed Autodesk definitions were checked for `Component.joints`, `Component.asBuiltJoints`, `BaseComponent.allOccurrences`, `Occurrence.fullPathName`, `Joint`/`AsBuiltJoint.parentComponent`, `.timelineObject`, `.occurrenceOne`, `.occurrenceTwo`, `.createForAssemblyContext`, and `Selections.add` (returns boolean).

## Tests

From any directory:

```sh
python3 -B -m unittest discover -s '/Users/tylerbeaton/Documents/Unity Projects/Ocean Motion/Tools/Fusion360/PlatformSolverInvestigation/JointLocator/tests' -v
```

Standard library only. Tests exercise the real locator/export/selection logic against unavoidable API stand-ins; they never launch Fusion. Test exports are temporary directories inside this folder and are removed afterward. Human verification must still confirm real enumeration, API errors and optional highlighting on the intended Fusion design.
