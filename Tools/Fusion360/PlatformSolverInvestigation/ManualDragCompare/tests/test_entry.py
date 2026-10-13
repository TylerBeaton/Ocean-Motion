"""Entry-point stand-ins; never a native Fusion solver proof."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace as NS
import unittest
from unittest.mock import patch
import test_recorder as fixtures
from test_recorder import Doc, Matrix
from test_compare import pose

ROOT = Path(__file__).resolve().parents[1]


class Event:
    def __init__(self):
        self.handlers = []
    def add(self, handler):
        self.handlers.append(handler)
        return True
    def remove(self, handler):
        self.handlers.remove(handler)
        return True


class UI:
    def __init__(self, top, base):
        self.action = 'START'
        self.selections = iter([top, base])
        self.messages = []
    def inputBox(self, *args):
        return self.action, False
    def selectEntity(self, *args):
        return NS(entity=next(self.selections))
    def messageBox(self, text, *args):
        self.messages.append(text)


class EntryTests(unittest.TestCase):
    def test_start_returns_then_finish_exports_without_model_writes(self):
        self.assertTrue((ROOT / 'ManualDragCompare.py').exists(), 'entry point missing')
        spec = importlib.util.spec_from_file_location('manual_drag_entry', ROOT / 'ManualDragCompare.py')
        assert spec and spec.loader
        entry = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(entry)
        root, top, base, joints = fixtures.CaptureTests().setup_model()
        ui = UI(top, base)
        design = NS(rootComponent=root, activeComponent=root)
        app = NS(activeDocument=Doc(1), activeProduct=design, userInterface=ui,
                 documentClosing=Event())
        core = NS(Application=NS(get=lambda: app), DocumentEventHandler=type('Handler', (), {}))
        fusion = NS(Design=NS(cast=lambda obj: obj), Occurrence=NS(cast=lambda obj: obj),
                    JointTypes=NS(RevoluteJointType=1), RevoluteJointMotion=NS(cast=lambda motion: motion))
        adsk = NS(core=core, fusion=fusion)
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            entry.REPORT_DIR = Path(folder)
            with patch.dict(sys.modules, {'adsk': adsk, 'adsk.core': core, 'adsk.fusion': fusion}):
                sys.modules.pop(entry.STATE_KEY, None)
                try:
                    entry.run(None)
                    self.assertIn('START recorded', ui.messages[-1])
                    self.assertEqual(top.transform2.values, pose())
                    self.assertEqual(len(app.documentClosing.handlers), 1)
                    # Reject another live document even with identical display name.
                    ui.action = 'FINISH'
                    app.activeDocument = Doc(2)
                    entry.run(None)
                    self.assertIn('Wrong live document', ui.messages[-1])
                    app.activeDocument = Doc(1)
                    # Simulate Fusion reloading the entry script between runs.
                    entry = importlib.util.module_from_spec(spec)
                    spec.loader.exec_module(entry)
                    entry.REPORT_DIR = Path(folder)
                    # Human drag stand-in occurs AFTER run has returned.
                    top.transform2 = Matrix(pose(10, 1))
                    for joint in joints:
                        joint.occurrenceTwo.transform2 = Matrix(pose(5))
                    ui.action = 'FINISH'
                    entry.run(None)
                    reports = [json.loads(p.read_text()) for p in Path(folder).glob('*.json')]
                    finish = next(r for r in reports if r['phase'] == 'FINISH' and r['status'] == 'MEASURED')
                    self.assertEqual(finish['status'], 'MEASURED')
                    self.assertEqual(finish['comparison']['articulated_count'], 6)
                    self.assertEqual(len(app.documentClosing.handlers), 0)
                    self.assertEqual(top.transform2.values, pose(10, 1))
                finally:
                    sys.modules.pop(entry.STATE_KEY, None)


if __name__ == '__main__':
    unittest.main()
