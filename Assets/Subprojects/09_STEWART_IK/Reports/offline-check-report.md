# Provisional offline IK — numerical check report

**No hardware output. Deferred physical horn-side offsets remain CAD placeholders.**

Geometry SHA-256: `6587aeade7f692b0a26cae4a8d3748bfa9bbb73cf817e76001627be6a882880f`

This checks mathematical closure of the configured geometry. Tiny residuals describe floating-point calculations, not physical measurement accuracy or a safe mechanical workspace.

| Case | Expected | Result | Max rod residual (mm) |
| --- | --- | --- | --- |
| neutral | solve | PASS | 0 |
| Unity_sway_X_-1mm | solve | PASS | 2.84e-14 |
| Unity_sway_X_+1mm | solve | PASS | 2.84e-14 |
| Unity_heave_Y_-1mm | solve | PASS | 2.84e-14 |
| Unity_heave_Y_+1mm | solve | PASS | 2.84e-14 |
| Unity_surge_Z_-1mm | solve | PASS | 2.84e-14 |
| Unity_surge_Z_+1mm | solve | PASS | 2.84e-14 |
| Unity_pitch_X_-1deg | solve | PASS | 2.84e-14 |
| Unity_pitch_X_+1deg | solve | PASS | 2.84e-14 |
| Unity_yaw_Y_-1deg | solve | PASS | 2.84e-14 |
| Unity_yaw_Y_+1deg | solve | PASS | 2.84e-14 |
| Unity_roll_Z_-1deg | solve | PASS | 2.84e-14 |
| Unity_roll_Z_+1deg | solve | PASS | 2.84e-14 |
| Unity_small_combined_pose | solve | PASS | 2.84e-14 |
| reject_1m_heave | reject | PASS | — |

## Neutral reference solution

Angles are signed about the configured P→Q shaft axes from each CAD radial reference. They are NOT Arduino angles or PWM commands. The initial branch choice is provisional.

| Channel | CAD axial placeholder (mm) | Neutral angle from CAD reference (deg) | Branch |
| --- | --- | --- | --- |
| 0 | 6.849791 | -2.342617 | -1 |
| 1 | 6.850031 | +1.244572 | +1 |
| 2 | 6.851100 | -2.414578 | -1 |
| 3 | 6.850361 | +1.312741 | +1 |
| 4 | 6.850000 | -2.319647 | -1 |
| 5 | 6.850000 | +1.344703 | +1 |

## Continuous small-pose path

181 requested samples: PASS. Maximum consecutive motor-angle change: 0.165692°. Maximum rod closure residual: 5.68e-14 mm.

No path collision or whole-platform Jacobian test is implied by this continuity check.

## Remaining assumptions

- Horn ball joints mount on the other side in hardware; current axial offsets are explicitly deferred CAD placeholders, not automatically negated physical offsets.
- Neutral height is rough; horizontal origin, yaw and level are assumed from the reported model/control frame.
- 35mm radius and184.5mm rods provisionally apply to all six; matching/tolerances unmeasured.
- Initial solution branch is closest to each CAD radial reference at provisional neutral, not physically confirmed.
- Pulse-to-angle calibration and actual joint-angle limits are missing;1000..2000us cannot be converted to angle bounds yet.
- Collision, ball-joint articulation, full-platform Jacobian singularities, payload and supply capacity are not validated.

## Next gate

Review the provisional geometry/reference directions before Unity visualization. Reconcile deferred physical horn geometry, angle-to-pulse calibration, bounds and whole-pose checks before a separately authorized hardware integration.
