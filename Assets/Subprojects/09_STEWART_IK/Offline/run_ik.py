"""Solve one pose offline; intentionally no serial/PWM output option."""
import argparse
import json
from pathlib import Path
import sys

from geometry_io import load_geometry, report_solution
from stewart_ik import Pose

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--geometry", type=Path, default=ROOT / "Measurements/offline-geometry.json")
    parser.add_argument("--translation-mm", type=float, nargs=3, metavar=("X", "Y", "Z"),
                        help="Neutral-relative Fusion control-frame translation in mm")
    parser.add_argument("--rotation-xyzw", type=float, nargs=4,
                        help="Neutral-relative Fusion quaternion; default identity")
    parser.add_argument("--unity-pose", type=Path,
                        help="JSON with neutral-relative position_m and rotation_xyzw")
    parser.add_argument("--output", type=Path, help="Save a JSON diagnostic report, not actuator commands")
    args = parser.parse_args()
    if args.unity_pose and (args.translation_mm is not None or args.rotation_xyzw is not None):
        parser.error("Use either Unity input or Fusion pose arguments, not both")
    try:
        model, geometry = load_geometry(args.geometry)
        if args.unity_pose:
            incoming = json.loads(args.unity_pose.read_text())
            pose = Pose.unity(incoming["position_m"], incoming["rotation_xyzw"])
        else:
            pose = Pose.fusion(args.translation_mm or (0, 0, 0),
                               args.rotation_xyzw or (0, 0, 0, 1))
        report = report_solution(model, geometry, pose)
        exit_code = 0 if report["kinematic_solution_found"] else 2
    except (ValueError, TypeError, KeyError, ArithmeticError, OSError) as exc:
        report = {"kinematic_solution_found": False, "hardware_output_enabled": False,
                  "mechanical_workspace_validated": False, "errors": [str(exc)], "legs": []}
        exit_code = 2
    content = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content)
    print(content, end="")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
