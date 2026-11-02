from dataclasses import replace
import json
import math
from pathlib import Path
import random
import subprocess
import sys
import unittest

OFFLINE = Path(__file__).resolve().parents[1]
ROOT = OFFLINE.parent
sys.path.insert(0, str(OFFLINE))

from build_geometry import build
from geometry_io import load_geometry, report_solution
from stewart_ik import (GeometryError, IDENTITY, Leg, Model, Pose, cross, dot,
                        leg_candidates, matvec, quaternion_matrix)


def axis_quaternion(axis, degrees):
    magnitude = math.hypot(*axis)
    half = math.radians(degrees) / 2
    return tuple(v/magnitude*math.sin(half) for v in axis) + (math.cos(half),)


class GeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model, cls.data = load_geometry(ROOT / "Measurements/offline-geometry.json")

    def assert_closure(self, model, result):
        self.assertTrue(result.found, result.errors)
        for leg, row in zip(model.legs, result.legs):
            horn, top = row["horn_joint_global_mm"], row["top_joint_global_mm"]
            self.assertAlmostEqual(math.dist(horn, top), leg.rod_length_mm, places=9)
            # Independent sphere/plane invariants, not a comparison to stored angles.
            from_shaft = tuple(h-p for h,p in zip(horn, leg.shaft_point_mm))
            along = sum(v*a for v,a in zip(from_shaft, leg.shaft_axis))
            perpendicular = tuple(v-along*a for v,a in zip(from_shaft, leg.shaft_axis))
            self.assertAlmostEqual(along, leg.axial_offset_mm, places=9)
            self.assertAlmostEqual(math.hypot(*perpendicular), leg.horn_radius_mm, places=9)
            self.assertLess(row["rod_length_residual_mm"], 1e-8)

    def test_neutral_closes_actual_lengths_with_explicit_placeholders(self):
        result = self.model.solve(Pose())
        self.assert_closure(self.model, result)
        self.assertEqual(len(result.legs), 6)
        for row in result.legs:
            self.assertAlmostEqual(row["neutral_relative_angle_deg"], 0., places=12)
        self.assertTrue(all(leg.axial_offset_mm > 6 for leg in self.model.legs))
        self.assertEqual(self.model.neutral_position_mm, (0., 0., 181.4))

    def test_editable_offset_changes_geometry_without_rewriting_sources(self):
        alternate = Model([replace(leg, axial_offset_mm=0.) for leg in self.model.legs],
                          self.model.neutral_position_mm)
        self.assert_closure(alternate, alternate.solve(Pose()))
        self.assertGreater(max(abs(a-b) for a,b in zip(alternate.neutral_angles, self.model.neutral_angles)), .001)

    def test_non_axis_aligned_leg_both_roots_and_negative_axial_offset(self):
        leg = Leg(0, (12., -8., 4.), (1., 2., 3.), -7., (1., -.5, 0.), (0., 0., 0.), 4., 3.)
        known_angle = math.radians(30)
        known_horn = leg.horn_point(known_angle)
        target = tuple(h+d for h,d in zip(known_horn, (1., 2., 2.)))
        roots, conditioning = leg_candidates(leg, target)
        self.assertGreater(conditioning, 1e-6)
        self.assertEqual(set(roots), {-1, 1})
        self.assertTrue(any(abs(math.remainder(a-known_angle, math.tau)) < 1e-10 for a in roots.values()))
        for angle in roots.values():
            self.assertAlmostEqual(math.dist(leg.horn_point(angle), target), 3., places=10)

    def test_small_six_degree_of_freedom_poses(self):
        for axis in ((1,0,0), (0,1,0), (0,0,1)):
            for direction in (-1, 1):
                position = tuple(direction*.001*x for x in axis)
                self.assert_closure(self.model, self.model.solve(Pose.unity(position, (0,0,0,1))))
                rotation = axis_quaternion(axis, direction)
                self.assert_closure(self.model, self.model.solve(Pose.unity((0,0,0), rotation)))

    def test_random_local_pose_closure(self):
        rng = random.Random(902)
        for _ in range(250):
            position = tuple(rng.uniform(-.003, .003) for _ in range(3))
            q = axis_quaternion(tuple(rng.uniform(-1, 1) for _ in range(3)), rng.uniform(-2, 2))
            self.assert_closure(self.model, self.model.solve(Pose.unity(position, q)))

    def test_continuity_unwraps_across_two_full_turns_without_branch_switch(self):
        # Synthetic single-leg geometry repeated for six channels; tests the branch
        # algorithm, not full-platform rank/workspace validity.
        legs = [Leg(i, (0,0,0), (0,0,1), 0, (1,0,0),
                    (.5, math.sqrt(3)/2, 0), 1, 1) for i in range(6)]
        model = Model(legs, (0,0,0))
        previous = model.solve(Pose()).angles_deg
        branches = tuple(model.branches)
        for degrees in range(0, 721, 2):
            pose = Pose.fusion(rotation_xyzw=axis_quaternion((0,0,1), degrees))
            result = model.solve(pose, previous, max_step_degrees=2.001)
            self.assert_closure(model, result)
            self.assertEqual(tuple(row["branch"] for row in result.legs), branches)
            for angle in result.angles_deg:
                self.assertAlmostEqual(angle, degrees, places=8)
            previous = result.angles_deg

    def test_unreachable_pose_returns_no_partial_leg_set(self):
        result = self.model.solve(Pose.fusion((0,0,1000)))
        self.assertFalse(result.found)
        self.assertTrue(result.errors)
        self.assertEqual(result.legs, ())
        self.assertEqual(result.as_dict()["legs"], [])

    def test_limits_reject_locked_branch_without_clipping_or_partial_result(self):
        legs = [Leg(i, (0,0,0), (0,0,1), 0, (1,0,0),
                    (.5, math.sqrt(3)/2, 0), 1, 1,
                    (-10,10) if i == 0 else None) for i in range(6)]
        model = Model(legs, (0,0,0))
        result = model.solve(Pose.fusion(rotation_xyzw=axis_quaternion((0,0,1), 20)))
        self.assertFalse(result.found)
        self.assertEqual(result.legs, ())
        self.assertIn("Channel 0", result.errors[0])
        self.assertIn("angle limits", result.errors[0])

    def test_tangent_and_indeterminate_targets_are_not_fake_solutions(self):
        leg = Leg(0, (0,0,0), (0,0,1), 0, (1,0,0), (0,0,0), 1, 1)
        for target in ((2,0,0), (0,0,0), (3,0,0)):
            with self.assertRaises(GeometryError):
                leg_candidates(leg, target)

    def test_step_guard(self):
        previous = self.model.solve(Pose()).angles_deg
        result = self.model.solve(Pose.fusion((0,0,3)), previous, max_step_degrees=.01)
        self.assertFalse(result.found)
        self.assertEqual(result.legs, ())

    def test_bad_inputs_fail(self):
        for position in ((math.nan,0,0), (0,math.inf,0), (0,0), (True,0,0)):
            with self.assertRaises(GeometryError):
                Pose.fusion(position)
        for q in ((0,0,0,0), (0,0,math.nan,1), (0,0,1)):
            with self.assertRaises(GeometryError):
                quaternion_matrix(q)
        with self.assertRaises(GeometryError):
            Pose(rotation=((1,0,0),(0,1,0),(0,0,-1)))
        with self.assertRaises(GeometryError):
            replace(self.model.legs[0], shaft_axis=(0,0,0))
        with self.assertRaises(GeometryError):
            replace(self.model.legs[0], reference_radial_direction=self.model.legs[0].shaft_axis)
        with self.assertRaises(GeometryError):
            replace(self.model.legs[0], rod_length_mm=-1)
        with self.assertRaises(GeometryError):
            Model([self.model.legs[0]]*6, (0,0,0))
        with self.assertRaises(GeometryError):
            self.model.solve(Pose(), [0]*5)

    def test_unity_basis_translation_and_each_rotation_axis(self):
        p = Pose.unity((.001,.002,.003), (0,0,0,1))
        self.assertEqual(p.translation_mm, (1.,3.,2.))
        # Unity +90X takes Unity Y to Z => Fusion Z to Y; similarly Y/Z.
        cases = [((1,0,0), (0,0,1), (0,1,0)),
                 ((0,1,0), (0,1,0), (1,0,0)),
                 ((0,0,1), (1,0,0), (0,0,1))]
        for axis, before, expected in cases:
            pose = Pose.unity((0,0,0), axis_quaternion(axis, 90))
            actual = matvec(pose.rotation, before)
            for a,b in zip(actual, expected):
                self.assertAlmostEqual(a,b,places=12)

    def test_quaternion_sign_equivalence_and_nontrivial_neutral_rotation(self):
        q = axis_quaternion((1,2,3), 35)
        self.assertEqual(Pose.unity((0,0,0), q).rotation,
                         Pose.unity((0,0,0), tuple(-x for x in q)).rotation)
        # Construct a rotated coordinate copy of the model, and ensure the same
        # neutral-relative pose produces identical signed motor angles.
        rotation = quaternion_matrix(q)
        offset = (11.,-7.,9.)
        rotated_legs = [replace(leg,
            shaft_point_mm=tuple(x+y for x,y in zip(matvec(rotation, leg.shaft_point_mm),offset)),
            shaft_axis=matvec(rotation,leg.shaft_axis),
            reference_radial_direction=matvec(rotation,leg.reference_radial_direction)) for leg in self.model.legs]
        centre = tuple(x+y for x,y in zip(matvec(rotation,self.model.neutral_position_mm),offset))
        rotated = Model(rotated_legs, centre, q)
        pose = Pose.fusion((1.,2.,-1.), axis_quaternion((2,1,3), .7))
        a, b = self.model.solve(pose), rotated.solve(pose)
        self.assert_closure(rotated,b)
        for x,y in zip(a.angles_deg,b.angles_deg): self.assertAlmostEqual(x,y,places=9)

    def test_config_and_output_never_enable_hardware(self):
        generated = build()
        self.assertEqual(generated, self.data)
        self.assertTrue(all(row["angle_limits_deg"] is None for row in self.data["legs"]))
        changed_metadata = dict(self.data, hardware_output_enabled=True)
        result = report_solution(self.model, changed_metadata, Pose())
        self.assertFalse(result["hardware_output_enabled"])
        self.assertFalse(result["mechanical_workspace_validated"])
        self.assertFalse(result["physical_branch_confirmed"])
        self.assertNotIn("pulse_us", json.dumps(result))

    def test_cli_unreachable_and_invalid_inputs_emit_diagnostics(self):
        for args in (("--translation-mm", "0", "0", "1000"),
                     ("--translation-mm", "nan", "0", "0")):
            result = subprocess.run([sys.executable, str(OFFLINE/"run_ik.py"), *args],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 2, result.stderr)
            report = json.loads(result.stdout)
            self.assertFalse(report["kinematic_solution_found"])
            self.assertFalse(report["hardware_output_enabled"])
            self.assertEqual(report["legs"], [])


if __name__ == "__main__":
    unittest.main()
