"""Editable offline geometry contract. No hardware-ready mode exists here."""
import json
from pathlib import Path

from stewart_ik import GeometryError, Leg, Model


def load_geometry(path):
    data = json.loads(Path(path).read_text())
    if data.get("schema_version") != 1 or data.get("length_units") != "mm":
        raise GeometryError("Expected geometry schema_version 1 and millimetres")
    if data.get("coordinate_frame") != "fusion_z_up":
        raise GeometryError("Expected the documented Fusion Z-up geometry frame")
    legs = [Leg(channel=row["channel"],
                shaft_point_mm=row["shaft_point_mm"],
                shaft_axis=row["shaft_axis"],
                axial_offset_mm=row["axial_offset_mm"],
                reference_radial_direction=row["reference_radial_direction"],
                top_local_mm=row["top_local_mm"],
                horn_radius_mm=row["horn_radius_mm"],
                rod_length_mm=row["rod_length_mm"],
                angle_limits_deg=row.get("angle_limits_deg")) for row in data["legs"]]
    model = Model(legs, data["neutral_pose"]["position_mm"],
                  data["neutral_pose"]["rotation_xyzw"])
    return model, data


def report_solution(model, data, pose, previous_angles_deg=None, max_step_degrees=None):
    result = model.solve(pose, previous_angles_deg, max_step_degrees).as_dict()
    result.update({"geometry_status": data["status"],
                   "unverified_assumptions": data.get("unverified_assumptions", []),
                   "angle_reference": "CAD radial reference, right-hand about P-to-Q shaft axis; NOT servo PWM or calibrated servo degrees",
                   "physical_branch_confirmed": False,
                   "pose_translation_fusion_mm": list(pose.translation_mm),
                   "pose_rotation_fusion_matrix": [list(row) for row in pose.rotation]})
    return result
