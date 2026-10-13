import math
import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "UnityJointVisualPlayback.py"


class VisualTargetsTests(unittest.TestCase):
    def test_full_sequence_is_prompt_default_and_preview_remains_selectable(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            choose = runpy.run_path(str(SCRIPT))["_choose_mode"]
        class UI:
            response = "full"
            def inputBox(self, prompt, title, default):
                self.assert_default = default
                return self.response, False
        ui = UI()
        self.assertEqual(choose(ui), "full")
        self.assertEqual(ui.assert_default, "full")
        ui.response = "preview"
        self.assertEqual(choose(ui), "preview")
        ui.response = "pose-test"
        self.assertEqual(choose(ui), "pose-test")

    def test_full_mode_scales_whole_capture_inside_smaller_visual_envelope(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            targets = runpy.run_path(str(SCRIPT))["_visual_targets"]
        s = math.sin(math.radians(5))
        c = math.cos(math.radians(5))
        rows = [{"position_m": (0, 0, 0), "quaternion": (0, 0, 0, 1)},
                {"position_m": (0, 1, 0), "quaternion": (s, 0, 0, c)}]
        result = targets(rows, translation_scale=0.012, rotation_scale=0.18)
        self.assertAlmostEqual(result[1][0], 12.0)
        self.assertAlmostEqual(result[1][1], -1.8)

    def test_full_mode_includes_first_and_last_recorded_frames(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            select = runpy.run_path(str(SCRIPT))["_select_frames"]
        rows = [{"sequence": i, "time_seconds": i * 0.2} for i in range(40)]
        frames = select(rows, full=True)
        self.assertEqual(frames[0]["sequence"], 0)
        self.assertEqual(frames[-1]["sequence"], 39)
        self.assertGreater(len(frames), 15)

    def test_visual_joint_cap_allows_observed_excursion_but_remains_bounded(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            acceptable = runpy.run_path(str(SCRIPT))["_within_visual_joint_travel"]
        self.assertTrue(acceptable(30.5))
        self.assertFalse(acceptable(35.01))
        self.assertFalse(acceptable(float("inf")))

    def test_joint_limit_report_names_the_coupled_leg_that_exceeded_travel(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            peak = runpy.run_path(str(SCRIPT))["_peak_joint_travel"]
        first = types.SimpleNamespace(assemblyContext=types.SimpleNamespace(fullPathName="Leg Assembly:1"))
        second = types.SimpleNamespace(assemblyContext=types.SimpleNamespace(fullPathName="Leg Assembly:2"))
        motions = [(first, types.SimpleNamespace(rotationValue=math.radians(2))),
                   (second, types.SimpleNamespace(rotationValue=math.radians(31)))]
        degrees, name = peak(motions, (0.0, 0.0))
        self.assertAlmostEqual(degrees, 31)
        self.assertEqual(name, "Leg Assembly:2")

    def test_slow_fusion_solve_does_not_add_csv_delay(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            delay = runpy.run_path(str(SCRIPT))["_playback_delay"]
        self.assertAlmostEqual(delay(100.0, 2.4, 2.0, 100.1), 0.3)
        self.assertEqual(delay(100.0, 2.4, 2.0, 100.8), 0.0)

    def test_unity_heave_pitch_roll_map_to_scaled_fusion_visual_targets(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            targets = runpy.run_path(str(SCRIPT))["_visual_targets"]
        neutral = {"position_m": (0, 0, 0), "quaternion": (0, 0, 0, 1)}
        s = math.sin(math.radians(5))
        c = math.cos(math.radians(5))
        heave_pitch = {"position_m": (0, 1, 0), "quaternion": (s, 0, 0, c)}
        heave_roll = {"position_m": (0, 1, 0), "quaternion": (0, 0, s, c)}
        actual = targets([neutral, heave_pitch, heave_roll])
        self.assertEqual(actual[0], (0, 0, 0))
        self.assertAlmostEqual(actual[1][0], 20.0)
        self.assertAlmostEqual(actual[1][1], -3.0)
        self.assertAlmostEqual(actual[1][2], 0.0)
        self.assertAlmostEqual(actual[2][2], -3.0)

    def test_preview_stops_before_joint_cap_on_verified_capture(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            select = runpy.run_path(str(SCRIPT))["_select_frames"]
        rows = [{"sequence": i, "time_seconds": i * 0.2} for i in range(40)]
        frames = select(rows)
        self.assertEqual(len(frames), 19)
        self.assertAlmostEqual(frames[0]["time_seconds"], 2.0)
        self.assertAlmostEqual(frames[-1]["time_seconds"], 5.6)


if __name__ == "__main__":
    unittest.main()
