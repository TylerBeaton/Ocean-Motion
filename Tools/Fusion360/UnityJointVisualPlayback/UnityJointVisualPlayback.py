"""Short, approximate Unity-CSV-driven Fusion joint visualization.

Visual proof only: not exact IK, motion control, or actuator-safe output.
Requires the sibling JointPoseProbe and UnityBoatTelemetryPlayback folders.
"""

import math
import os
import sys
import time
import traceback

import adsk.core
import adsk.fusion

TOOLS_DIR = os.path.dirname(os.path.dirname(os.path.realpath(__file__)))
for folder in ("JointPoseProbe", "UnityBoatTelemetryPlayback"):
    path = os.path.join(TOOLS_DIR, folder)
    if path not in sys.path:
        sys.path.insert(0, path)
from JointPoseProbe import (_drift, _local_response, _pose_delta, _restore_to_baseline,
                            _set_angle, _top_pose, _within_limit, _within_visual_envelope,
                            signed_angle_difference_degrees)
from joint_solver import solve_min_norm
from telemetry_math import fusion_delta_from_unity_pose, parse_telemetry_rows

START_SECONDS = 2.0
END_SECONDS = 5.6
FRAME_STEP_SECONDS = 0.2
FULL_FRAME_STEP_SECONDS = 0.4
TRANSLATION_SCALE = 0.02
ROTATION_SCALE = 0.3
FULL_TRANSLATION_SCALE = 0.012
FULL_ROTATION_SCALE = 0.18
PROBE_DEGREES = 1.0
MAX_STEP_DEGREES = 2.5
# Offline visual-only ceiling; Fusion's enabled joint limits remain authoritative.
MAX_TOTAL_DEGREES = 35.0
MAX_PLATE_DISPLACEMENT_MM = 25.0
MAX_PLATE_TILT_DEGREES = 8.0


def _relative_component_pose(parent, child):
    """Rigid relative transform; removes any shared whole-assembly movement."""
    return tuple(tuple(sum(parent[k][row] * child[k][col] for k in range(3))
                       for col in range(3)) +
                 (sum(parent[k][row] * (child[k][3] - parent[k][3]) for k in range(3)),)
                 for row in range(3))


def _servo_body_poses(joints):
    poses = []
    for joint, _ in joints:
        pair = []
        for occurrence in (joint.occurrenceOne, joint.occurrenceTwo):
            if occurrence is None:
                raise ValueError("Cannot inspect both component occurrences of joint " + joint.name)
            matrix = occurrence.transform2
            pose = tuple(tuple(matrix.getCell(r, c) for c in range(4)) for r in range(3))
            if not all(math.isfinite(v) for row in pose for v in row):
                raise ValueError("Non-finite component transform at " + occurrence.fullPathName)
            pair.append(pose)
        poses.append(tuple(pair))
    return poses


def _pose_test_target(baseline, fraction=1.0):
    """One small world-heave/local-tilt request, no lateral travel or yaw.

    Use a rotation vector to match _pose_delta, rather than composing Euler
    angles (which introduces an unintended third rotation component).
    """
    if not math.isfinite(fraction) or not 0 <= fraction <= 1:
        raise ValueError("Pose-test fraction must be between zero and one")
    if fraction == 0:
        return baseline
    vector = (math.radians(0.15) * fraction, math.radians(0.15) * fraction, 0.0)
    angle = math.sqrt(sum(v * v for v in vector))
    x, y, z = (v / angle for v in vector)
    skew = ((0, -z, y), (z, 0, -x), (-y, x, 0))
    rotation = tuple(tuple(
        (1.0 if row == col else 0.0) + math.sin(angle) * skew[row][col] +
        (1 - math.cos(angle)) * sum(skew[row][k] * skew[k][col] for k in range(3))
        for col in range(3)) for row in range(3))
    return tuple(tuple(sum(baseline[row][k] * rotation[k][col] for k in range(3))
                       for col in range(3)) +
                 (baseline[row][3] + (0.1 * fraction if row == 2 else 0.0),)
                 for row in range(3))


