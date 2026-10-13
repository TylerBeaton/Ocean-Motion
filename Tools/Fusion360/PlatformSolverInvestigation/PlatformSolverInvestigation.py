"""Disposable-copy experiment for platform-driven constraint reconnection.

Not a CSV player, validated inverse kinematics, or physical actuator output.
All helpers and reports live here; previous Ocean Motion tools are untouched.
"""
import importlib.util
import json
import math
import os
import pathlib
import time
import traceback
import uuid

import adsk.core
import adsk.fusion

HERE = pathlib.Path(__file__).resolve().parent


def _helper(name):
    spec = importlib.util.spec_from_file_location("platform_investigation_" + name, HERE / (name + ".py"))
    if spec is None or spec.loader is None:
        raise RuntimeError("Missing investigation helper: " + name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


measurements = _helper("measurements")
driver = _helper("reconnect")


def pose(matrix):
    value = tuple(tuple(matrix.getCell(r, c) for c in range(4)) for r in range(3))
    measurements.error(value, value)  # finite/shape validation
    return value


def capture(root):
    result = {}
    for i in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(i)
        path = occurrence.fullPathName
        if path in result:
            raise ValueError("Ambiguous occurrence path: " + path)
        result[path] = pose(occurrence.transform2)
    return result


def check_angles(servos, originals):
    excursions = []
    for joint, original in zip(servos, originals):
        motion = adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion)
        value = motion.rotationValue
        if not math.isfinite(value) or not math.isfinite(original):
            raise ValueError("Non-finite servo angle: " + joint.name)
        delta = math.degrees(math.atan2(math.sin(value - original), math.cos(value - original)))
        if abs(delta) > 10:
            raise ValueError("Servo exceeded 10-degree diagnostic excursion: " + joint.name)
        limits = motion.rotationLimits
        if limits and ((limits.isMinimumValueEnabled and value < limits.minimumValue) or
                       (limits.isMaximumValueEnabled and value > limits.maximumValue)):
            raise ValueError("Servo exceeded an enabled Fusion joint limit: " + joint.name)
        excursions.append(delta)
    return excursions


def experiment(root, top, base, attachments, servos, pump, pause, record, hold):
    """Six bounded absolute heave steps; caller supplies UI/logging callbacks."""
    baseline = capture(root)
    initial = top.transform2.copy()
    bodies = [(j.name, j.occurrenceOne.fullPathName, j.occurrenceTwo.fullPathName) for j in servos]
    for _, one, two in bodies:
        if one not in baseline or two not in baseline:
            raise ValueError("Servo component is not in the root-context snapshot")
    originals = [adsk.fusion.RevoluteJointMotion.cast(j.jointMotion).rotationValue for j in servos]
    check_angles(servos, originals)
    record("baseline", dict(poses=baseline, servo_angles=originals, servo_bodies=bodies))
    phase = "forward"
    def observe(state):
        record(state, dict(phase=phase, poses=capture(root),
                           servo_angles=[adsk.fusion.RevoluteJointMotion.cast(j.jointMotion).rotationValue
                                         for j in servos]))
        check_angles(servos, originals)
    attempted, flags_intact = False, True
    result = {}
    summary = {}
    try:
        for step in range(1, 7):
            target = initial.copy()
            if not target.setCell(2, 3, initial.getCell(2, 3) + 0.1 * step / 6):
                raise ValueError("Could not construct heave target")
            attempted = True
            driver.reconnect(top, attachments, target, pump, observe)
            summary = measurements.summarize(baseline, capture(root), top.fullPathName,
                                             base.fullPathName, bodies, pose(target))
            record("step", dict(step=step, **summary))
            if summary["base_mm"] > 0.05 or summary["base_deg"] > 0.02:
                raise ValueError("Selected base moved; reconnect experiment stopped")
            if summary["top_error_mm"] > 0.1 or summary["top_error_deg"] > 0.05:
                raise ValueError("Platform did not reach the requested pose")
            if summary["articulated_count"] != 6:
                raise ValueError("Independent servo-body articulation not detected at all six joints")
            if any(s["relative_deg"] > 10 for s in summary["servos"]):
                raise ValueError("Servo component rotation exceeded 10-degree diagnostic excursion")
            pause(0.5)
        hold(summary)
    except driver.StateRestoreError:
        flags_intact = False
        raise
    finally:
        if attempted and flags_intact:
            phase = "restoration"
            driver.reconnect(top, attachments, initial, pump, observe)
            after = capture(root)
            residuals = {name: measurements.error(value, after[name]) for name, value in baseline.items()}
            angle_residuals = check_angles(servos, originals)
            result = dict(restored=all(mm <= 0.1 and deg <= 0.05 for mm, deg in residuals.values()) and
                          all(abs(value) <= 0.05 for value in angle_residuals),
                          occurrence_residuals=residuals, joint_residuals_deg=angle_residuals)
            record("restoration", result)
        elif attempted:
            record("restoration_skipped", dict(reason="Constraint cleanup failed; DO NOT SAVE"))
    return result


