import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch


SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "UnityBoatTelemetryPlayback.py"


class PlaybackJointTests(unittest.TestCase):
    def test_calm_offset_works_with_cached_four_argument_math_module(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        legacy_math = types.ModuleType("telemetry_math")
        setattr(legacy_math, "fusion_delta_from_unity_pose",
                lambda position, rotation, cm, scale: (rotation, position))
        setattr(legacy_math, "parse_telemetry_rows", lambda lines: [])
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core,
                                      "adsk.fusion": fusion, "telemetry_math": legacy_math}):
            map_frame = runpy.run_path(str(SCRIPT))["_map_frame_pose"]

        rotation, position = map_frame(
            {"position_m": (0.1, 0.352202, 0.2), "quaternion": (0, 0, 0, 1)},
            0.352202,
        )
        self.assertEqual(rotation, (0, 0, 0, 1))
        self.assertAlmostEqual(position[1], 0.0)

    def test_prompts_for_calm_y_offset_in_csv_metres(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            ask_calm_y = runpy.run_path(str(SCRIPT))["_ask_calm_y"]

        prompts = []
        def input_box(message, title, default):
            prompts.append((message, title, default))
            return ("0.352202", False)

        ui = types.SimpleNamespace(inputBox=input_box)
        self.assertEqual(ask_calm_y(ui), 0.352202)
        self.assertIn("CSV", prompts[0][0])
        self.assertEqual(prompts[0][2], "0.352202")

    def test_reads_joint_vector_without_collection_count(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            capture_joint_ranges = runpy.run_path(str(SCRIPT))["_capture_joint_ranges"]

        class JointVector:
            def __iter__(self):
                return iter((joint,))

            def __len__(self):
                return 1

            def __getitem__(self, index):
                return (joint,)[index]

        joint = types.SimpleNamespace(
            name="Actuator 1", jointMotion=types.SimpleNamespace(slideValue=3.0)
        )
        root = types.SimpleNamespace(allJoints=JointVector())
        ranges = {}
        capture_joint_ranges(root, ranges)
        self.assertEqual(ranges[("Actuator 1", "slideValue", "cm")], [3.0, 3.0])


if __name__ == "__main__":
    unittest.main()
