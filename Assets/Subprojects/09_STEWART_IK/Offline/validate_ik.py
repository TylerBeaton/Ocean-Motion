"""Produce reviewable numerical checks for the current provisional geometry."""
import argparse
import hashlib
import json
import math
from pathlib import Path

from geometry_io import load_geometry, report_solution
from stewart_ik import Pose

ROOT = Path(__file__).resolve().parents[1]


def axis_quaternion(axis, degrees):
    length = math.hypot(*axis)
    half = math.radians(degrees) / 2
    return tuple(x / length * math.sin(half) for x in axis) + (math.cos(half),)


def summarize_report(report):
    """Keep reviewable metrics; detailed per-leg reconstruction is opt-in."""
    summary = {key: value for key, value in report.items() if key != "cases"}
    summary["report_format"] = "summary"
    summary["cases"] = []
    for case in report["cases"]:
        result = case["result"]
        summary["cases"].append({
            "case": case["case"], "expected_solution": case["expected_solution"],
            "solution_found": result["kinematic_solution_found"], "passed": case["passed"],
            "max_rod_length_residual_mm": max(
                (leg["rod_length_residual_mm"] for leg in result["legs"]), default=None),
            "errors": result["errors"]})
    summary["neutral_reference"] = [
        {key: row[key] for key in ("channel", "angle_deg_from_reference", "branch")}
        for row in report["cases"][0]["result"]["legs"]]
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--geometry", type=Path, default=ROOT / "Measurements/offline-geometry.json")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "Reports")
    parser.add_argument("--full-report", type=Path,
                        help="Also write detailed per-leg results to this local JSON path")
    args = parser.parse_args()
    if args.full_report and args.full_report.resolve() == (args.output_dir / "offline-check-report.json").resolve():
        parser.error("--full-report must differ from the summary report path")
    model, data = load_geometry(args.geometry)
    fixtures = [("neutral", Pose(), True)]
    for name, axis in (("sway_X", (1,0,0)), ("heave_Y", (0,1,0)), ("surge_Z", (0,0,1))):
        for sign in (-1, 1):
            translation = tuple(sign*.001*x for x in axis)
            fixtures.append((f"Unity_{name}_{sign:+d}mm", Pose.unity(translation, (0,0,0,1)), True))
    for name, axis in (("pitch_X", (1,0,0)), ("yaw_Y", (0,1,0)), ("roll_Z", (0,0,1))):
        for sign in (-1, 1):
            fixtures.append((f"Unity_{name}_{sign:+d}deg",
                             Pose.unity((0,0,0), axis_quaternion(axis, sign)), True))
    fixtures.append(("Unity_small_combined_pose",
                     Pose.unity((.0005,.00075,-.0005), axis_quaternion((1,2,3), .5)), True))
    fixtures.append(("reject_1m_heave", Pose.unity((0,1,0), (0,0,0,1)), False))
    cases = []
    for name, pose, expected in fixtures:
        result = report_solution(model, data, pose)
        cases.append({"case": name, "expected_solution": expected,
                      "passed": result["kinematic_solution_found"] == expected,
                      "result": result})

    # A continuous circle of small tilt/heave poses; no hardware or physics plant.
    previous = None
    max_step, max_residual = 0., 0.
    continuity_passed = True
    path_errors = []
    samples = 181
    for index in range(samples):
        phase = math.tau * index / (samples - 1)
        pose = Pose.unity((0, .0015*math.sin(phase), 0),
                          axis_quaternion((math.cos(phase),0,math.sin(phase)), 1))
        solved = model.solve(pose, previous, max_step_degrees=1. if previous else None)
        if not solved.found:
            continuity_passed = False
            path_errors.extend(solved.errors)
            break
        if previous:
            max_step = max(max_step, max(abs(a-b) for a,b in zip(solved.angles_deg, previous)))
        max_residual = max(max_residual, max(row["rod_length_residual_mm"] for row in solved.legs))
        previous = solved.angles_deg
    report = {
        "title": "Provisional offline rotary IK mathematical checks",
        "hardware_output_enabled": False,
        "mechanical_workspace_validated": False,
        "geometry_sha256": hashlib.sha256(args.geometry.read_bytes()).hexdigest(),
        "passed": all(case["passed"] for case in cases) and continuity_passed,
        "case_count": len(cases), "cases": cases,
        "continuity_path": {"requested_samples": samples, "passed": continuity_passed,
                            "max_angle_step_deg": max_step,
                            "max_rod_length_residual_mm": max_residual,
                            "errors": path_errors},
        "unverified_assumptions": data["unverified_assumptions"]
    }
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / "offline-check-report.json").write_text(
        json.dumps(summarize_report(report), indent=2, allow_nan=False) + "\n")
    if args.full_report:
        args.full_report.parent.mkdir(parents=True, exist_ok=True)
        args.full_report.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n")
    lines = ["# Provisional offline IK — numerical check report", "",
             "**No hardware output. Deferred physical horn-side offsets remain CAD placeholders.**", "",
             f"Geometry SHA-256: `{report['geometry_sha256']}`", "",
             "This checks mathematical closure of the configured geometry. Tiny residuals describe floating-point calculations, not physical measurement accuracy or a safe mechanical workspace.", "",
             "| Case | Expected | Result | Max rod residual (mm) |",
             "| --- | --- | --- | --- |"]
    for case in cases:
        r = case["result"]
        residual = max((leg["rod_length_residual_mm"] for leg in r["legs"]), default=None)
        text = f"{residual:.3g}" if residual is not None else "—"
        lines.append(f"| {case['case']} | {'solve' if case['expected_solution'] else 'reject'} | {'PASS' if case['passed'] else 'FAIL'} | {text} |")
    lines += ["", "## Neutral reference solution", "",
              "Angles are signed about the configured P→Q shaft axes from each CAD radial reference. They are NOT Arduino angles or PWM commands. The initial branch choice is provisional.", "",
              "| Channel | CAD axial placeholder (mm) | Neutral angle from CAD reference (deg) | Branch |",
              "| --- | --- | --- | --- |"]
    for leg, solved in zip(model.legs, cases[0]["result"]["legs"]):
        lines.append(f"| {leg.channel} | {leg.axial_offset_mm:.6f} | {solved['angle_deg_from_reference']:+.6f} | {solved['branch']:+d} |")
    lines += ["", "## Continuous small-pose path", "",
              f"{samples} requested samples: {'PASS' if continuity_passed else 'FAIL'}. Maximum consecutive motor-angle change: {max_step:.6f}°. Maximum rod closure residual: {max_residual:.3g} mm.", "",
              "No path collision or whole-platform Jacobian test is implied by this continuity check.", "",
              "## Remaining assumptions", ""]
    lines += [f"- {item}" for item in data["unverified_assumptions"]]
    lines += ["", "## Next gate", "",
              "Review the provisional geometry/reference directions before Unity visualization. Reconcile deferred physical horn geometry, angle-to-pulse calibration, bounds and whole-pose checks before a separately authorized hardware integration.", ""]
    (args.output_dir / "offline-check-report.md").write_text("\n".join(lines))
    print(f"{'PASS' if report['passed'] else 'FAIL'}: {len(cases)} pose fixtures; continuous path {samples} samples; max step {max_step:.6f} deg; max rod residual {max_residual:.3g} mm. OFFLINE ONLY.")
    return 0 if report["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