def inspect_joints(root):
    """Read each endpoint once; one bad native getter must not erase the audit."""
    inspected = []
    for family, joints in (("joint", root.allJoints), ("as_built", root.allAsBuiltJoints)):
        for index, joint in enumerate(joints):
            errors = {}
            info = dict(family=family, index=index, name=family + ":" + str(index),
                        one=None, two=None, suppressed=None, errors=errors)
            for attribute, key in (("name", "name"), ("isSuppressed", "suppressed"),
                                   ("occurrenceOne", "one"), ("occurrenceTwo", "two")):
                try:
                    value = getattr(joint, attribute)
                    if key in ("one", "two"):
                        value = value.fullPathName if value is not None else None
                    info[key] = value
                except Exception as exc:
                    errors[attribute] = str(exc)
            inspected.append((joint, info))
    return inspected


def audit(root):
    """Compare proxy/native reads only; native paths are NOT root replacements."""
    inspected = inspect_joints(root)
    for joint, info in inspected:
        metadata_errors = {}
        info["metadata_errors"] = metadata_errors
        for attribute in ("isValid", "assemblyContext", "parentComponent", "timelineObject"):
            try:
                value = getattr(joint, attribute)
                if attribute == "assemblyContext":
                    value = value.fullPathName if value is not None else None
                elif attribute == "parentComponent":
                    value = value.name if value is not None else None
                elif attribute == "timelineObject":
                    value = dict(index=value.index, name=value.name) if value is not None else None
                info[attribute] = value
            except Exception as exc:
                metadata_errors[attribute] = str(exc)
        try:
            native = joint.nativeObject
            info["already_native"] = native is None
            if native is not None:
                native_info: dict = {"errors": {}}
                for attribute in ("occurrenceOne", "occurrenceTwo"):
                    try:
                        value = getattr(native, attribute)
                        native_info[attribute] = value.fullPathName if value is not None else None
                    except Exception as exc:
                        native_info["errors"][attribute] = str(exc)
                info["native"] = native_info
        except Exception as exc:
            metadata_errors["nativeObject"] = str(exc)
    return dict(joints=[info for _, info in inspected], occurrence_paths=[
        root.allOccurrences.item(i).fullPathName for i in range(root.allOccurrences.count)])


def inventory(root, top, inspected=None):
    if top.isGrounded or top.isGroundToParent:
        raise ValueError("Top platform is already grounded; no changes made")
    inspected = inspect_joints(root) if inspected is None else inspected
    unreadable = [info for _, info in inspected if info["errors"]]
    if unreadable:
        raise ValueError("Joint inventory is unreadable; no model changes made: " + "; ".join(
            "{} [{}:{}]: {}".format(info["name"], info["family"], info["index"],
                                    ", ".join(info["errors"])) for info in unreadable))
    joints = [joint for joint, _ in inspected]
    attachments = [joint for joint, info in inspected
                   if top.fullPathName in (info["one"], info["two"])]
    servos = [j for j in joints if adsk.fusion.RevoluteJointMotion.cast(j.jointMotion)]
    if sum(not j.isSuppressed for j in attachments) != 6:
        raise ValueError("Expected six active platform attachment joints; no changes made")
    if len(servos) != 6 or any(j.isSuppressed or j.isLocked for j in servos):
        raise ValueError("Expected six active unlocked servo revolutes; no changes made")
    return attachments, servos