def _request_constrained_pose(root, top, pose):
    matrix = top.transform2.copy()
    for row in range(3):
        for col in range(4):
            if not math.isfinite(pose[row][col]):
                raise ValueError("Non-finite pose request")
            if not matrix.setCell(row, col, pose[row][col]):
                raise ValueError("Could not construct pose matrix")
    # Unlike assigning transform2, this explicitly asks Fusion to honor joints.
    # A True result is NOT proof of target accuracy; always measure afterwards.
    if not root.transformOccurrences([top], [matrix], False):
        raise ValueError("Fusion rejected the joint-aware pose request")
    adsk.doEvents()
    return _top_pose(root)


def _run_pose_test(app, root, joints):
    """One constrained target and restoration; never starts CSV playback."""
    top = next((root.allOccurrences.item(i) for i in range(root.allOccurrences.count)
                if root.allOccurrences.item(i).fullPathName == "Top Platform:1"), None)
    if top is None:
        raise ValueError("Top Platform:1 not found")
    baseline = _top_pose(root)
    originals = [motion.rotationValue for _, motion in joints]
    body_baseline = _servo_body_poses(joints)
    target = _pose_test_target(baseline)
    actual = baseline
    translation = rotation = (0.0, 0.0, 0.0)
    changes = [0.0] * len(joints)
    try:
        for step in range(1, 11):
            actual = _request_constrained_pose(root, top, _pose_test_target(baseline, step / 10))
            translation, rotation = _pose_delta(baseline, actual)
            changes = [signed_angle_difference_degrees(original, motion.rotationValue)
                       for original, (_, motion) in zip(originals, joints)]
            finite = all(math.isfinite(v) for v in (*translation, *rotation, *changes))
            travel, name = _peak_joint_travel(joints, originals)
            if not finite or not _within_visual_joint_travel(travel):
                raise ValueError("Pose test exceeded finite/visual joint bounds at {}".format(name))
            if any(not _within_limit(motion, motion.rotationValue) for _, motion in joints):
                raise ValueError("Pose test readback exceeded an enabled joint limit")
            distance, tilt = _drift(baseline, actual)
            if distance > MAX_PLATE_DISPLACEMENT_MM or tilt > MAX_PLATE_TILT_DEGREES:
                raise ValueError("Pose test exceeded visual plate bounds: {:.3f} mm, {:.3f} deg".format(
                    distance, tilt))
            app.activeViewport.refresh()
            time.sleep(0.3)
        error_mm, error_deg = _drift(target, actual)
        accepted = error_mm <= 0.1 and error_deg <= 0.05
        body_actual = _servo_body_poses(joints)
        body_lines = []
        articulated = 0
        for (joint, _), before, after in zip(joints, body_baseline, body_actual):
            relative_mm, relative_deg = _drift(_relative_component_pose(*before),
                                              _relative_component_pose(*after))
            articulated += relative_deg > 0.001
            one_mm, one_deg = _drift(before[0], after[0])
            two_mm, two_deg = _drift(before[1], after[1])
            name = joint.assemblyContext.fullPathName if joint.assemblyContext else joint.name
            body_lines.append(
                "{}: relative {:.4f} deg / {:.4f} mm; body 1 world {:.4f} mm / {:.4f} deg; "
                "body 2 world {:.4f} mm / {:.4f} deg".format(
                    name, relative_deg, relative_mm, one_mm, one_deg, two_mm, two_deg))
        articulation = ("NO SERVO-BODY ARTICULATION DETECTED — linkage solving is unproven."
                        if not articulated else
                        "Relative body rotation detected at {}/6 servo joints; inspect all linkages.".format(articulated))
        app.activeViewport.refresh()
        app.userInterface.messageBox(
            "Constraint-aware pose test: {}.\n"
            "Requested world XYZ mm: (0, 0, +1); local rotation XYZ deg: (+0.15, +0.15, 0).\n"
            "Achieved world XYZ mm: {}\nAchieved local rotation XYZ deg: {}\n"
            "Full-pose error: {:.3f} mm, {:.3f} deg (tolerances 0.1 mm / 0.05 deg).\n"
            "Six revolute angle changes (deg): {}\n{}\n{}\n\n"
            "Inspect ALL SIX linkages before OK. Pose agreement alone does not prove articulation.\n"
            "OK restores the starting pose. This is one probe, not CSV or hardware validation."
            .format("WITHIN POSE TOLERANCE" if accepted else "NOT ACCEPTED",
                    tuple(round(v, 3) for v in translation),
                    tuple(round(v, 3) for v in rotation), error_mm, error_deg,
                    tuple(round(v, 3) for v in changes), articulation, "\n".join(body_lines)))
    finally:
        # Try restoration even if Fusion partially changed the assembly then
        # rejected the target. Do not fall back to six drift-prone joint sweeps.
        try:
            restored = _request_constrained_pose(root, top, baseline)
            mm, degrees = _drift(baseline, restored)
            joint_error, _ = _peak_joint_travel(joints, originals)
            app.activeViewport.refresh()
            if not (math.isfinite(mm) and math.isfinite(degrees) and math.isfinite(joint_error)
                    and mm <= 0.1 and degrees <= 0.05 and joint_error <= 0.05):
                app.userInterface.messageBox(
                    "Pose-test restoration mismatch: {:.3f} mm, {:.3f} deg, {:.3f} deg joint. "
                    "DO NOT SAVE this duplicate; reopen a clean copy."
                    .format(mm, degrees, joint_error))
        except Exception:
            app.userInterface.messageBox("Pose-test restoration failed. DO NOT SAVE this duplicate.\n" +
                                         traceback.format_exc())


