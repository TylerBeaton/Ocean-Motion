"""Offline rotary Stewart geometry. No PWM mapping, transport or hardware output."""
from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Optional, Sequence, Tuple

Vec = Tuple[float, float, float]
Mat = Tuple[Vec, Vec, Vec]
IDENTITY: Mat = ((1., 0., 0.), (0., 1., 0.), (0., 0., 1.))
# Coordinate choice: Unity X->Fusion X, Unity Y->Fusion Z, Unity Z->Fusion Y.
# This basis is a reflection. Conjugate rotation matrices; don't swap Euler values.
UNITY_TO_FUSION: Mat = ((1., 0., 0.), (0., 0., 1.), (0., 1., 0.))


class GeometryError(ValueError):
    pass


def number(value, label="value") -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise GeometryError(f"{label} must be a finite number")
    value = float(value)
    if not math.isfinite(value):
        raise GeometryError(f"{label} must be finite")
    return value


def vector(values: Sequence[float], label="vector") -> Vec:
    if not isinstance(values, (list, tuple)) or len(values) != 3:
        raise GeometryError(f"{label} must contain three finite numbers")
    return tuple(number(x, label) for x in values)


def add(a: Vec, b: Vec) -> Vec:
    return tuple(x + y for x, y in zip(a, b))


def sub(a: Vec, b: Vec) -> Vec:
    return tuple(x - y for x, y in zip(a, b))


def scale(a: Vec, factor: float) -> Vec:
    return tuple(x * factor for x in a)


def dot(a: Vec, b: Vec) -> float:
    return sum(x * y for x, y in zip(a, b))


def cross(a: Vec, b: Vec) -> Vec:
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def unit(a: Vec) -> Vec:
    length = math.hypot(*a)
    if length < 1e-12 or not math.isfinite(length):
        raise GeometryError("Zero/invalid axis or horn reference direction")
    return scale(a, 1. / length)


def transpose(m: Mat) -> Mat:
    return tuple(tuple(m[j][i] for j in range(3)) for i in range(3))


def matvec(m: Mat, v: Vec) -> Vec:
    return tuple(dot(row, v) for row in m)


def matmul(a: Mat, b: Mat) -> Mat:
    return tuple(tuple(dot(row, col) for col in transpose(b)) for row in a)


def quaternion_matrix(q) -> Mat:
    if not isinstance(q, (list, tuple)) or len(q) != 4:
        raise GeometryError("Quaternion must contain x,y,z,w")
    values = tuple(number(x, "quaternion") for x in q)
    length = math.hypot(*values)
    if length < 1e-12 or not math.isfinite(length):
        raise GeometryError("Quaternion must be nonzero and normalizable")
    x, y, z, w = (x / length for x in values)
    return ((1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)),
            (2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)),
            (2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)))


def unwrap_near(angle: float, reference: float) -> float:
    return angle + math.tau * round((reference - angle) / math.tau)


@dataclass(frozen=True)
class Pose:
    """Neutral-relative translation in Fusion mm and active Fusion rotation matrix."""
    translation_mm: Vec = (0., 0., 0.)
    rotation: Mat = IDENTITY

    def __post_init__(self):
        object.__setattr__(self, "translation_mm", vector(self.translation_mm, "translation"))
        if not isinstance(self.rotation, (tuple, list)) or len(self.rotation) != 3:
            raise GeometryError("Rotation must be a 3x3 proper orthonormal matrix")
        matrix = tuple(vector(row, "rotation") for row in self.rotation)
        gram = matmul(matrix, transpose(matrix))
        if (max(abs(gram[i][j] - IDENTITY[i][j]) for i in range(3) for j in range(3)) > 1e-8
                or abs(dot(matrix[0], cross(matrix[1], matrix[2])) - 1) > 1e-8):
            raise GeometryError("Rotation must be proper and orthonormal")
        object.__setattr__(self, "rotation", matrix)

    @classmethod
    def fusion(cls, translation_mm=(0., 0., 0.), rotation_xyzw=(0., 0., 0., 1.)):
        return cls(vector(translation_mm), quaternion_matrix(rotation_xyzw))

    @classmethod
    def unity(cls, position_m, rotation_xyzw):
        """Input is already relative to the calibrated Unity neutral frame.

        Future pipeline adapter: Unity X pitch, Y yaw, Z roll; Y heave.
        This is a coordinate conversion, not motion scaling/conditioning.
        """
        translation = scale(matvec(UNITY_TO_FUSION, vector(position_m)), 1000.)
        rotation = matmul(matmul(UNITY_TO_FUSION, quaternion_matrix(rotation_xyzw)),
                          transpose(UNITY_TO_FUSION))
        return cls(translation, rotation)


