import importlib.util
import io
import pathlib
import unittest

HERE = pathlib.Path(__file__).resolve().parents[1]

def load():
    spec = importlib.util.spec_from_file_location('candidate_core', HERE / 'core.py')
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

HEADER = 'schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w\n'

class CsvTests(unittest.TestCase):
    def test_multiple_first_neutral_heave_frames_include_last(self):
        self.assertTrue((HERE / 'core.py').exists(), 'CSV animation core missing')
        c = load()
        rows = c.read_csv(io.StringIO(HEADER + '1,0,0,0,1,0,0,0,0,1\n1,1,0.5,0,1.1,0,0,0,0,1\n1,2,0.9,0,1.2,0,0,0,0,1\n'))
        frames = c.frames(rows)
        self.assertEqual([f['sequence'] for f in frames], [0, 1, 2])
        self.assertAlmostEqual(frames[0]['heave_cm'], 0)
        self.assertAlmostEqual(frames[-1]['heave_cm'], .1)

class RotationAndSafetyTests(unittest.TestCase):
    def test_relative_quaternion_mapping_and_target(self):
        import math
        c = load()
        rows = c.read_csv(io.StringIO(HEADER + '1,0,0,0,1,0,0,0,0,1\n' +
            '1,1,.5,99,1.1,99,0,0,0.08715574274765817,0.9961946980917455\n'))
        f = c.frames(rows)[1]
        self.assertAlmostEqual(f['rotation_vector'][1], -math.radians(10)*.08)
        baseline = ((1,0,0,4),(0,1,0,5),(0,0,1,6))
        target = c.target(baseline, f)
        self.assertEqual((target[0][3], target[1][3]), (4,5))
        self.assertAlmostEqual(target[2][3], 6.05)
        self.assertAlmostEqual(target[0][2], math.sin(-math.radians(.8)))
        rows[0].update(rot_z=rows[1]['rot_z'], rot_w=rows[1]['rot_w'])
        self.assertAlmostEqual(c.frames(rows)[1]['rotation_vector'][1], 0)

    def test_malformed_and_unsafe_inputs_rejected(self):
        c = load()
        for text in ['1,0,0,0,nan,0,0,0,0,1\n', '1,0,0,0,1,0,0,0,0,0\n']:
            with self.assertRaises(ValueError):
                rows = c.read_csv(io.StringIO(HEADER + text + '1,1,.5,0,1,0,0,0,0,1\n'))
                c.frames(rows)
        rows = c.read_csv(io.StringIO(HEADER+'1,0,0,0,1,0,0,0,0,1\n1,1,.5,0,100,0,0,0,0,1\n'))
        with self.assertRaises(ValueError): c.frames(rows)
        for interval in (0, -1, float('nan')):
            with self.assertRaises(ValueError): c.frames(rows, interval=interval)

if __name__ == '__main__':
    unittest.main()