def _visual_targets(frames, translation_scale=TRANSLATION_SCALE,
                    rotation_scale=ROTATION_SCALE):
    poses = []
    for frame in frames:
        rotation, translation = fusion_delta_from_unity_pose(
            frame["position_m"], frame["quaternion"], 100.0, translation_scale)
        poses.append(tuple(tuple(rotation[row][col] for col in range(3)) +
                           (translation[row],) for row in range(3)))
    initial = poses[0]
    targets = []
    for pose in poses:
        displacement, rotation = _pose_delta(initial, pose)
        targets.append((displacement[2], rotation[0] * rotation_scale,
                        rotation[1] * rotation_scale))
    return targets


def _select_frames(rows, full=False):
    if full:
        first = rows[0]["time_seconds"]
        last = rows[-1]["time_seconds"]
        selected = []
        for step in range(int((last - first) / FULL_FRAME_STEP_SECONDS) + 1):
            when = first + step * FULL_FRAME_STEP_SECONDS
            frame = min(rows, key=lambda row: abs(row["time_seconds"] - when))
            if not selected or frame["sequence"] != selected[-1]["sequence"]:
                selected.append(frame)
        if not selected or selected[-1]["sequence"] != rows[-1]["sequence"]:
            selected.append(rows[-1])
        if len(selected) < 3:
            raise ValueError("Full CSV needs at least three distinct samples")
        return selected
    selected = []
    for step in range(round((END_SECONDS - START_SECONDS) / FRAME_STEP_SECONDS) + 1):
        when = START_SECONDS + step * FRAME_STEP_SECONDS
        frame = min(rows, key=lambda row: abs(row["time_seconds"] - when))
        if not selected or frame["sequence"] != selected[-1]["sequence"]:
            selected.append(frame)
    if len(selected) < 3 or selected[0]["time_seconds"] >= selected[-1]["time_seconds"]:
        raise ValueError("CSV must contain at least three frames from 2 to 5.6 seconds")
    return selected


