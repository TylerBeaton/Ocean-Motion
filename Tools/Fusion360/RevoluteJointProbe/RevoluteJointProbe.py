"""One-joint Fusion probe: test a root-context revolute motion, then restore it."""

import math
import traceback

import adsk.core
import adsk.fusion


def _revolute_candidates(root):
    candidates = []
    for joint in root.allJoints:
        motion = adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion)
        if motion:
            candidates.append((joint, motion))
    return candidates


def _native_revolute_paths(root):
    """List native revolute joints in all component occurrences."""
    paths = []
    for index in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(index)
        for joint in occurrence.component.joints:
            if adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion):
                paths.append("{}/{}".format(occurrence.fullPathName, joint.name))
    return paths

def _snapshot(root):
    poses = {}
    for index in range(root.allOccurrences.count):
        occurrence = root.allOccurrences.item(index)
        matrix = occurrence.transform2
        poses[occurrence.fullPathName] = tuple(
            matrix.getCell(row, col) for row in range(3) for col in range(4)
        )
    return poses


def run(_context):
    app = adsk.core.Application.get()
    ui = app.userInterface
    motion = None
    original_angle = None
    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            ui.messageBox("Open the duplicated Fusion assembly first.")
            return
        root = design.rootComponent
        candidates = _revolute_candidates(root)
        nested_paths = _native_revolute_paths(root)
        if not candidates:
            ui.messageBox("No root-context revolute joints found. No changes made.")
            return

        if len(candidates) == 1:
            index = 0
        else:
            choices = "\n".join(
                "{}: {} ({})".format(i, joint.name,
                    joint.assemblyContext.fullPathName if joint.assemblyContext else "root")
                for i, (joint, _) in enumerate(candidates)
            )
            answer, cancelled = ui.inputBox(
                "Choose a servo revolute joint by number:\n" + choices,
                "One-Joint Probe", "0",
            )
            if cancelled:
                return
            index = int(answer)
            if index < 0 or index >= len(candidates):
                raise ValueError("Joint index is outside the list")

        joint, motion = candidates[index]
        answer, cancelled = ui.inputBox(
            "Temporary change in degrees (between -10 and +10). Try -5 first.",
            "One-Joint Probe", "-5",
        )
        if cancelled:
            return
        delta_degrees = float(answer)
        if not math.isfinite(delta_degrees) or not 0 < abs(delta_degrees) <= 10:
            raise ValueError("Choose a nonzero finite change of at most 10 degrees")

        original_angle = motion.rotationValue
        target_angle = original_angle + math.radians(delta_degrees)
        limits = motion.rotationLimits
        if limits:
            if limits.isMinimumValueEnabled and target_angle < limits.minimumValue:
                raise ValueError("Requested angle is below the joint minimum")
            if limits.isMaximumValueEnabled and target_angle > limits.maximumValue:
                raise ValueError("Requested angle is above the joint maximum")

        before = _snapshot(root)
        motion.rotationValue = target_angle
        adsk.doEvents()
        app.activeViewport.refresh()
        after = _snapshot(root)
        moved = [
            name for name, values in after.items()
            if name in before and any(abs(a - b) > 1e-6 for a, b in zip(before[name], values))
        ]
        context = joint.assemblyContext.fullPathName if joint.assemblyContext else "root"
        message = (
            "Joint: {} ({})\n"
            "Requested change: {} degrees\n"
            "Rotation before/after (radians): {:.6f} / {:.6f}\n"
            "Tracked occurrences that changed: {}\n"
            "Examples: {}\n\n"
            "Root-context revolute joints: {}\n"
            "Revolute joints through component occurrences: {}\n"
            "Nested examples: {}\n\n"
            "Inspect crank, rods, and platform before pressing OK. "
            "The original joint angle will then be restored."
        ).format(joint.name, context, delta_degrees, original_angle,
                 motion.rotationValue, len(moved), ", ".join(moved[:8]) or "none",
                 len(candidates), len(nested_paths), ", ".join(nested_paths[:12]) or "none")
        ui.messageBox(message)
    except Exception:
        ui.messageBox("One-joint probe failed:\n{}".format(traceback.format_exc()))
    finally:
        if motion is not None and original_angle is not None:
            motion.rotationValue = original_angle
            adsk.doEvents()
            app.activeViewport.refresh()


def stop(_context):
    pass
