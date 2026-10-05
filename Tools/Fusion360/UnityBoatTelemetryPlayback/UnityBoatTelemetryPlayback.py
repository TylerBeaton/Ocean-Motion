"""Fusion 360 script: play Unity boat telemetry on a component occurrence."""

import csv
import os
import sys
import time
import traceback

import adsk.core
import adsk.fusion

SCRIPT_DIR = os.path.dirname(os.path.realpath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from telemetry_math import fusion_delta_from_unity_pose, parse_telemetry_rows

# Configuration. An empty occurrence name prompts when the script runs.
TARGET_OCCURRENCE_NAME = ""
TRANSLATION_SCALE = 1.0
PLAYBACK_SPEED = 1.0
FUSION_CENTIMETERS_PER_UNITY_METER = 100.0
LOOP_PLAYBACK = False
RESTORE_NEUTRAL_AFTER_PLAYBACK = True
WRITE_JOINT_RANGE_REPORT = True


def _find_occurrence(root, name):
    exact = None
    component_match = None
    for index in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(index)
        if occurrence.name == name or occurrence.fullPathName == name:
            exact = occurrence
            break
        if occurrence.component and occurrence.component.name == name:
            component_match = occurrence
    return exact or component_match


def _compose_from_neutral(neutral, delta_rotation, delta_translation):
    result = adsk.core.Matrix3D.create()
    for row in range(3):
        for column in range(3):
            value = sum(neutral.getCell(row, k) * delta_rotation[k][column] for k in range(3))
            result.setCell(row, column, value)

    for row in range(3):
        value = neutral.getCell(row, 3)
        value += sum(neutral.getCell(row, k) * delta_translation[k] for k in range(3))
        result.setCell(row, 3, value)
    result.setCell(3, 3, 1.0)
    return result


def _capture_joint_ranges(root, ranges):
    joints = root.allJoints
    for index in range(joints.count):
        joint = joints.item(index)
        motion = joint.jointMotion
        if not motion:
            continue
        for attribute, unit in (("slideValue", "cm"), ("rotationValue", "rad")):
            try:
                value = float(getattr(motion, attribute))
            except (AttributeError, RuntimeError, TypeError, ValueError):
                continue
            key = (joint.name, attribute, unit)
            if key not in ranges:
                ranges[key] = [value, value]
            else:
                ranges[key][0] = min(ranges[key][0], value)
                ranges[key][1] = max(ranges[key][1], value)


def _write_joint_report(input_path, ranges):
    report_path = os.path.splitext(input_path)[0] + ".fusion-joint-ranges.csv"
    with open(report_path, "w", newline="", encoding="utf-8") as output:
        writer = csv.writer(output)
        writer.writerow(("joint_name", "measurement", "unit", "minimum", "maximum", "travel"))
        for (name, measurement, unit), values in sorted(ranges.items()):
            writer.writerow((name, measurement, unit, values[0], values[1], values[1] - values[0]))
    return report_path


def _choose_csv(ui):
    dialog = ui.createFileDialog()
    dialog.title = "Choose Unity boat telemetry CSV"
    dialog.filter = "CSV files (*.csv)"
    dialog.isMultiSelectEnabled = False
    if dialog.showOpen() != adsk.core.DialogResults.DialogOK:
        return None
    return dialog.filename


def run(_context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    occurrence = None
    neutral = None

    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            ui.messageBox("Open the Stewart-platform Fusion design before running this script.")
            return

        input_path = _choose_csv(ui)
        if not input_path:
            return
        with open(input_path, "r", newline="", encoding="utf-8-sig") as source:
            frames = parse_telemetry_rows(source)

        occurrence_name = TARGET_OCCURRENCE_NAME
        if not occurrence_name:
            occurrence_name, cancelled = ui.inputBox(
                "Enter the moving top-platform occurrence name (for example Platform:1).",
                "Unity Boat Telemetry Playback",
                "Platform:1",
            )
            if cancelled:
                return

        occurrence = _find_occurrence(design.rootComponent, occurrence_name.strip())
        if not occurrence:
            ui.messageBox("Occurrence not found: {}".format(occurrence_name))
            return
        if occurrence.isGrounded:
            ui.messageBox("{} is grounded. Unground it before playback.".format(occurrence.name))
            return

        neutral = occurrence.transform2.copy()
        joint_ranges = {}

        while True:
            previous_time = frames[0]["time_seconds"]
            for frame in frames:
                delay = max(0.0, frame["time_seconds"] - previous_time) / max(PLAYBACK_SPEED, 1e-6)
                if delay:
                    time.sleep(delay)
                previous_time = frame["time_seconds"]

                rotation, translation = fusion_delta_from_unity_pose(
                    frame["position_m"],
                    frame["quaternion"],
                    FUSION_CENTIMETERS_PER_UNITY_METER,
                    TRANSLATION_SCALE,
                )
                occurrence.transform2 = _compose_from_neutral(neutral, rotation, translation)
                adsk.doEvents()
                app.activeViewport.refresh()
                if WRITE_JOINT_RANGE_REPORT:
                    _capture_joint_ranges(design.rootComponent, joint_ranges)
            if not LOOP_PLAYBACK:
                break

        report_path = None
        if WRITE_JOINT_RANGE_REPORT:
            report_path = _write_joint_report(input_path, joint_ranges)

        message = "Playback complete. Inspect the final pose, then choose OK"
        if RESTORE_NEUTRAL_AFTER_PLAYBACK:
            message += " to restore the neutral pose."
        else:
            message += ". The component will remain at the final pose."
        if report_path:
            message += "\n\nJoint range report:\n{}".format(report_path)
        ui.messageBox(message)

    except Exception:
        ui.messageBox("Playback failed:\n{}".format(traceback.format_exc()))
    finally:
        if RESTORE_NEUTRAL_AFTER_PLAYBACK and occurrence and neutral:
            occurrence.transform2 = neutral
            adsk.doEvents()
            app.activeViewport.refresh()


def stop(_context):
    pass
