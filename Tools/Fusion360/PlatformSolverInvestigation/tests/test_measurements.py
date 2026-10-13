import importlib.util
import math
import pathlib
import unittest

PATH = pathlib.Path(__file__).resolve().parents[1] / "measurements.py"
IDENTITY = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0))


def load():
    spec = importlib.util.spec_from_file_location("investigation_measurements", PATH)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MeasurementTests(unittest.TestCase):
    def test_nonfinite_pose_cannot_be_reported_as_zero_rotation_error(self):
        m = load()
        for bad in (((float("nan"), 0, 0, 0), *IDENTITY[1:]),
                    (*IDENTITY[:2], (0, 0, 1, float("inf")))):
            with self.assertRaises(ValueError):
                m.error(IDENTITY, bad)

    def test_plate_only_and_whole_assembly_moves_are_not_articulation(self):
        self.assertTrue(PATH.exists(), "Independent component measurements are missing")
        m = load()
        before = {name: IDENTITY for name in ("top", "base", "crank")}
        raised = ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0.1))
        servos = [("servo", "base", "crank")]
        plate_only = m.summarize(before, dict(before, top=raised), "top", "base", servos, raised)
        self.assertAlmostEqual(plate_only["top_error_mm"], 0)
        self.assertEqual(plate_only["articulated_count"], 0)
        rigid = m.summarize(before, {name: raised for name in before}, "top", "base", servos, raised)
        self.assertEqual(rigid["articulated_count"], 0)
        self.assertAlmostEqual(rigid["base_mm"], 1)
        rotation = ((0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 1, 0))
        articulation = m.summarize(before, dict(before, top=raised, crank=rotation),
                                   "top", "base", servos, raised)
        self.assertEqual(articulation["articulated_count"], 1)
        self.assertAlmostEqual(articulation["servos"][0]["relative_deg"], 90)
        self.assertAlmostEqual(articulation["base_mm"], 0)


if __name__ == "__main__":
    unittest.main()
