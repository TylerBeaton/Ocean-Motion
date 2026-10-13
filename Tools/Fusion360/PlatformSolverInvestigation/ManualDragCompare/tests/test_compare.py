import importlib.util
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load():
    spec = importlib.util.spec_from_file_location('manual_compare_core', ROOT / 'compare.py')
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def pose(degrees=0, x=0):
    a = math.radians(degrees)
    return [[math.cos(a), -math.sin(a), 0, x],
            [math.sin(a), math.cos(a), 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]


def snapshot():
    return {'top': pose(), 'base': pose(), 'servos': [
        {'name': 'servo' + str(i), 'one_path': 'a' + str(i), 'two_path': 'b' + str(i),
         'one': pose(), 'two': pose(), 'angle_rad': 0.0} for i in range(6)], 'read_errors': []}


class ComparisonTests(unittest.TestCase):
    def test_plate_only_is_not_servo_articulation(self):
        self.assertTrue((ROOT / 'compare.py').exists(), 'comparison implementation missing')
        core = load()
        before, after = snapshot(), snapshot()
        after['top'] = pose(10, 1)
        result = core.compare(before, after)
        self.assertEqual(result['articulated_count'], 0)
        self.assertAlmostEqual(result['top_world']['translation_mm'], 10)
        self.assertEqual(len(result['servos']), 6)

    def test_real_relative_motion_and_angles(self):
        before, after = snapshot(), snapshot()
        for servo in after['servos']:
            servo['one'] = pose(20)
            servo['two'] = pose(35)
            servo['angle_rad'] = math.radians(15)
        result = load().compare(before, after)
        self.assertEqual(result['articulated_count'], 6)
        self.assertAlmostEqual(result['servos'][0]['rotation_deg'], 15)
        self.assertAlmostEqual(result['servos'][0]['angle_delta_rad'], math.radians(15))

    def test_rejects_incomplete_nonfinite_and_mismatched_pairs(self):
        for mutation in ('missing_top', 'missing_body', 'missing_servo', 'wrong_pair', 'nan_pose', 'inf_angle'):
            with self.subTest(mutation=mutation):
                before, after = snapshot(), snapshot()
                if mutation == 'missing_top':
                    del after['top']
                elif mutation == 'missing_body':
                    after['servos'][0]['one'] = None
                elif mutation == 'missing_servo':
                    after['servos'].pop()
                elif mutation == 'wrong_pair':
                    after['servos'][0]['one_path'] = 'replacement'
                elif mutation == 'nan_pose':
                    after['top'][0][0] = float('nan')
                else:
                    after['servos'][0]['angle_rad'] = float('inf')
                with self.assertRaises(ValueError):
                    load().compare(before, after)


if __name__ == '__main__':
    unittest.main()
