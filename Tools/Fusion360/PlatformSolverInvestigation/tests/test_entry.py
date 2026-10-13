"""Integration seams use API-shaped stand-ins, not a simulated Fusion solver."""
import importlib.util
import json
import math
import pathlib
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

PATH = pathlib.Path(__file__).resolve().parents[1] / "PlatformSolverInvestigation.py"


def load():
    adsk = types.ModuleType("adsk")
    core, fusion = types.ModuleType("adsk.core"), types.ModuleType("adsk.fusion")
    setattr(adsk, "core", core)
    setattr(adsk, "fusion", fusion)
    setattr(fusion, "RevoluteJointMotion", types.SimpleNamespace(cast=lambda value: value))
    spec = importlib.util.spec_from_file_location("solver_investigation_main", PATH)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    with patch.dict(sys.modules, {"adsk": adsk, "adsk.core": core, "adsk.fusion": fusion}):
        spec.loader.exec_module(module)
    return module


class EntryTests(unittest.TestCase):
    def test_audit_reads_native_reference_without_selection_or_model_mutation(self):
        m = load()
        class Proxy:
            name = "Servo 1 Rigid"
            isSuppressed = False
            isValid = True
            assemblyContext = types.SimpleNamespace(fullPathName="Leg:1")
            parentComponent = types.SimpleNamespace(name="Mount assembly")
            timelineObject = types.SimpleNamespace(index=42, name="Servo mount feature")
            occurrenceOne = types.SimpleNamespace(fullPathName="Leg:1+Servo:1")
            nativeObject = types.SimpleNamespace(
                occurrenceOne=types.SimpleNamespace(fullPathName="Servo:1"),
                occurrenceTwo=types.SimpleNamespace(fullPathName="Mount:1"))
            @property
            def occurrenceTwo(self):
                raise RuntimeError("InternalValidationError : res")
        root = types.SimpleNamespace(allJoints=[], allAsBuiltJoints=[Proxy()],
                                    allOccurrences=types.SimpleNamespace(count=0))
        messages = []
        ui = types.SimpleNamespace(inputBox=lambda *_: ("AUDIT", False), messageBox=messages.append)
        app = types.SimpleNamespace(userInterface=ui, activeProduct=types.SimpleNamespace(rootComponent=root),
            version="stand-in", activeDocument=types.SimpleNamespace(name="TEST ONLY"))
        m.adsk.core.Application = types.SimpleNamespace(get=lambda: app)
        m.adsk.fusion.Design = types.SimpleNamespace(cast=lambda product: product)
        with tempfile.TemporaryDirectory(dir=PATH.parent) as folder:
            with patch.object(m, "HERE", pathlib.Path(folder)), patch.object(m, "experiment") as drive, \
                 patch.object(m, "inventory") as validate:
                m.run(None)
                drive.assert_not_called()
                validate.assert_not_called()
            reports = list(pathlib.Path(folder).glob("reports/*.jsonl"))
            self.assertEqual(len(reports), 1, "AUDIT must produce a report")
            records = [json.loads(line) for line in reports[0].read_text().splitlines()]
            self.assertEqual([r["event"] for r in records], ["start", "audit", "complete"])
            joint = records[1]["data"]["joints"][0]
            self.assertIn("occurrenceTwo", joint["errors"])
            self.assertEqual(joint["native"]["occurrenceTwo"], "Mount:1")
            self.assertEqual(joint["assemblyContext"], "Leg:1")
            self.assertEqual(joint["parentComponent"], "Mount assembly")
            self.assertEqual(joint["timelineObject"], {"index": 42, "name": "Servo mount feature"})
            self.assertIn("No model changes", messages[0])

    def test_unreadable_asbuilt_endpoint_is_reported_and_active_joint_blocks_motion(self):
        m = load()
        class BrokenJoint:
            name = "As-built broken"
            isSuppressed = False
            occurrenceOne = types.SimpleNamespace(fullPathName="Leg:1")
            @property
            def occurrenceTwo(self):
                raise RuntimeError("2 : InternalValidationError : res")
        good = types.SimpleNamespace(name="Good", isSuppressed=False,
                                     occurrenceOne=None, occurrenceTwo=None)
        root = types.SimpleNamespace(allJoints=[good], allAsBuiltJoints=[BrokenJoint()])
        inspect = getattr(m, "inspect_joints", None)
        self.assertTrue(callable(inspect), "Fault-tolerant joint inspection is missing")
        inspected = m.inspect_joints(root)
        self.assertEqual(len(inspected), 2)
        bad = inspected[1][1]
        self.assertEqual(bad["name"], "As-built broken")
        self.assertEqual(bad["one"], "Leg:1")
        self.assertIn("occurrenceTwo", bad["errors"])
        self.assertIn("InternalValidationError", bad["errors"]["occurrenceTwo"])
        top = types.SimpleNamespace(fullPathName="Top:1", isGrounded=False, isGroundToParent=False)
        with self.assertRaisesRegex(ValueError, "As-built broken.*occurrenceTwo"):
            m.inventory(root, top, inspected)
        base = types.SimpleNamespace(fullPathName="Base:1")
        occurrences = [top, base]
        root.allOccurrences = types.SimpleNamespace(count=2, item=lambda i: occurrences[i])
        selections, messages = iter(occurrences), []
        ui = types.SimpleNamespace(inputBox=lambda *_: ("RUN", False), messageBox=messages.append,
            selectEntity=lambda *_: types.SimpleNamespace(entity=next(selections)))
        app = types.SimpleNamespace(userInterface=ui, activeProduct=types.SimpleNamespace(rootComponent=root),
            version="stand-in", activeDocument=types.SimpleNamespace(name="TEST ONLY"))
        m.adsk.core.Application = types.SimpleNamespace(get=lambda: app)
        m.adsk.fusion.Design = types.SimpleNamespace(cast=lambda product: product)
        with tempfile.TemporaryDirectory(dir=PATH.parent) as folder:
            with patch.object(m, "HERE", pathlib.Path(folder)), patch.object(m, "experiment") as drive:
                m.run(None)
                drive.assert_not_called()
            reports = list(pathlib.Path(folder).glob("reports/*.jsonl"))
            self.assertEqual(len(reports), 1)
            records = [json.loads(line) for line in reports[0].read_text().splitlines()]
            self.assertEqual([r["event"] for r in records], ["start", "inventory", "error"])
            self.assertIn("occurrenceTwo", records[1]["data"]["joints"][1]["errors"])
            self.assertIn("As-built broken", messages[0])

    def test_cancelled_confirmation_does_not_access_design_or_run_experiment(self):
        m = load()
        run = getattr(m, "run", None)
        self.assertTrue(callable(run), "Fusion script entry point is missing")
        assert callable(run)
        ui = types.SimpleNamespace(inputBox=lambda *_: ("CANCEL", False))
        app = types.SimpleNamespace(userInterface=ui)
        m.adsk.core.Application = types.SimpleNamespace(get=lambda: app)
        with patch.object(m, "experiment", side_effect=AssertionError("Must not run")):
            run(None)

    def test_joint_checks_handle_wrap_and_reject_excess_nonfinite_or_enabled_limit(self):
        m = load()
        check = m.check_angles
        motion = types.SimpleNamespace(rotationValue=0, rotationLimits=None)
        servo = types.SimpleNamespace(name="servo", jointMotion=motion)
        self.assertAlmostEqual(check([servo], [2 * math.pi])[0], 0)
        for angle in (math.radians(11), float("nan")):
            motion.rotationValue = angle
            with self.assertRaises(ValueError):
                check([servo], [0])
        motion.rotationValue = math.radians(2)
        motion.rotationLimits = types.SimpleNamespace(isMinimumValueEnabled=False,
                                isMaximumValueEnabled=True, maximumValue=math.radians(1))
        with self.assertRaisesRegex(ValueError, "enabled"):
            check([servo], [0])

    def test_joint_motion_is_cast_before_reading_revolute_properties(self):
        m = load()
        base_motion = object()
        revolute = types.SimpleNamespace(rotationValue=0.01, rotationLimits=None)
        m.adsk.fusion.RevoluteJointMotion.cast = lambda value: revolute if value is base_motion else None
        servo = types.SimpleNamespace(name="servo", jointMotion=base_motion)
        self.assertAlmostEqual(m.check_angles([servo], [0])[0], math.degrees(0.01))

    def test_bounded_steps_log_both_states_and_restore_without_csv(self):
        m = load()
        experiment = getattr(m, "experiment", None)
        self.assertTrue(callable(experiment), "Bounded experiment runner is missing")
        assert callable(experiment)
        class Matrix:
            def __init__(self, z=0, angle=0):
                c, s = math.cos(angle), math.sin(angle)
                self.values = [[c, -s, 0, 0], [s, c, 0, 0], [0, 0, 1, z]]
            def copy(self):
                copy = Matrix()
                copy.values = [row[:] for row in self.values]
                return copy
            def getCell(self, row, col):
                return self.values[row][col]
            def setCell(self, row, col, value):
                self.values[row][col] = value
                return True
        top = types.SimpleNamespace(fullPathName="Top Platform:1", isGrounded=False,
                                    isGroundToParent=False, transform2=Matrix())
        base = types.SimpleNamespace(fullPathName="Base:1", transform2=Matrix())
        cranks = [types.SimpleNamespace(fullPathName="Crank:"+str(i), transform2=Matrix()) for i in range(6)]
        servos = [types.SimpleNamespace(name="servo"+str(i), assemblyContext=None,
                  occurrenceOne=base, occurrenceTwo=crank, isSuppressed=False, isLocked=False,
                  jointMotion=types.SimpleNamespace(rotationValue=0.0, rotationLimits=None))
                  for i, crank in enumerate(cranks)]
        attachments = [types.SimpleNamespace(name="attach"+str(i), occurrenceOne=top, occurrenceTwo=crank,
                       isSuppressed=False, jointMotion=None) for i, crank in enumerate(cranks)]
        occurrences = [top, base] + cranks
        root = types.SimpleNamespace(allOccurrences=types.SimpleNamespace(count=len(occurrences),
                                     item=lambda i: occurrences[i]), allJoints=servos,
                                     allAsBuiltJoints=attachments)
        def pump():
            if top.isGrounded and not any(j.isSuppressed for j in attachments):
                angle = top.transform2.getCell(2, 3)
                for crank, servo in zip(cranks, servos):
                    crank.transform2 = Matrix(angle=angle)
                    servo.jointMotion.rotationValue = angle
        events, held = [], []
        result = experiment(root, top, base, attachments, servos, pump,
                            lambda *_: None, lambda event, data: events.append((event, data)), held.append)
        self.assertTrue(result["restored"])
        self.assertAlmostEqual(top.transform2.getCell(2, 3), 0)
        self.assertFalse(top.isGrounded)
        self.assertFalse(any(j.isSuppressed for j in attachments))
        self.assertEqual(len(held), 1)
        self.assertEqual(held[0]["articulated_count"], 6)
        self.assertEqual(sum(event == "step" for event, _ in events), 6)
        self.assertEqual(sum(event == "grounded" for event, _ in events), 7)
        self.assertEqual(sum(event == "released" for event, _ in events), 7)
        events.clear()
        with self.assertRaisesRegex(ValueError, "articulation"):
            experiment(root, top, base, attachments, servos, lambda: None,
                       lambda *_: None, lambda event, data: events.append((event, data)), held.append)
        self.assertEqual(sum(event == "step" for event, _ in events), 1)
        self.assertAlmostEqual(top.transform2.getCell(2, 3), 0)
        events.clear()
        def moving_base():
            pump()
            base.transform2 = Matrix(z=top.transform2.getCell(2, 3))
        with self.assertRaisesRegex(ValueError, "base moved"):
            experiment(root, top, base, attachments, servos, moving_base,
                       lambda *_: None, lambda event, data: events.append((event, data)), held.append)
        self.assertEqual(sum(event == "step" for event, _ in events), 1)
        self.assertAlmostEqual(base.transform2.getCell(2, 3), 0)
        # Exercise the Fusion entry/UI/report wiring with the same stand-ins.
        for servo in servos:
            servo.jointMotion.objectType = "RevoluteJointMotion"
        for attachment in attachments:
            attachment.jointMotion = types.SimpleNamespace(objectType="BallJointMotion")
        selections = iter((top, base))
        messages = []
        ui = types.SimpleNamespace(inputBox=lambda *_: ("RUN", False),
            selectEntity=lambda *_: types.SimpleNamespace(entity=next(selections)),
            messageBox=messages.append)
        app = types.SimpleNamespace(userInterface=ui, activeProduct=types.SimpleNamespace(rootComponent=root),
            activeDocument=types.SimpleNamespace(name="UNIT TEST - not Fusion output"), version="stand-in",
            activeViewport=types.SimpleNamespace(refresh=lambda: None))
        m.adsk.core.Application = types.SimpleNamespace(get=lambda: app)
        m.adsk.fusion.Design = types.SimpleNamespace(cast=lambda product: product)
        m.adsk.doEvents = pump
        with tempfile.TemporaryDirectory(dir=PATH.parent) as folder:
            with patch.object(m, "HERE", pathlib.Path(folder)), patch.object(m.time, "sleep"), \
                 patch.object(m, "inventory", return_value=(attachments, servos)):
                m.run(None)
            reports = list(pathlib.Path(folder).glob("reports/*.jsonl"))
            self.assertEqual(len(reports), 1)
            records = [json.loads(line) for line in reports[0].read_text().splitlines()]
            self.assertEqual(records[-1]["event"], "complete", records)
            self.assertTrue(records[-1]["data"]["restored"])
            self.assertEqual(len(messages), 2)

    def test_inventory_includes_asbuilt_attachments_without_mutating_them(self):
        self.assertTrue(PATH.exists(), "Fusion entry script is missing")
        m = load()
        top = types.SimpleNamespace(fullPathName="Top Platform:1", isGrounded=False, isGroundToParent=False)
        base = types.SimpleNamespace(fullPathName="Base:1")
        crank = types.SimpleNamespace(fullPathName="Crank:1")
        joints = [types.SimpleNamespace(name="servo"+str(i), occurrenceOne=base, occurrenceTwo=crank,
                  jointMotion=object(), isSuppressed=False, isLocked=False, assemblyContext=None)
                  for i in range(6)]
        attachments = [types.SimpleNamespace(name="attach"+str(i), occurrenceOne=top, occurrenceTwo=crank,
                       jointMotion=None, isSuppressed=False, isLocked=False, assemblyContext=None)
                       for i in range(6)]
        root = types.SimpleNamespace(allJoints=joints, allAsBuiltJoints=attachments)
        selected, servos = m.inventory(root, top)
        self.assertEqual(selected, attachments)
        self.assertEqual(servos, joints)
        self.assertFalse(any(j.isSuppressed for j in attachments))
        root.allAsBuiltJoints = attachments[:5]
        with self.assertRaisesRegex(ValueError, "six active platform"):
            m.inventory(root, top)


if __name__ == "__main__":
    unittest.main()
