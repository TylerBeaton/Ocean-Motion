"""Regression tests for the actual playback-to-joint command path.

The small stand-in captures event boundaries, not Fusion's constraint solver.
Real assembly articulation remains an in-Fusion acceptance test.
"""
import importlib.util
import math
import pathlib
import runpy
import sys
import types
import unittest
from unittest.mock import patch

TOOLS = pathlib.Path(__file__).resolve().parents[2]
SCRIPT = TOOLS / "UnityJointVisualPlayback" / "UnityJointVisualPlayback.py"


def load_playback(events):
    adsk = types.ModuleType("adsk")
    core = types.ModuleType("adsk.core")
    fusion = types.ModuleType("adsk.fusion")
    setattr(adsk, "core", core)
    setattr(adsk, "fusion", fusion)
    setattr(adsk, "doEvents", events)
    spec = importlib.util.spec_from_file_location(
        "JointPoseProbe", TOOLS / "JointPoseProbe" / "JointPoseProbe.py")
    assert spec is not None and spec.loader is not None
    probe = importlib.util.module_from_spec(spec)
    with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core,
                                 "adsk.fusion": fusion, "JointPoseProbe": probe}):
        spec.loader.exec_module(probe)
        return runpy.run_path(str(SCRIPT))


class FrameUpdateTests(unittest.TestCase):
    def test_pose_test_fraction_scales_motion_without_overshoot(self):
        module = load_playback(lambda: None)
        target = module["_pose_test_target"]
        baseline = ((1, 0, 0, 7), (0, 1, 0, 2), (0, 0, 1, 30))
        for fraction in (0, 0.1, 0.5, 1):
            translation, rotation = module["_pose_delta"](baseline, target(baseline, fraction))
            self.assertAlmostEqual(translation[2], fraction)
            self.assertAlmostEqual(rotation[0], fraction * 0.15)
            self.assertAlmostEqual(rotation[1], fraction * 0.15)
            self.assertAlmostEqual(rotation[2], 0)
        for invalid in (-0.1, 1.1, float("nan")):
            with self.assertRaises(ValueError):
                target(baseline, invalid)

    def test_relative_component_pose_distinguishes_rigid_shift_from_joint_rotation(self):
        module = load_playback(lambda: None)
        relative = module.get("_relative_component_pose")
        self.assertTrue(callable(relative), "Missing independent servo-body measurement")
        assert callable(relative)
        identity = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0))
        offset = ((1, 0, 0, 2), (0, 1, 0, 0), (0, 0, 1, 0))
        # Same rigid translation AND rotation of both bodies must cancel.
        moved_parent = ((0, -1, 0, 5), (1, 0, 0, 3), (0, 0, 1, 4))
        moved_child = ((0, -1, 0, 5), (1, 0, 0, 5), (0, 0, 1, 4))
        self.assertEqual(relative(identity, offset), relative(moved_parent, moved_child))
        tilted = module["_pose_test_target"](identity)
        mm, degrees = module["_drift"](relative(identity, identity), relative(identity, tilted))
        self.assertAlmostEqual(mm, 1)
        self.assertGreater(degrees, 0.2)

    def test_joint_bounds_are_rechecked_after_frame_event_processing(self):
        joints = [(types.SimpleNamespace(assemblyContext=None, name=str(i)),
                   types.SimpleNamespace(rotationValue=0.0, rotationLimits=None))
                  for i in range(6)]
        def settle():
            joints[-1][1].rotationValue = math.radians(40)
        module = load_playback(settle)
        drive = module["_drive_frame"]
        axes = lambda *_: ((0, 0, 0), (0, 0, 0), (0, 0, 0))
        with patch.dict(drive.__globals__, {"_current_axes": axes}):
            with self.assertRaisesRegex(ValueError, "40.00"):
                drive(None, joints, [0.0] * 6, None,
                      [(1, 0, 0), (0, 1, 0), (0, 0, 1)] * 2, (0.1, 0.1, 0.1))

    def test_pose_test_mode_does_not_open_csv_or_start_legacy_calibration(self):
        module = load_playback(lambda: None)
        run = module["run"]
        errors, probes = [], []
        root = types.SimpleNamespace(allJoints=[types.SimpleNamespace(jointMotion=object())
                                               for _ in range(6)])
        app = types.SimpleNamespace(activeProduct=object(),
                                    userInterface=types.SimpleNamespace(messageBox=errors.append))
        adsk = run.__globals__["adsk"]
        setattr(adsk.core, "Application", types.SimpleNamespace(get=lambda: app))
        setattr(adsk.fusion, "Design", types.SimpleNamespace(cast=lambda _: types.SimpleNamespace(rootComponent=root)))
        setattr(adsk.fusion, "RevoluteJointMotion", types.SimpleNamespace(cast=lambda v: v))
        def forbidden(*_):
            self.fail("Pose test entered the legacy CSV/calibration path")
        with patch.dict(run.__globals__, {"_choose_mode": lambda _: "pose-test",
                                         "_choose_csv": forbidden, "_calibrate": forbidden,
                                         "_run_pose_test": lambda a, r, j: probes.append((a, r, len(j)))}):
            run(None)
        self.assertEqual(errors, [])
        self.assertEqual(probes, [(app, root, 6)])

    def test_pose_probe_rejects_lateral_error_and_always_restores(self):
        module = load_playback(lambda: None)
        probe = module.get("_run_pose_test")
        self.assertTrue(callable(probe), "Missing bounded joint-aware pose probe")
        assert callable(probe)
        baseline = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 30))
        target = module["_pose_test_target"](baseline)
        wrong = tuple(row[:3] + (row[3] + (0.1 if i == 0 else 0),)
                      for i, row in enumerate(target))
        top = types.SimpleNamespace(fullPathName="Top Platform:1", transform2=types.SimpleNamespace(
            getCell=lambda r, c: baseline[r][c]))
        root = types.SimpleNamespace(allOccurrences=types.SimpleNamespace(
            count=1, item=lambda _: top))
        joints = [(types.SimpleNamespace(name=str(i), assemblyContext=None,
                                        occurrenceOne=top, occurrenceTwo=top),
                   types.SimpleNamespace(rotationValue=0.0, rotationLimits=None))
                  for i in range(6)]
        messages = []
        app = types.SimpleNamespace(userInterface=types.SimpleNamespace(messageBox=messages.append),
                                    activeViewport=types.SimpleNamespace(refresh=lambda: True))
        requests = []
        sleeps = []
        def request(_root, _top, pose):
            requests.append(pose)
            return baseline if pose == baseline else wrong
        with patch.dict(probe.__globals__, {"_top_pose": lambda _: baseline,
                                           "_request_constrained_pose": request,
                                           "time": types.SimpleNamespace(sleep=sleeps.append)}):
            probe(app, root, joints)
        expected = [module["_pose_test_target"](baseline, i / 10) for i in range(1, 11)]
        self.assertEqual(requests, expected + [baseline])
        self.assertEqual(sleeps, [0.3] * 10)
        self.assertIn("NOT ACCEPTED", messages[0])
        self.assertIn("1.000", messages[0])
        self.assertIn("NO SERVO-BODY ARTICULATION DETECTED", messages[0])
        requests.clear()
        def failing_request(_root, _top, pose):
            requests.append(pose)
            if len(requests) == 1:
                raise ValueError("Injected Fusion failure after mutation")
            return baseline
        with patch.dict(probe.__globals__, {"_top_pose": lambda _: baseline,
                                           "_request_constrained_pose": failing_request}):
            with self.assertRaisesRegex(ValueError, "Injected Fusion failure"):
                probe(app, root, joints)
        self.assertEqual(requests, [expected[0], baseline])
        requests.clear()
        wrong = tuple(row[:3] + (row[3] + (3.0 if i == 0 else 0),)
                      for i, row in enumerate(target))
        with patch.dict(probe.__globals__, {"_top_pose": lambda _: baseline,
                                           "_request_constrained_pose": request}):
            with self.assertRaisesRegex(ValueError, "plate bounds"):
                probe(app, root, joints)
        self.assertEqual(requests, [expected[0], baseline])

    def test_constrained_request_honors_joints_and_reads_back_after_events(self):
        operations = []
        module = load_playback(lambda: operations.append("events"))
        request = module.get("_request_constrained_pose")
        self.assertTrue(callable(request), "Missing joint-aware pose request")
        assert callable(request)
        target = ((1, 0, 0, 12), (0, 1, 0, -7), (0, 0, 1, 30.1))

        class Matrix:
            def __init__(self):
                self.cells = {}
            def copy(self):
                return Matrix()
            def setCell(self, row, col, value):
                self.cells[row, col] = value
                return True

        top = types.SimpleNamespace(transform2=Matrix())
        def transform(occurrences, transforms, ignore_joints):
            operations.append("transform")
            self.assertEqual(occurrences, [top])
            self.assertIs(ignore_joints, False)
            self.assertEqual(transforms[0].cells,
                             {(r, c): target[r][c] for r in range(3) for c in range(4)})
            return True
        root = types.SimpleNamespace(transformOccurrences=transform)
        def read_pose(_root):
            operations.append("readback")
            return target
        with patch.dict(request.__globals__, {"_top_pose": read_pose}):
            self.assertEqual(request(root, top, target), target)
        self.assertEqual(operations, ["transform", "events", "readback"])
        root.transformOccurrences = lambda *_: False
        with self.assertRaisesRegex(ValueError, "rejected"):
            request(root, top, target)

    def test_pose_test_target_keeps_world_xy_and_rotates_about_plate_origin(self):
        module = load_playback(lambda: None)
        # Deliberately rotated and translated neutral: do not rotate its origin
        # around the world's origin when requesting a platform-local tilt.
        baseline = ((0, -1, 0, 12), (1, 0, 0, -7), (0, 0, 1, 30))
        target = module.get("_pose_test_target")
        self.assertTrue(callable(target), "Missing bounded full-pose test target")
        assert callable(target)
        pose = target(baseline)
        translation, rotation = module["_pose_delta"](baseline, pose)
        for actual, expected in zip(translation, (0, 0, 1)):
            self.assertAlmostEqual(actual, expected)
        for actual, expected in zip(rotation, (0.15, 0.15, 0)):
            self.assertAlmostEqual(actual, expected)

    def test_frame_never_pumps_events_between_individual_joint_commands(self):
        writes = []
        presented = []
        module = load_playback(lambda: presented.append(tuple(writes)))

        class Motion:
            rotationLimits = None
            def __init__(self, index):
                self.index = index
                self.angle = 0.0
            @property
            def rotationValue(self):
                return self.angle
            @rotationValue.setter
            def rotationValue(self, value):
                self.angle = value
                writes.append(self.index)

        joints = [(types.SimpleNamespace(assemblyContext=None, name=str(i)), Motion(i))
                  for i in range(6)]
        drive = module["_drive_frame"]
        columns = [(1, 0, 0), (0, 1, 0), (0, 0, 1)] * 2
        axes = lambda *_: ((0, 0, 0), (0, 0, 0), (0, 0, 0))
        with patch.dict(drive.__globals__, {"_current_axes": axes}):
            drive(None, joints, [0.0] * 6, None, columns, (0.1, 0.1, 0.1))
        self.assertEqual(writes, list(range(6)))
        self.assertTrue(all(len(snapshot) == 6 for snapshot in presented),
                        "UI events exposed a partially updated six-joint frame: {}".format(presented))
        self.assertTrue(all(math.isfinite(motion.rotationValue) for _, motion in joints))


if __name__ == "__main__":
    unittest.main()
