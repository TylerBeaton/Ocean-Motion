import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from joint_solver import solve_min_norm


class SolverTests(unittest.TestCase):
    def test_visual_envelope_still_rejects_unbounded_solver_drift(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        script = pathlib.Path(__file__).resolve().parents[1] / "JointPoseProbe.py"
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            acceptable = runpy.run_path(str(script))["_within_visual_envelope"]
        self.assertTrue(acceptable((0.282, 0.095, 0.057)))
        self.assertFalse(acceptable((6.0, 0.095, 0.057)))
        self.assertFalse(acceptable((0.282, 3.0, 0.057)))
        self.assertFalse(acceptable((0.282, 0.095, 3.0)))

    def test_response_uses_local_pose_before_each_joint_probe(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        script = pathlib.Path(__file__).resolve().parents[1] / "JointPoseProbe.py"
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            response = runpy.run_path(str(script))["_local_response"]
        identity = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0.2))
        moved = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0.25))
        for actual, expected in zip(response(identity, moved, 1), (0.5, 0.0, 0.0)):
            self.assertAlmostEqual(actual, expected)

    def test_second_restoration_sweep_recovers_pose_before_continuing(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        script = pathlib.Path(__file__).resolve().parents[1] / "JointPoseProbe.py"
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            restore = runpy.run_path(str(script))["_restore_to_baseline"]
        state = {"sweeps": 0}
        def sweep(_joints, _originals):
            state["sweeps"] += 1
        def measure(_baseline, _current):
            return ((0.119, 0.048) if state["sweeps"] == 1 else (0.02, 0.01))
        motions = [types.SimpleNamespace(rotationValue=0.0) for _ in range(6)]
        joints = [(None, motion) for motion in motions]
        with patch.dict(restore.__globals__, {"_restore_all_joints": sweep,
                                              "_drift": measure, "_top_pose": lambda root: root}):
            readings = restore(joints, (0.0,) * 6, object(), object())
        self.assertEqual(state["sweeps"], 2)
        self.assertEqual(readings, [(0.119, 0.048, 0.0), (0.02, 0.01, 0.0)])

    def test_restoration_resets_all_joints_affected_by_probe(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        script = pathlib.Path(__file__).resolve().parents[1] / "JointPoseProbe.py"
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            restore = runpy.run_path(str(script))["_restore_all_joints"]
        motions = [types.SimpleNamespace(rotationValue=value) for value in (0.0, 0.0, 0.3, 0.0, -0.4, 0.0)]
        joints = [(None, motion) for motion in motions]
        with patch.dict(restore.__globals__, {"_set_angle": lambda motion, angle: setattr(motion, "rotationValue", angle)}):
            restore(joints, (0.0,) * 6)
        self.assertEqual([motion.rotationValue for motion in motions], [0.0] * 6)

    def test_joint_residual_handles_wraparound(self):
        import math
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        script = pathlib.Path(__file__).resolve().parents[1] / "JointPoseProbe.py"
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            signed_angle_difference_degrees = runpy.run_path(str(script))["signed_angle_difference_degrees"]
        self.assertAlmostEqual(signed_angle_difference_degrees(0, math.radians(359.6)), -0.4)

    def test_three_independent_axes(self):
        columns = [(1, 0, 0), (0, 1, 0), (0, 0, 1), (0, 0, 0)]
        increments = solve_min_norm(columns, (1, 2, -3), damping=0)
        self.assertEqual(increments, (1, 2, -3, 0))

    def test_singular_response_is_rejected_without_damping(self):
        with self.assertRaises(ValueError):
            solve_min_norm([(1, 0, 0)] * 6, (0, 1, 0), damping=0)

    def test_damping_keeps_zero_response_finite(self):
        self.assertEqual(solve_min_norm([(0, 0, 0)] * 6, (1, 2, 3)), (0,) * 6)


if __name__ == "__main__":
    unittest.main()