@dataclass(frozen=True)
class Leg:
    channel: int
    shaft_point_mm: Vec
    shaft_axis: Vec
    axial_offset_mm: float
    reference_radial_direction: Vec
    top_local_mm: Vec
    horn_radius_mm: float
    rod_length_mm: float
    angle_limits_deg: Optional[Tuple[float, float]] = None

    def __post_init__(self):
        if isinstance(self.channel, bool) or not isinstance(self.channel, int):
            raise GeometryError("Channel must be an integer")
        for field in ("shaft_point_mm", "top_local_mm"):
            object.__setattr__(self, field, vector(getattr(self, field), field))
        axis = unit(vector(self.shaft_axis, "shaft_axis"))
        radial = vector(self.reference_radial_direction, "reference_radial_direction")
        radial = unit(sub(radial, scale(axis, dot(radial, axis))))
        object.__setattr__(self, "shaft_axis", axis)
        object.__setattr__(self, "reference_radial_direction", radial)
        for field in ("axial_offset_mm", "horn_radius_mm", "rod_length_mm"):
            value = number(getattr(self, field), field)
            if field != "axial_offset_mm" and value <= 0:
                raise GeometryError(f"{field} must be positive")
            object.__setattr__(self, field, value)
        if self.angle_limits_deg is not None:
            if len(self.angle_limits_deg) != 2:
                raise GeometryError("Angle limits require minimum and maximum")
            low, high = (number(x, "angle limit") for x in self.angle_limits_deg)
            if low >= high:
                raise GeometryError("Angle limits must be ordered")
            object.__setattr__(self, "angle_limits_deg", (low, high))

    @property
    def circle_centre_mm(self):
        return add(self.shaft_point_mm, scale(self.shaft_axis, self.axial_offset_mm))

    def horn_point(self, angle: float) -> Vec:
        u = self.reference_radial_direction
        v = cross(self.shaft_axis, u)
        radial = add(scale(u, math.cos(angle)), scale(v, math.sin(angle)))
        return add(self.circle_centre_mm, scale(radial, self.horn_radius_mm))

    def within_limits(self, angle: float) -> bool:
        if self.angle_limits_deg is None:
            return True
        low, high = self.angle_limits_deg
        return low <= math.degrees(angle) <= high


def leg_candidates(leg: Leg, top_global_mm: Vec):
    """Return stable algebraic branch labels +1/-1, never angle-sorted labels.

    C=P+d*a; H=C+r*(u*cos(theta)+v*sin(theta)); v=a cross u.
    |T-H|^2=L^2 reduces to A*cos(theta)+B*sin(theta)=g.
    """
    displacement = sub(vector(top_global_mm), leg.circle_centre_mm)
    a = dot(displacement, leg.reference_radial_direction)
    b = dot(displacement, cross(leg.shaft_axis, leg.reference_radial_direction))
    amplitude = math.hypot(a, b)
    rhs = (dot(displacement, displacement) + leg.horn_radius_mm**2 - leg.rod_length_mm**2) / (2*leg.horn_radius_mm)
    if not all(math.isfinite(x) for x in (amplitude, rhs)):
        raise GeometryError("Non-finite geometry calculation")
    if amplitude < 1e-10:
        reason = "indeterminate horn angle" if abs(rhs) < 1e-10 else "unreachable shaft-axis target"
        raise GeometryError(reason)
    ratio = rhs / amplitude
    if abs(ratio) > 1. + 1e-12:
        raise GeometryError("rod sphere does not intersect horn circle")
    ratio = max(-1., min(1., ratio)) # Only roundoff, not unreachable-pose clamping.
    conditioning = math.sqrt(max(0., 1. - ratio*ratio))
    if conditioning < 1e-6:
        raise GeometryError("tangent/ill-conditioned individual leg solution")
    phase = math.atan2(b, a)
    opening = math.acos(ratio)
    return {1: phase + opening, -1: phase - opening}, conditioning


