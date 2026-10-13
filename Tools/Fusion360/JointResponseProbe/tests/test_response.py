import math
import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch


SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "JointResponseProbe.py"


class JointResponseTests(unittest.TestCase):
    def test_cross_joint_drift_excludes_driven_joint(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core,
                                      "adsk.fusion": fusion}):
            drift = runpy.run_path(str(SCRIPT))["_max_other_joint_change"]
        baseline = (0, 0, 0)
        moved = (math.radians(2), math.radians(0.5), 0)
        self.assertAlmostEqual(drift(baseline, moved, 0), 0.5)
        wrapped = (math.radians(2), math.radians(359.6), 0)
        self.assertAlmostEqual(drift(baseline, wrapped, 0), 0.4)

    def test_top_platform_delta_is_mm_and_signed_local_rotation(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core,
                                      "adsk.fusion": fusion}):
            delta = runpy.run_path(str(SCRIPT))["_pose_delta"]
        identity = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0))
        radians = math.radians(2)
        moved = ((1, 0, 0, 0),
                 (0, math.cos(radians), -math.sin(radians), 0),
                 (0, math.sin(radians), math.cos(radians), 0.25))
        translation, rotation = delta(identity, moved)
        self.assertAlmostEqual(translation[2], 2.5)
        self.assertAlmostEqual(rotation[0], 2)
        self.assertAlmostEqual(rotation[1], 0)
        self.assertAlmostEqual(rotation[2], 0)


if __name__ == "__main__":
    unittest.main()
