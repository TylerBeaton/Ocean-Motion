"""Measure how each servo revolute moves the top platform on a copy of the design."""

import math
import traceback

import adsk.core
import adsk.fusion


def _pose_delta(before, after):
    """Return world translation (mm), local rotation vector (degrees)."""
    translation = tuple((after[row][3] - before[row][3]) * 10.0 for row in range(3))
    relative = tuple(tuple(sum(before[k][row] * after[k][col] for k in range(3))
                           for col in range(3)) for row in range(3))
    angle = math.acos(max(-1.0, min(1.0,
        (sum(relative[i][i] for i in range(3)) - 1.0) / 2.0)))
    if angle < 1e-7:
        rotation = (0.0, 0.0, 0.0)
    else:
        factor = math.degrees(angle / (2.0 * math.sin(angle)))
        rotation = (
            (relative[2][1] - relative[1][2]) * factor,
            (relative[0][2] - relative[2][0]) * factor,
            (relative[1][0] - relative[0][1]) * factor,
        )
    return translation, rotation


def _max_other_joint_change(baseline, current, driven_index):
    return max(math.degrees(abs(math.atan2(math.sin(a - b), math.cos(a - b))))
               for index, (a, b) in enumerate(zip(baseline, current))
               if index != driven_index)


def _top_pose(root):
    for index in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(index)
        if occurrence.fullPathName == "Top Platform:1":
            matrix = occurrence.transform2
            return tuple(tuple(matrix.getCell(row, col) for col in range(4))
                         for row in range(3))
    raise ValueError("Top Platform:1 not found in root occurrences")


def run(_context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    active_motion = None
    original_angle = None
    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            raise ValueError("Open the duplicated Fusion design first")
        root = design.rootComponent
        joints = []
        for joint in root.allJoints:
            motion = adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion)
            if motion:
                joints.append((joint, motion))
        if len(joints) != 6:
            raise ValueError("Expected six root-context revolute joints; found {}".format(len(joints)))
        answer, cancelled = ui.inputBox(
            "Test each joint by a small relative angle in degrees (max 5; use 2). "
            "The script restores each angle before the next test.",
            "Six-Joint Response Probe", "2",
        )
        if cancelled:
            return
        degrees = float(answer)
        if not math.isfinite(degrees) or not 0 < abs(degrees) <= 5:
            raise ValueError("Choose a nonzero finite angle of at most 5 degrees")
        baseline = _top_pose(root)
        baseline_angles = tuple(motion.rotationValue for _, motion in joints)
        lines = ["Top Platform:1 response for each +{:.1f} degree joint change:".format(degrees),
                 "Translation = world X/Y/Z mm; rotation = local X/Y/Z degrees.",
                 "Other = maximum change of undriven joint angles."]
        clean_restoration = True
        for joint_index, (joint, motion) in enumerate(joints):
            active_motion = motion
            original_angle = motion.rotationValue
            target = original_angle + math.radians(degrees)
            limits = motion.rotationLimits
            if limits and ((limits.isMinimumValueEnabled and target < limits.minimumValue) or
                           (limits.isMaximumValueEnabled and target > limits.maximumValue)):
                raise ValueError("{} violates its enabled rotation limit".format(joint.name))
            try:
                motion.rotationValue = target
                adsk.doEvents()
                moved = _top_pose(root)
                translation, rotation = _pose_delta(baseline, moved)
                moved_angles = tuple(item.rotationValue for _, item in joints)
                other_change = _max_other_joint_change(baseline_angles, moved_angles, joint_index)
                context = joint.assemblyContext.fullPathName if joint.assemblyContext else "root"
                lines.append("{} / {}: mm ({:+.2f}, {:+.2f}, {:+.2f}); deg ({:+.3f}, {:+.3f}, {:+.3f}); other {:.3f} deg".format(
                    context, joint.name, *translation, *rotation, other_change))
            finally:
                motion.rotationValue = original_angle
                adsk.doEvents()
                active_motion = None
                original_angle = None
            restored_angles = tuple(item.rotationValue for _, item in joints)
            angle_residual = max(math.degrees(abs(math.atan2(math.sin(a - b), math.cos(a - b))))
                                 for a, b in zip(baseline_angles, restored_angles))
            pose_translation, pose_rotation = _pose_delta(baseline, _top_pose(root))
            pose_residual = math.sqrt(sum(value * value for value in pose_translation))
            rotation_residual = math.sqrt(sum(value * value for value in pose_rotation))
            if angle_residual > 0.05 or pose_residual > 0.1 or rotation_residual > 0.05:
                lines.append("Stopped after {}: restoration residual {:.3f} deg joint, {:.3f} mm plate, {:.3f} deg plate.".format(
                    context, angle_residual, pose_residual, rotation_residual))
                clean_restoration = False
                break
        app.activeViewport.refresh()
        if clean_restoration:
            lines.append("All six joint angles and top pose returned near baseline.")
        ui.messageBox("\n".join(lines))
    except Exception:
        ui.messageBox("Response probe failed:\n{}".format(traceback.format_exc()))
    finally:
        if active_motion is not None and original_angle is not None:
            active_motion.rotationValue = original_angle
            adsk.doEvents()
            app.activeViewport.refresh()


def stop(_context):
    pass
