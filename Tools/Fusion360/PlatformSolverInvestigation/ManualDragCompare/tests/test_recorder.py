"""Stand-ins test recorder contracts, NOT Autodesk solver behavior."""
import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace as NS
import unittest
from test_compare import pose, snapshot

ROOT = Path(__file__).resolve().parents[1]


def load():
    spec = importlib.util.spec_from_file_location('drag_recorder', ROOT / 'recorder.py')
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    setattr(module, 'revolute_motion', lambda motion: motion)  # Autodesk cast stand-in
    return module


class Doc:
    def __init__(self, native_id):
        self.native_id = native_id
        self.name = 'Same name'
        self.creationId = 'same-creation-id-even-for-copy'
        self.isValid = True
    def __eq__(self, other):
        return isinstance(other, Doc) and self.native_id == other.native_id


class RecorderTests(unittest.TestCase):
    def test_angle_casts_base_joint_motion_before_reading_value(self):
        rec = load()
        base_motion = object()
        setattr(rec, 'revolute_motion', lambda motion: NS(rotationValue=0.25) if motion is base_motion else None)
        self.assertEqual(rec.angle(NS(jointMotion=base_motion)), 0.25)

    def test_live_document_not_name_or_creation_id(self):
        self.assertTrue((ROOT / 'recorder.py').exists(), 'recorder implementation missing')
        rec = load()
        original = Doc(1)
        state = NS(document=original, invalidated=False, baseline_id='a', before=snapshot())
        rec.check_session(state, Doc(1), 'a')  # separate wrapper of same native document
        for doc, baseline in ((Doc(2), 'a'), (Doc(1), 'wrong')):
            with self.assertRaises(ValueError):
                rec.check_session(state, doc, baseline)
        state.invalidated = True
        with self.assertRaises(ValueError):
            rec.check_session(state, Doc(1), 'a')
        state.invalidated = False
        original.isValid = False
        with self.assertRaises(ValueError):
            rec.check_session(state, Doc(1), 'a')
        with self.assertRaises(ValueError):
            rec.check_session(None, Doc(1), 'a')

    def test_report_roundtrip_and_nonfinite_rejection(self):
        rec = load()
        self.assertTrue(hasattr(rec, 'export_report'), 'report exporter missing')
        with tempfile.TemporaryDirectory(dir=ROOT) as folder:
            payload = {'kind': 'stand-in-test-not-native', 'before': snapshot(),
                       'after': snapshot(), 'result': rec.measure.compare(snapshot(), snapshot())}
            path = rec.export_report(Path(folder), payload, 'test')
            self.assertEqual(json.loads(path.read_text()), payload)
            with self.assertRaises(ValueError):
                rec.export_report(Path(folder), {'bad': float('nan')}, 'bad')


class Matrix:
    def __init__(self, values):
        self.values = values
    def getCell(self, r, c):
        return self.values[r][c]


class CaptureTests(unittest.TestCase):
    def setup_model(self):
        occurrences = [NS(fullPathName='top', isValid=True, transform2=Matrix(pose())),
                       NS(fullPathName='base', isValid=True, transform2=Matrix(pose()))]
        joints = []
        for i in range(6):
            one = NS(fullPathName='leg%d+a' % i, isValid=True, transform2=Matrix(pose()))
            two = NS(fullPathName='leg%d+b' % i, isValid=True, transform2=Matrix(pose()))
            occurrences.extend([one, two])
            joints.append(NS(name='servo%d' % i, isValid=True, occurrenceOne=one, occurrenceTwo=two,
                             jointMotion=NS(jointType=1, rotationValue=0.0)))
        root = NS(allJoints=joints, allOccurrences=occurrences)
        return root, occurrences[0], occurrences[1], joints

    def test_capture_root_context_and_missing_body_errors(self):
        rec = load()
        self.assertTrue(hasattr(rec, 'capture'), 'capture missing')
        root, top, base, joints = self.setup_model()
        errors = []
        selected = rec.discover(root, 1, errors)
        result = rec.capture(root, top, base, selected)
        self.assertEqual(len(result['servos']), 6)
        self.assertEqual(result['read_errors'], [])
        self.assertEqual(result['top'], pose())
        root.allOccurrences.remove(joints[0].occurrenceOne)
        result = rec.capture(root, top, base, selected)
        self.assertIsNone(result['servos'][0]['one'])
        self.assertTrue(result['read_errors'])
        self.assertEqual(len(result['servos']), 6)

    def test_same_path_replacement_is_not_original_occurrence(self):
        rec = load()
        root, top, base, joints = self.setup_model()
        replacement = NS(fullPathName=top.fullPathName, isValid=True, transform2=Matrix(pose(12)))
        root.allOccurrences[0] = replacement
        result = rec.capture(root, top, base, joints)
        self.assertIsNone(result['top'])
        self.assertTrue(result['read_errors'])

    def test_angle_error_explicit_and_unrelated_asbuilt_never_read(self):
        rec = load()
        self.assertTrue(hasattr(rec, 'capture'), 'capture missing')
        root, top, base, joints = self.setup_model()
        joints[1].jointMotion.rotationValue = float('nan')
        result = rec.capture(root, top, base, joints)
        self.assertIsNone(result['servos'][1]['angle_rad'])
        self.assertIn('angle', str(result['read_errors']))
        # Root has no allAsBuiltJoints member; capture must still work.


if __name__ == '__main__':
    unittest.main()