def _choose_csv(ui):
    dialog = ui.createFileDialog()
    dialog.title = "Choose a Unity telemetry CSV with pitch and roll"
    dialog.filter = "CSV files (*.csv)"
    dialog.isMultiSelectEnabled = False
    if dialog.showOpen() != adsk.core.DialogResults.DialogOK:
        return None
    return dialog.filename


def _choose_mode(ui):
    value, cancelled = ui.inputBox(
        "Type pose-test for ONE joint-aware pose diagnostic (no CSV), full for the complete CSV, "
        "or preview for the short CSV segment. CSV modes remain approximate joint control.",
        "Unity Joint Visual Playback", "full")
    if cancelled:
        return None
    mode = value.strip().lower()
    if mode not in ("full", "preview", "pose-test"):
        raise ValueError("Playback mode must be full, preview, or pose-test")
    return mode


def _playback_delay(start_wall, frame_time, first_time, now):
    return max(0.0, start_wall + frame_time - first_time - now)


def _calibrate(root, joints, originals, baseline):
    columns = []
    for index, (_, motion) in enumerate(joints):
        before = _top_pose(root)
        requested = motion.rotationValue + math.radians(PROBE_DEGREES)
        if not _within_limit(motion, requested):
            raise ValueError("Leg {} cannot be probed within its joint limit".format(index + 1))
        try:
            _set_angle(motion, requested)
            columns.append(_local_response(before, _top_pose(root), PROBE_DEGREES))
        finally:
            readings = _restore_to_baseline(joints, originals, baseline, root)
        if not _within_visual_envelope(readings[-1]):
            raise ValueError("Calibration drift exceeded visual envelope at leg {}: {}".format(
                index + 1, readings))
    return columns


def _current_axes(start_pose, root):
    translation, rotation = _pose_delta(start_pose, _top_pose(root))
    return (translation[2], rotation[0], rotation[1]), translation, rotation


def _peak_joint_travel(joints, originals):
    excursions = [
        (abs(signed_angle_difference_degrees(original, motion.rotationValue)),
         joint.assemblyContext.fullPathName if joint.assemblyContext else joint.name)
        for (joint, motion), original in zip(joints, originals)
    ]
    return max(excursions)


def _within_visual_joint_travel(degrees):
    return math.isfinite(degrees) and degrees <= MAX_TOTAL_DEGREES


def _drive_frame(root, joints, originals, start_pose, columns, target):
    current, _, _ = _current_axes(start_pose, root)
    error = tuple(wanted - measured for wanted, measured in zip(target, current))
    increments = solve_min_norm(columns, error)
    peak = max(abs(value) for value in increments)
    scale = min(1.0, MAX_STEP_DEGREES / peak) if peak else 1.0
    for (_, motion), increment in zip(joints, increments):
        candidate = motion.rotationValue + math.radians(increment * scale)
        if not _within_limit(motion, candidate):
            raise ValueError("Frame command exceeded an enabled joint limit")
        # Do not publish the five partially updated configurations. This does
        # not defer Fusion's constraint solve or make these writes atomic.
        motion.rotationValue = candidate
        excursion, name = _peak_joint_travel(joints, originals)
        if not _within_visual_joint_travel(excursion):
            raise ValueError("{} reached {:.2f} deg from start (visual cap {:.1f} deg)".format(
                name, excursion, MAX_TOTAL_DEGREES))
    adsk.doEvents()
    actual, translation, rotation = _current_axes(start_pose, root)
    excursion, name = _peak_joint_travel(joints, originals)
    if not _within_visual_joint_travel(excursion):
        raise ValueError("{} settled at {:.2f} deg from start (visual cap {:.1f} deg)".format(
            name, excursion, MAX_TOTAL_DEGREES))
    distance = math.sqrt(sum(value * value for value in translation))
    tilt = math.sqrt(sum(value * value for value in rotation))
    if distance > MAX_PLATE_DISPLACEMENT_MM or tilt > MAX_PLATE_TILT_DEGREES:
        raise ValueError("Plate exceeded the bounded visual experiment: {:.2f} mm, {:.2f} deg".format(
            distance, tilt))
    return actual, translation, rotation


