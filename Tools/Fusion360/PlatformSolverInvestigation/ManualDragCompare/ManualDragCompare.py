"""Two-run read-only Fusion manual-drag recorder. No model mutation APIs."""
import importlib.util
from datetime import datetime, timezone
from pathlib import Path
import sys
import traceback
import types
import uuid

HOME = Path(__file__).resolve().parent
REPORT_DIR = HOME / 'reports'
STATE_KEY = '_ocean_motion_manual_drag_compare_live_v1'


def helper():
    spec = importlib.util.spec_from_file_location('_drag_fresh_recorder', HOME / 'recorder.py')
    if spec is None or spec.loader is None:
        raise RuntimeError('Cannot load recorder.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def release(app, state):
    if state is not None and getattr(state, 'handler', None) is not None:
        if not app.documentClosing.remove(state.handler):
            raise RuntimeError('Could not remove document-closing observer')
        state.handler = None


def run(context):
    import adsk.core
    import adsk.fusion
    app = adsk.core.Application.get()
    ui = app.userInterface
    rec = helper()
    state = sys.modules.get(STATE_KEY)
    report = {'schema': 1, 'phase': 'UNSET', 'status': 'REJECTED',
              'timestamp_utc': datetime.now(timezone.utc).isoformat(),
              'read_errors': [], 'units': {'matrix_translation': 'cm', 'angles': 'rad',
                                          'comparison_translation': 'mm', 'comparison_rotation': 'deg'},
              'rotation_threshold_deg': 0.001,
              'scope': 'Read-only observations; not a solver-health or kinematic-correctness certification'}
    try:
        action, cancelled = ui.inputBox(
            'START records current poses and RETURNS. Dismiss its message, manually drag, then run FINISH.\n'
            'START replaces any prior baseline; FINISH requires the same live document.\nEnter START or FINISH:',
            'ManualDragCompare', 'FINISH' if state is not None and not state.invalidated else 'START')
        if cancelled:
            return
        action = action.strip().upper()
        report['phase'] = action
        if action not in ('START', 'FINISH'):
            raise ValueError('Enter START or FINISH')
        doc = app.activeDocument
        design = adsk.fusion.Design.cast(app.activeProduct)
        if doc is None or design is None:
            raise ValueError('An active Fusion Design document is required')
        root = design.rootComponent
        if not (design.activeComponent == root):
            raise ValueError('Activate the root component manually, then rerun; script will not change activation')
        report['document_name'] = rec.read(report['read_errors'], 'document.name', lambda: doc.name)
        if action == 'START':
            # Explicit START discards the old *recorder* state, never the document pose.
            release(app, state)
            sys.modules.pop(STATE_KEY, None)
            state = None
            top = adsk.fusion.Occurrence.cast(ui.selectEntity('Select TOP component occurrence', 'Occurrences').entity)
            base = adsk.fusion.Occurrence.cast(ui.selectEntity('Select BASE component occurrence', 'Occurrences').entity)
            if top is None or base is None or top == base:
                raise ValueError('Select two distinct component occurrences (top and base)')
            joints = rec.discover(root, adsk.fusion.JointTypes.RevoluteJointType, report['read_errors'])
            before = rec.capture(root, top, base, joints)
            report['before'] = before
            report['read_errors'].extend(before['read_errors'])
            rec.measure.validate_snapshot(before)
            pairs = [(j.occurrenceOne, j.occurrenceTwo) for j in joints]
            if any(a is None or b is None for a, b in pairs):
                raise ValueError('Missing servo endpoint')
            report['baseline_id'] = uuid.uuid4().hex
            state = types.ModuleType(STATE_KEY)
            state.document, state.root = doc, root
            state.top, state.base, state.joints, state.pairs = top, base, joints, pairs
            state.before, state.baseline_id = before, report['baseline_id']
            state.invalidated = False
            state.report = report
            state.handler = None

            class ClosingObserver(adsk.core.DocumentEventHandler):
                def __init__(self):
                    super().__init__()
                def notify(self, args):
                    # Conservative: ANY document-closing event invalidates this baseline,
                    # even a cancelled close. No reliance on invalid post-close wrappers.
                    state.invalidated = True

            state.handler = ClosingObserver()
            if not app.documentClosing.add(state.handler):
                raise RuntimeError('Could not register close invalidation; baseline refused')
            report['status'] = 'START_WITH_READ_ERRORS' if report['read_errors'] else 'START_RECORDED'
            path = rec.export_report(REPORT_DIR, report, 'start')
            state.start_path = str(path)
            sys.modules[STATE_KEY] = state
            ui.messageBox('START recorded. Dismiss this dialog; the script then returns.\n'
                          'Only AFTER it returns, manually drag the top and rerun FINISH.\n'
                          'Do not close/reopen, edit constraints, or rebuild the model.\n'
                          'Read errors: %d (see report).\n%s' % (len(report['read_errors']), path))
            return

        expected = state.report['baseline_id'] if state is not None else None
        rec.check_session(state, doc, expected)
        report['baseline_id'] = state.baseline_id
        report['start_report'] = state.start_path
        report['before'] = state.before
        report['read_errors'].extend(state.report['read_errors'])
        if not (state.root == root):
            raise ValueError('Root design identity changed; START again')
        for joint, (one, two) in zip(state.joints, state.pairs):
            if not joint.isValid or not one.isValid or not two.isValid:
                raise ValueError('A recorded joint or endpoint is no longer valid')
            if not (joint.occurrenceOne == one and joint.occurrenceTwo == two):
                raise ValueError('A recorded servo body pair changed; START again')
        after = rec.capture(root, state.top, state.base, state.joints)
        report['after'] = after
        report['read_errors'].extend(after['read_errors'])
        if any(state.before[key] != after[key] for key in ('top_path', 'base_path')):
            raise ValueError('Selected component identity/path changed')
        report['comparison'] = rec.measure.compare(state.before, after)
        report['status'] = 'MEASURED_WITH_READ_ERRORS' if report['read_errors'] else 'MEASURED'
        path = rec.export_report(REPORT_DIR, report, 'finish')
        state.invalidated = True
        release(app, state)
        ui.messageBox('FINISH: %s\nRelative servo rotations above 0.001 degrees: %d/6.\n'
                      'This is motion evidence, not solver validation. Read errors: %d.\n%s' %
                      (report['status'], report['comparison']['articulated_count'], len(report['read_errors']), path))
    except Exception as exc:
        report['status'] = 'REJECTED'
        report['error'] = type(exc).__name__ + ': ' + str(exc)
        report['traceback'] = traceback.format_exc()
        # A START that failed after handler registration must not leave a usable baseline.
        if report['phase'] == 'START' and state is not None:
            state.invalidated = True
            try:
                release(app, state)
            except Exception as cleanup_error:
                report['cleanup_error'] = str(cleanup_error)
        try:
            path = rec.export_report(REPORT_DIR, report, 'rejected')
            detail = str(path)
        except Exception as export_error:
            detail = 'REPORT WRITE FAILED: ' + str(export_error)
        ui.messageBox('ManualDragCompare REJECTED: ' + report['error'] + '\n' + detail)
