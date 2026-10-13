"""Pure-Python CSV and coordinate conversion helpers for Fusion playback."""

import csv
import math

SCHEMA_VERSION = 1
REQUIRED_COLUMNS = (
    "schema_version", "sequence", "time_seconds",
    "pos_x_m", "pos_y_m", "pos_z_m",
    "rot_x", "rot_y", "rot_z", "rot_w",
)


def _matmul3(a, b):
    return tuple(
        tuple(sum(a[row][k] * b[k][col] for k in range(3)) for col in range(3))
        for row in range(3)
    )


def quaternion_to_matrix(quaternion):
    x, y, z, w = quaternion
    magnitude = math.sqrt(x * x + y * y + z * z + w * w)
    if magnitude <= 1e-12:
        raise ValueError("Rotation quaternion has zero length")
    x, y, z, w = x / magnitude, y / magnitude, z / magnitude, w / magnitude
    return (
        (1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
        (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
        (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)),
    )


def fusion_delta_from_unity_pose(position_m, quaternion, cm_per_m=100.0, motion_scale=1.0):
    """Map Unity (+X right,+Y up,+Z forward) to Fusion (+X right,+Y forward,+Z up)."""
    change_basis = ((1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0))
    unity_rotation = quaternion_to_matrix(quaternion)
    fusion_rotation = _matmul3(_matmul3(change_basis, unity_rotation), change_basis)
    x, y, z = position_m
    factor = cm_per_m * motion_scale
    return fusion_rotation, (x * factor, z * factor, y * factor)


def parse_telemetry_rows(lines):
    reader = csv.DictReader(line for line in lines if line.strip() and not line.lstrip().startswith("#"))
    if reader.fieldnames is None or any(column not in reader.fieldnames for column in REQUIRED_COLUMNS):
        raise ValueError("Telemetry CSV is missing one or more required columns")

    rows = []
    for line_number, row in enumerate(reader, start=2):
        try:
            version = int(row["schema_version"])
            if version != SCHEMA_VERSION:
                raise ValueError("Unsupported schema version {} on row {}".format(version, line_number))
            rows.append({
                "sequence": int(row["sequence"]),
                "time_seconds": float(row["time_seconds"]),
                "position_m": (float(row["pos_x_m"]), float(row["pos_y_m"]), float(row["pos_z_m"])),
                "quaternion": (float(row["rot_x"]), float(row["rot_y"]), float(row["rot_z"]), float(row["rot_w"])),
            })
        except (TypeError, ValueError) as exc:
            if "schema version" in str(exc):
                raise
            raise ValueError("Invalid telemetry value on row {}: {}".format(line_number, exc)) from exc
    if not rows:
        raise ValueError("Telemetry CSV contains no data rows")
    return rows