@dataclass(frozen=True)
class Solution:
    legs: tuple
    errors: tuple

    @property
    def found(self):
        return not self.errors and len(self.legs) == 6

    @property
    def angles_deg(self):
        return tuple(row["angle_deg_from_reference"] for row in self.legs) if self.found else ()

    def as_dict(self):
        return {"kinematic_solution_found": self.found,
                "hardware_output_enabled": False,
                "mechanical_workspace_validated": False,
                "errors": list(self.errors),
                "legs": list(self.legs) if self.found else []}


class Model:
    def __init__(self, legs: Sequence[Leg], neutral_position_mm, neutral_rotation_xyzw=(0., 0., 0., 1.)):
        if sorted(leg.channel for leg in legs) != list(range(6)):
            raise GeometryError("Exactly one leg for each channel 0..5 is required")
        self.legs = tuple(sorted(legs, key=lambda leg: leg.channel))
        self.neutral_position_mm = vector(neutral_position_mm, "neutral position")
        self.neutral_rotation = quaternion_matrix(neutral_rotation_xyzw)
        self.neutral_angles = []
        self.branches = []
        for leg in self.legs:
            top = self.top_point(leg, Pose())
            try:
                candidates, _ = leg_candidates(leg, top)
                eligible = [(branch, unwrap_near(angle, 0.)) for branch, angle in candidates.items()]
                eligible = [(branch, angle) for branch, angle in eligible if leg.within_limits(angle)]
                if not eligible:
                    raise GeometryError("no neutral branch within configured angle limits")
                ordered = sorted(eligible, key=lambda pair: abs(pair[1]))
                if len(ordered) == 2 and abs(abs(ordered[0][1]) - abs(ordered[1][1])) < 1e-10:
                    raise GeometryError("ambiguous neutral branch; supply a better horn reference")
                branch, angle = ordered[0]
            except GeometryError as exc:
                raise GeometryError(f"Neutral channel {leg.channel}: {exc}") from exc
            self.branches.append(branch)
            self.neutral_angles.append(angle)

    def top_point(self, leg: Leg, pose: Pose):
        # Local pose is relative to the neutral CONTROL frame; translation rotates with it.
        relative = add(pose.translation_mm, matvec(pose.rotation, leg.top_local_mm))
        return add(self.neutral_position_mm, matvec(self.neutral_rotation, relative))

    def solve(self, pose: Pose, previous_angles_deg=None, max_step_degrees=None) -> Solution:
        if previous_angles_deg is None:
            previous = self.neutral_angles
        else:
            if len(previous_angles_deg) != 6:
                raise GeometryError("Previous solution requires six angles")
            previous = [math.radians(number(x, "previous angle")) for x in previous_angles_deg]
        if max_step_degrees is not None:
            max_step_degrees = number(max_step_degrees, "max step")
            if max_step_degrees <= 0:
                raise GeometryError("Max step must be positive")
        rows, errors = [], []
        for index, leg in enumerate(self.legs):
            top = self.top_point(leg, pose)
            try:
                candidates, conditioning = leg_candidates(leg, top)
                angle = unwrap_near(candidates[self.branches[index]], previous[index])
                if not leg.within_limits(angle):
                    raise GeometryError("locked branch outside configured angle limits")
                if max_step_degrees is not None and abs(math.degrees(angle - previous[index])) > max_step_degrees:
                    raise GeometryError("solution exceeds requested continuity step")
                horn = leg.horn_point(angle)
                residual = abs(math.dist(horn, top) - leg.rod_length_mm)
                if not math.isfinite(residual) or residual > 1e-7:
                    raise GeometryError("rod-length reconstruction residual too large")
                rows.append({"channel": leg.channel,
                             "angle_deg_from_reference": math.degrees(angle),
                             "neutral_relative_angle_deg": math.degrees(angle-self.neutral_angles[index]),
                             "branch": self.branches[index],
                             "horn_joint_global_mm": list(horn),
                             "top_joint_global_mm": list(top),
                             "rod_length_residual_mm": residual,
                             "individual_leg_condition_sine": conditioning})
            except GeometryError as exc:
                errors.append(f"Channel {leg.channel}: {exc}")
        return Solution(tuple(rows) if not errors else (), tuple(errors))