def run(_context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    root = None
    baseline = None
    originals = []
    joints = []
    changed = False
    report = []
    current_time = None
    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            raise ValueError("Open a disposable duplicate of the Fusion assembly")
        root = design.rootComponent
        joints = [(joint, adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion))
                  for joint in root.allJoints]
        joints = [(joint, motion) for joint, motion in joints if motion]
        if len(joints) != 6:
            raise ValueError("Expected six revolute joints, found {}".format(len(joints)))
        mode = _choose_mode(ui)
        if mode is None:
            return
        if mode == "pose-test":
            _run_pose_test(app, root, joints)
            return
        filename = _choose_csv(ui)
        if not filename:
            return
        with open(filename, "r", newline="", encoding="utf-8-sig") as source:
            frames = _select_frames(parse_telemetry_rows(source), full=(mode == "full"))
        targets = _visual_targets(
            frames,
            translation_scale=FULL_TRANSLATION_SCALE if mode == "full" else TRANSLATION_SCALE,
            rotation_scale=FULL_ROTATION_SCALE if mode == "full" else ROTATION_SCALE)
        originals = [motion.rotationValue for _, motion in joints]
        baseline = _top_pose(root)
        changed = True
        columns = _calibrate(root, joints, originals, baseline)
        start_pose = _top_pose(root)
        calibration_mm, calibration_deg = _drift(baseline, start_pose)
        report = []
        translation = (0.0, 0.0, 0.0)
        rotation = (0.0, 0.0, 0.0)
        start_wall = time.monotonic()
        first_time = frames[0]["time_seconds"]
        for frame, target in zip(frames, targets):
            current_time = frame["time_seconds"]
            delay = _playback_delay(start_wall, frame["time_seconds"], first_time, time.monotonic())
            if delay:
                time.sleep(delay)
            actual, translation, rotation = _drive_frame(root, joints, originals, start_pose, columns, target)
            app.activeViewport.refresh()
            report.append("t {:.2f}s target {} achieved {}".format(
                frame["time_seconds"], tuple(round(v, 2) for v in target),
                tuple(round(v, 2) for v in actual)))
        message = (
            "Unity CSV -> Fusion joint visual {} complete (not exact IK).\n"
            "Calibration drift {:.2f} mm, {:.2f} deg.\n"
            "Axes: Fusion heave mm, local X/Y tilt deg.\n"
            "Final unwanted X/Y: {:+.2f}, {:+.2f} mm; local Z: {:+.2f} deg.\n"
            "{}\n\nInspect all six linkages. Press OK to restore joint angles."
        ).format(mode, calibration_mm, calibration_deg, translation[0], translation[1], rotation[2],
                 "\n".join(report))
        ui.messageBox(message)
    except Exception:
        progress = "Stopped at CSV t {:.2f}s.\n".format(current_time) if current_time is not None else ""
        if report:
            progress += "Completed frames:\n{}\n".format("\n".join(report))
        ui.messageBox("Unity joint visual preview stopped.\n{}{}".format(progress, traceback.format_exc()))
    finally:
        if changed and root is not None and baseline is not None:
            try:
                readings = _restore_to_baseline(joints, originals, baseline, root)
                app.activeViewport.refresh()
                if not _within_visual_envelope(readings[-1]):
                    ui.messageBox("Restoration outside visual envelope: {}. Do not save this pose.".format(
                        readings[-1]))
            except Exception:
                ui.messageBox("Restoration failed on the duplicate design:\n{}".format(traceback.format_exc()))


def stop(_context):
    pass
