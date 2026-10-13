"""Fusion read adapter and live-session guard; no model writes."""
import importlib.util
import math
import json
import uuid
from pathlib import Path


def export_report(folder, payload, phase):
    text = json.dumps(payload, indent=2, allow_nan=False)
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / ('manual-drag-' + phase + '-' + uuid.uuid4().hex + '.json')
    with path.open('x', encoding='utf-8') as stream:
        stream.write(text + '\n')
    return path


def fresh_module(name):
    spec = importlib.util.spec_from_file_location('_manual_drag_' + name, Path(__file__).with_name(name + '.py'))
    if spec is None or spec.loader is None:
        raise RuntimeError('Cannot load comparison module')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


measure = fresh_module('compare')


def read(errors, label, getter):
    try:
        return getter()
    except Exception as exc:
        errors.append({'field': label, 'error': type(exc).__name__ + ': ' + str(exc)})
        return None


def discover(root, revolute_type, errors):
    joints = []
    for index, joint in enumerate(root.allJoints):
        kind = read(errors, 'ordinary joint[%d].jointMotion.jointType' % index,
                    lambda: joint.jointMotion.jointType)
        if kind == revolute_type:
            joints.append(joint)
    if len(joints) != 6:
        raise ValueError('Expected exactly six readable ordinary revolute joints; found %d' % len(joints))
    return joints


def root_occurrences(root):
    result = {}
    for occ in root.allOccurrences:
        path = occ.fullPathName
        if path in result:
            raise ValueError('Ambiguous root-context occurrence path: ' + path)
        result[path] = occ
    return result


def matrix_for(occ, occurrences):
    if occ is None or not occ.isValid:
        raise ValueError('Missing/invalid occurrence')
    path = occ.fullPathName
    if path not in occurrences:
        raise ValueError('Occurrence not present in root context: ' + path)
    root_occ = occurrences[path]
    if not root_occ.isValid or not (root_occ == occ):
        raise ValueError('Invalid or replaced root-context occurrence: ' + path)
    transform = root_occ.transform2
    matrix = [[transform.getCell(r, c) for c in range(4)] for r in range(4)]
    measure.validate_pose(matrix)
    return matrix


def revolute_motion(motion):
    import adsk.fusion
    return adsk.fusion.RevoluteJointMotion.cast(motion)


def angle(joint):
    value = revolute_motion(joint.jointMotion).rotationValue
    if not math.isfinite(value):
        raise ValueError('Nonfinite revolute angle')
    return value


def capture(root, top, base, joints):
    errors = []
    occurrences = read(errors, 'root.allOccurrences', lambda: root_occurrences(root)) or {}
    result = {'top_path': read(errors, 'top.path', lambda: top.fullPathName),
              'base_path': read(errors, 'base.path', lambda: base.fullPathName),
              'top': read(errors, 'top.matrix', lambda: matrix_for(top, occurrences)),
              'base': read(errors, 'base.matrix', lambda: matrix_for(base, occurrences)),
              'servos': [], 'read_errors': errors}
    for index, joint in enumerate(joints):
        label = 'servo[%d]' % index
        name = read(errors, label + '.name', lambda: joint.name)
        one = read(errors, label + '.occurrenceOne', lambda: joint.occurrenceOne)
        two = read(errors, label + '.occurrenceTwo', lambda: joint.occurrenceTwo)
        result['servos'].append({
            'name': name,
            'one_path': read(errors, label + '.one_path', lambda: one.fullPathName),
            'two_path': read(errors, label + '.two_path', lambda: two.fullPathName),
            'one': read(errors, label + '.one.matrix', lambda: matrix_for(one, occurrences)),
            'two': read(errors, label + '.two.matrix', lambda: matrix_for(two, occurrences)),
            'angle_rad': read(errors, label + '.angle_rad', lambda: angle(joint))})
    return result



def check_session(state, active_document, baseline_id):
    if state is None or state.invalidated:
        raise ValueError('No live START baseline: restart/reopen/script-state loss requires START')
    if not state.document.isValid or active_document is None or not active_document.isValid:
        raise ValueError('START document no longer valid; START again')
    # Fusion Document.__eq__ calls native Document___eq__; Python `is` is not used.
    if not (state.document == active_document):
        raise ValueError('Wrong live document; return to the document used for START')
    if state.baseline_id != baseline_id or state.before is None:
        raise ValueError('Wrong or missing START baseline')
