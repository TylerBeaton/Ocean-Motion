import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch


SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "RevoluteJointProbe.py"


class RevoluteJointProbeTests(unittest.TestCase):
    def test_lists_revolute_joints_inside_linked_leg_occurrences(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        setattr(fusion, "RevoluteJointMotion", types.SimpleNamespace(
            cast=lambda motion: motion if motion.kind == "revolute" else None))
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            paths = runpy.run_path(str(SCRIPT))["_native_revolute_paths"]

        joint = types.SimpleNamespace(name="Revolute 1", jointMotion=types.SimpleNamespace(kind="revolute"))
        legs = [types.SimpleNamespace(fullPathName="Leg Assembly:{}".format(i),
                                      component=types.SimpleNamespace(joints=[joint])) for i in (1, 2)]
        root = types.SimpleNamespace(allOccurrences=types.SimpleNamespace(
            count=2, item=lambda index: legs[index]))
        self.assertEqual(paths(root), ["Leg Assembly:1/Revolute 1", "Leg Assembly:2/Revolute 1"])

    def test_uses_root_context_revolute_joint_and_ignores_other_joint_types(self):
        adsk = types.ModuleType("adsk")
        core = types.ModuleType("adsk.core")
        fusion = types.ModuleType("adsk.fusion")
        setattr(adsk, "core", core)
        setattr(adsk, "fusion", fusion)
        revolute_type = types.SimpleNamespace(cast=lambda motion: motion if motion.kind == "revolute" else None)
        setattr(fusion, "RevoluteJointMotion", revolute_type)
        with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
            candidates = runpy.run_path(str(SCRIPT))["_revolute_candidates"]

        root_joint = types.SimpleNamespace(
            name="Revolute 1", jointMotion=types.SimpleNamespace(kind="revolute"),
            assemblyContext=types.SimpleNamespace(fullPathName="Leg Assembly:1"),
        )
        ball_joint = types.SimpleNamespace(name="Ball 5", jointMotion=types.SimpleNamespace(kind="ball"))
        root = types.SimpleNamespace(allJoints=[root_joint, ball_joint])
        self.assertEqual(candidates(root), [(root_joint, root_joint.jointMotion)])


if __name__ == "__main__":
    unittest.main()
