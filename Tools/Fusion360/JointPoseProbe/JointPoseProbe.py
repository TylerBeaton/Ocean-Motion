"""Approximate, bounded 3-axis visual joint pose probe for a duplicate Fusion assembly.

Not CSV playback, validated inverse kinematics, or physical actuator control.
"""

import math
import os
import sys
import traceback

import adsk.core
import adsk.fusion

SCRIPT_DIR = os.path.dirname(os.path.realpath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)
from joint_solver import solve_min_norm

TOP_PATH = "Top Platform:1"
PROBE_DEGREES = 1.0
MAX_COMMAND_DEGREES = 2.0
TARGET = (1.0, 0.15, 0.15)  # Fusion heave mm, local X/Y tilt degrees
# Visual drift envelope, NOT a hardware or IK accuracy limit.
MAX_VISUAL_DRIFT_MM = 5.0
MAX_VISUAL_DRIFT_DEG = 2.0
MAX_VISUAL_JOINT_DRIFT_DEG = 2.0


def signed_angle_difference_degrees(before, after):
    difference = after - before
    return math.degrees(math.atan2(math.sin(difference), math.cos(difference)))


def _top_pose(root):
    for index in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(index)
        if occurrence.fullPathName == TOP_PATH:
            matrix = occurrence.transform2
            return tuple(tuple(matrix.getCell(row, col) for col in range(4))
                         for row in range(3))
    raise ValueError("{} not found".format(TOP_PATH))


def _pose_delta(before, after):
    translation = tuple((after[row][3] - before[row][3]) * 10.0 for row in range(3))
    relative = tuple(tuple(sum(before[k][row] * after[k][col] for k in range(3))
                           for col in range(3)) for row in range(3))
    angle = math.acos(max(-1.0, min(1.0,
        (sum(relative[i][i] for i in range(3)) - 1.0) / 2.0)))
    if angle < 1e-7:
        rotation = (0.0, 0.0, 0.0)
    else:
        factor = math.degrees(angle / (2.0 * math.sin(angle)))
        rotation = ((relative[2][1] - relative[1][2]) * factor,
                    (relative[0][2] - relative[2][0]) * factor,
                    (relative[1][0] - relative[0][1]) * factor)
    return translation, rotation


def _local_response(before, after, step_degrees):
    translation, rotation = _pose_delta(before, after)
    return (translation[2] / step_degrees,
            rotation[0] / step_degrees,
            rotation[1] / step_degrees)


def _within_limit(motion, value):
    limits = motion.rotationLimits
    return not limits or not (
        (limits.isMinimumValueEnabled and value < limits.minimumValue) or
        (limits.isMaximumValueEnabled and value > limits.maximumValue))


def _set_angle(motion, angle):
    if not _within_limit(motion, angle):
        raise ValueError("Joint command exceeds an enabled Fusion joint limit")
    motion.rotationValue = angle
    adsk.doEvents()


def _restore_all_joints(joints, originals):
    # A driven joint can change all five other revolute angles in this assembly.
    for (_, motion), original in reversed(list(zip(joints, originals))):
        _set_angle(motion, original)


def _drift(baseline, current):
    translation, rotation = _pose_delta(baseline, current)
    return (math.sqrt(sum(value * value for value in translation)),
            math.sqrt(sum(value * value for value in rotation)))


def _restore_to_baseline(joints, originals, baseline, root):
    """Try at most two sweeps; return measured pose and joint residuals."""
    readings = []
    for _ in range(2):
        _restore_all_joints(joints, originals)
        mm, degrees = _drift(baseline, _top_pose(root))
        joint_residual = max(abs(signed_angle_difference_degrees(original, motion.rotationValue))
                             for original, (_, motion) in zip(originals, joints))
        readings.append((mm, degrees, joint_residual))
        if mm <= 0.1 and degrees <= 0.05 and joint_residual <= 0.05:
            break
    return readings


def _within_visual_envelope(reading):
    mm, pose_degrees, joint_degrees = reading
    return (math.isfinite(mm) and math.isfinite(pose_degrees) and math.isfinite(joint_degrees)
            and mm <= MAX_VISUAL_DRIFT_MM and pose_degrees <= MAX_VISUAL_DRIFT_DEG
            and joint_degrees <= MAX_VISUAL_JOINT_DRIFT_DEG)


