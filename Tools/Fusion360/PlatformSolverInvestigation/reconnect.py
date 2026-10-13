"""Experimental community workaround; no Fusion imports or claimed IK guarantee."""


class StateRestoreError(RuntimeError):
    """Stop further CAD mutations when original constraint flags cannot return."""


def reconnect(top, joints, target, pump, observe):
    """Reattach constraints with the end platform pinned, then release it.

    Caller must use a disposable document, explicitly authorize constraint
    toggling, capture geometry before/after, and restore the starting pose.
    """
    originals = [(joint, joint.isSuppressed) for joint in joints]
    grounded = top.isGrounded
    try:
        for joint, suppressed in originals:
            if not suppressed:
                joint.isSuppressed = True
        top.transform2 = target
        top.isGrounded = True
        for joint, suppressed in originals:
            if joint.isSuppressed != suppressed:
                joint.isSuppressed = suppressed
        pump()
        observe("grounded")
    finally:
        errors = []
        for joint, suppressed in originals:
            try:
                if joint.isSuppressed != suppressed:
                    joint.isSuppressed = suppressed
                if joint.isSuppressed != suppressed:
                    raise RuntimeError("Suppression readback differs")
            except Exception as exc:
                errors.append("{}: {}".format(joint.name, exc))
        try:
            if top.isGrounded != grounded:
                top.isGrounded = grounded
            if top.isGrounded != grounded:
                raise RuntimeError("Grounding readback differs")
        except Exception as exc:
            errors.append("platform grounding: {}".format(exc))
        if errors:
            raise StateRestoreError("DO NOT SAVE: constraint cleanup failed: " + "; ".join(errors))
    pump()
    observe("released")