def run(context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    answer, cancelled = ui.inputBox(
        "AUDIT is read-only: compare assembly-context and native joint endpoints. "
        "No selections, motion, grounding, suppression, or saves. Use AUDIT for the current blocker.\n\n"
        "RUN: DISPOSABLE COPY ONLY. This experiment suppresses the six platform attachment joints, "
        "temporarily grounds the top, and reconnects the joints at each step. "
        "It attempts six steps to +1 mm world Z, then restoration. Restoration may fail. "
        "It does NOT save. Close the copy without saving afterward.\n\n"
        "Type RUN to authorize this experiment on the active disposable copy.",
        "Platform solver investigation", "AUDIT")
    if cancelled or answer.strip() not in ("RUN", "AUDIT"):
        return
    report = HERE / "reports" / ("probe-" + uuid.uuid4().hex + ".jsonl")
    stream = None
    def record(event, data):
        if stream is not None:
            stream.write(json.dumps(dict(event=event, monotonic=time.monotonic(), data=data),
                                    allow_nan=False) + "\n")
            stream.flush()
            os.fsync(stream.fileno())
    try:
        report.parent.mkdir(exist_ok=True)
        stream = report.open("x", encoding="utf-8")
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            raise ValueError("Activate a Fusion design first")
        root = design.rootComponent
        record("start", dict(fusion_version=app.version, document=app.activeDocument.name,
                             experiment="read-only reference audit" if answer.strip() == "AUDIT"
                             else "six-step +1 mm world-Z reconnect; no CSV"))
        if answer.strip() == "AUDIT":
            record("audit", audit(root))
            record("complete", dict(mode="AUDIT", model_changes=False))
            ui.messageBox("Read-only audit complete. No model changes.\n"
                          "Endpoint errors are retained in the report; this is not a motion test.\nReport: " + str(report))
            return
        occurrences = {root.allOccurrences.item(i).fullPathName: root.allOccurrences.item(i)
                       for i in range(root.allOccurrences.count)}
        def select(prompt):
            selected = ui.selectEntity(prompt, "Occurrences").entity
            if selected.fullPathName not in occurrences:
                raise ValueError("Selection is not a root-context occurrence")
            return occurrences[selected.fullPathName]
        top = select("Select the moving top platform COMPONENT (use the Browser)")
        base = select("Select the stationary base COMPONENT (not the whole assembly)")
        if top.fullPathName == base.fullPathName or top.fullPathName.startswith(base.fullPathName + "+"):
            raise ValueError("Base must be a separate stationary component, not the top or its parent")
        inspected = inspect_joints(root)
        record("inventory", dict(top=top.fullPathName, base=base.fullPathName,
                                 joints=[info for _, info in inspected]))
        attachments, servos = inventory(root, top, inspected)
        def pump():
            adsk.doEvents()
            app.activeViewport.refresh()
        def hold(summary):
            ui.messageBox(
                "Measured +1 mm heave candidate, NOT validated playback.\n"
                "Six servo-body relative rotations detected.\n"
                "Top error: {:.4f} mm / {:.4f} deg. Base: {:.4f} mm / {:.4f} deg.\n\n"
                "Inspect ALL SIX rods/cranks: connected at both ends? Base stationary?\n"
                "OK attempts restoration. No automatic joint-closure proof is claimed.\n\nReport: {}".format(
                    summary["top_error_mm"], summary["top_error_deg"], summary["base_mm"],
                    summary["base_deg"], report))
        result = experiment(root, top, base, attachments, servos, pump, time.sleep, record, hold)
        record("complete", result)
        ui.messageBox(("Starting component poses and angles restored within diagnostic tolerance."
                       if result["restored"] else "WARNING: restoration differs from start. DO NOT SAVE.") +
                      "\nClose this disposable copy WITHOUT SAVING.\nReport: " + str(report))
    except Exception:
        details = traceback.format_exc()
        try:
            record("error", dict(traceback=details, instruction="DO NOT SAVE"))
        except Exception:
            pass  # Preserve the original error even if report storage has failed.
        ui.messageBox("Investigation stopped. DO NOT SAVE. Close the disposable copy without saving.\n\n" +
                      details + "\nReport (if created): " + str(report))
    finally:
        if stream is not None:
            stream.close()
