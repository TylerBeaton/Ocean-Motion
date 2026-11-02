"""Generate a provisional editable offline config from preserved measurements."""
import argparse
import json
from pathlib import Path

from geometry_io import load_geometry
from stewart_ik import Pose

ROOT = Path(__file__).resolve().parents[1]


def build(measurements=ROOT / "Measurements"):
    def read(name):
        return json.loads((Path(measurements) / name).read_text())
    shafts = read("servo-shafts-fusion.json")
    top = read("top-joints-fusion.json")
    horns = read("horn-joints-fusion.json")
    lengths = read("physical-link-lengths.json")
    neutral = read("physical-neutral-pose.json")
    top_by_channel = {row["channel"]: row for row in top["top_joints"]}
    horn_by_channel = {row["channel"]: row for row in horns["horn_joints"]}
    data = {
        "schema_version": 1,
        "status": "OFFLINE_PROVISIONAL_CAD_HORN_OFFSETS_NOT_PHYSICAL_CALIBRATION",
        "length_units": "mm",
        "coordinate_frame": "fusion_z_up",
        "hardware_output_enabled": False,
        "source_measurements": ["servo-shafts-fusion.json", "top-joints-fusion.json",
                                "horn-joints-fusion.json", "physical-link-lengths.json",
                                "physical-neutral-pose.json"],
        "unverified_assumptions": [
            "Horn ball joints mount on the other side in hardware; current axial offsets are explicitly deferred CAD placeholders, not automatically negated physical offsets.",
            "Neutral height is rough; horizontal origin, yaw and level are assumed from the reported model/control frame.",
            "35mm radius and184.5mm rods provisionally apply to all six; matching/tolerances unmeasured.",
            "Initial solution branch is closest to each CAD radial reference at provisional neutral, not physically confirmed.",
            "Pulse-to-angle calibration and actual joint-angle limits are missing;1000..2000us cannot be converted to angle bounds yet.",
            "Collision, ball-joint articulation, full-platform Jacobian singularities, payload and supply capacity are not validated."
        ],
        "neutral_pose": {
            "position_mm": neutral["provisional_physical_top_control_origin_global_mm"],
            "rotation_xyzw": neutral["neutral_control_rotation_quaternion_xyzw"],
            "definition": "Fusion-aligned CONTROL frame at neutral, not the CAD component's local transform"
        },
        "legs": []
    }
    for shaft in shafts["servos"]:
        channel = shaft["channel"]
        horn = horn_by_channel[channel]
        data["legs"].append({
            "channel": channel,
            "shaft_point_mm": shaft["p_mm"],
            "shaft_axis": shaft["axis_p_to_q"],
            "axial_offset_mm": horn["derived_axial_offset_along_P_to_Q_mm"],
            "axial_offset_status": "UNVERIFIED_CAD_PLACEHOLDER_PHYSICAL_SIDE_DIFFERS",
            "reference_radial_direction": horn["CAD_reference_radial_unit_direction"],
            "top_local_mm": top_by_channel[channel]["t_local_mm"],
            "horn_radius_mm": lengths["reported_horn_radius_mm"],
            "rod_length_mm": lengths["reported_pushrod_length_mm"],
            "angle_limits_deg": None
        })
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--measurements", type=Path, default=ROOT / "Measurements")
    parser.add_argument("--output", type=Path, help="New config file; existing files are preserved unless --force")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    data = build(args.measurements)
    content = json.dumps(data, indent=2, allow_nan=False) + "\n"
    if args.output:
        if args.output.exists() and not args.force:
            parser.error("Output exists; edit it in place or explicitly use --force to regenerate")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(content)
        model, _ = load_geometry(args.output)
        if not model.solve(Pose()).found:
            parser.error("Generated provisional neutral could not be solved")
        print(f"Wrote {args.output}; neutral closure checked. OFFLINE ONLY.")
    else:
        print(content, end="")


if __name__ == "__main__":
    main()
