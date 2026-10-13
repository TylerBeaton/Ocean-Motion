import math
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from telemetry_math import (
    fusion_delta_from_unity_pose,
    parse_telemetry_rows,
    quaternion_to_matrix,
)


class TelemetryMathTests(unittest.TestCase):
    def test_identity_pose_stays_identity(self):
        rotation, translation_cm = fusion_delta_from_unity_pose(
            (0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0), 100.0, 1.0
        )
        self.assertEqual(rotation, ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)))
        self.assertEqual(translation_cm, (0.0, 0.0, 0.0))

    def test_unity_axes_map_to_fusion_x_y_z(self):
        _, translation_cm = fusion_delta_from_unity_pose(
            (1.0, 2.0, 3.0), (0.0, 0.0, 0.0, 1.0), 100.0, 0.5
        )
        self.assertEqual(translation_cm, (50.0, 150.0, 100.0))

    def test_quaternion_is_normalized(self):
        rotation = quaternion_to_matrix((0.0, 0.0, 0.0, 2.0))
        self.assertAlmostEqual(rotation[0][0], 1.0)
        self.assertAlmostEqual(rotation[1][1], 1.0)
        self.assertAlmostEqual(rotation[2][2], 1.0)

    def test_parser_rejects_wrong_schema(self):
        csv_text = "schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w\n2,0,0,0,0,0,0,0,0,1\n"
        with self.assertRaisesRegex(ValueError, "schema version"):
            parse_telemetry_rows(csv_text.splitlines())

    def test_parser_reads_valid_row(self):
        csv_text = "schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w\n1,7,1.25,1,2,3,0,0,0,1\n"
        rows = parse_telemetry_rows(csv_text.splitlines())
        self.assertEqual(rows[0]["sequence"], 7)
        self.assertTrue(math.isclose(rows[0]["time_seconds"], 1.25))
        self.assertEqual(rows[0]["position_m"], (1.0, 2.0, 3.0))


if __name__ == "__main__":
    unittest.main()