def run(_context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    joints = []
    originals = []
    baseline = None
    root = None
    changed = False
    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            raise ValueError("Open a duplicate of the Fusion assembly first")
        root = design.rootComponent
        for joint in root.allJoints:
            motion = adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion)
            if motion:
                joints.append((joint, motion))
        if len(joints) != 6:
            raise ValueError("Expected six revolute joints, found {}".format(len(joints)))
        originals = [motion.rotationValue for _, motion in joints]
        baseline = _top_pose(root)
        response = []
        for index, (_, motion) in enumerate(joints):
            probe_before = _top_pose(root)
            target_angle = motion.rotationValue + math.radians(PROBE_DEGREES)
            if not _within_limit(motion, target_angle):
                raise ValueError("Leg {} cannot be probed within its joint limits".format(index + 1))
            try:
                changed = True
                _set_angle(motion, target_angle)
                response.append(_local_response(probe_before, _top_pose(root), PROBE_DEGREES))
                moved_angles = [item.rotationValue for _, item in joints]
            finally:
                restore_readings = _restore_to_baseline(joints, originals, baseline, root)
            if not _within_visual_envelope(restore_readings[-1]):
                residual = [signed_angle_difference_degrees(a, item.rotationValue)
                            for a, (_, item) in zip(originals, joints)]
                driven = [signed_angle_difference_degrees(a, b)
                          for a, b in zip(originals, moved_angles)]
                raise ValueError(
                    "Visual drift envelope exceeded after leg {}. Sweeps (mm, pose deg, joint deg): {}\n"
                    "Driven angles (deg): {}\nRestored angles (deg): {}"
                    .format(index + 1, restore_readings,
                            [round(v, 3) for v in driven], [round(v, 3) for v in residual]))

        # The CAD solver settles approximately, so use its actual current pose.
        playback_baseline = _top_pose(root)
        calibration_drift = _drift(baseline, playback_baseline)
        increments = solve_min_norm(response, TARGET)
        peak = max(abs(value) for value in increments)
        scale = min(1.0, MAX_COMMAND_DEGREES / peak) if peak else 1.0
        desired = tuple(value * scale for value in TARGET)
        increments = tuple(value * scale for value in increments)
        for (_, motion), increment in zip(joints, increments):
            _set_angle(motion, motion.rotationValue + math.radians(increment))
        app.activeViewport.refresh()
        translation, rotation = _pose_delta(playback_baseline, _top_pose(root))
        commands = ", ".join("{} {:+.2f} deg".format(
            joint.assemblyContext.fullPathName if joint.assemblyContext else joint.name, increment)
            for (joint, _), increment in zip(joints, increments))
        message = (
            "Approximate three-axis visual pose (not CSV playback or hardware IK).\n"
            "Calibration drift from initial pose: {:.3f} mm, {:.3f} deg.\n"
            "Target Fusion heave/X tilt/Y tilt: {:+.3f} mm, {:+.3f} deg, {:+.3f} deg\n"
            "Achieved heave/X tilt/Y tilt: {:+.3f} mm, {:+.3f} deg, {:+.3f} deg\n"
            "Unrequested world X/Y: {:+.3f}, {:+.3f} mm; local Z: {:+.3f} deg\n"
            "Joint commands: {}\n\n"
            "Inspect all six legs before pressing OK to restore the starting joint angles."
        ).format(*calibration_drift, *desired, translation[2], rotation[0], rotation[1],
                 translation[0], translation[1], rotation[2], commands)
        ui.messageBox(message)
    except Exception:
        ui.messageBox("Three-axis pose probe failed:\n{}".format(traceback.format_exc()))
    finally:
        if changed:
            try:
                readings = _restore_to_baseline(joints, originals, baseline, root)
                app.activeViewport.refresh()
                mm, degrees, joint_degrees = readings[-1]
                if not _within_visual_envelope(readings[-1]):
                    ui.messageBox("Restoration outside visual envelope: {:.3f} mm, {:.3f} deg pose, {:.3f} deg joint. "
                                  "Do not save this duplicate pose.".format(mm, degrees, joint_degrees))
            except Exception:
                ui.messageBox("Restoration failed on the duplicate design:\n{}".format(traceback.format_exc()))


def stop(_context):
    pass
