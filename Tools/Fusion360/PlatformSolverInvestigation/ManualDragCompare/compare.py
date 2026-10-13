"""Read-only pose comparison. Fusion matrices use centimetres; angles radians."""
import math


def relative(a, b):
    return [[sum(a[k][r] * b[k][c] for k in range(3)) for c in range(3)] +
            [sum(a[k][r] * (b[k][3] - a[k][3]) for k in range(3))]
            for r in range(3)] + [[0, 0, 0, 1]]


def delta(a, b):
    rotation = relative(a, b)
    cosine = (sum(rotation[i][i] for i in range(3)) - 1) / 2
    return {'translation_mm': math.sqrt(sum((b[r][3] - a[r][3]) ** 2 for r in range(3))) * 10,
            'rotation_deg': math.degrees(math.acos(max(-1, min(1, cosine))))}


def validate_pose(pose):
    if not isinstance(pose, (list, tuple)) or len(pose) != 4 or any(len(row) != 4 for row in pose):
        raise ValueError('Missing or malformed 4x4 matrix')
    if not all(isinstance(v, (int, float)) and math.isfinite(v) for row in pose for v in row):
        raise ValueError('Nonfinite matrix')
    if any(abs(pose[3][i] - (1 if i == 3 else 0)) > 1e-7 for i in range(4)):
        raise ValueError('Non-affine matrix')
    for i in range(3):
        for j in range(3):
            if abs(sum(pose[k][i] * pose[k][j] for k in range(3)) - (i == j)) > 1e-5:
                raise ValueError('Non-rigid matrix')


def validate_snapshot(snapshot):
    try:
        validate_pose(snapshot['top'])
        validate_pose(snapshot['base'])
        if len(snapshot['servos']) != 6:
            raise ValueError('Exactly six servos required')
        for servo in snapshot['servos']:
            validate_pose(servo['one'])
            validate_pose(servo['two'])
            if servo['angle_rad'] is not None and not math.isfinite(servo['angle_rad']):
                raise ValueError('Nonfinite angle')
    except (KeyError, TypeError) as exc:
        raise ValueError('Missing component or measurement: ' + str(exc)) from exc


def compare(before, after):
    validate_snapshot(before)
    validate_snapshot(after)
    for a, b in zip(before['servos'], after['servos']):
        if any(a[key] != b[key] for key in ('name', 'one_path', 'two_path')):
            raise ValueError('Servo pair identity changed')
    servos = []
    for a, b in zip(before['servos'], after['servos']):
        change = delta(relative(a['one'], a['two']), relative(b['one'], b['two']))
        angle_delta = None if a['angle_rad'] is None or b['angle_rad'] is None else b['angle_rad'] - a['angle_rad']
        servos.append(dict(name=a['name'], one_path=a['one_path'], two_path=a['two_path'],
                           angle_delta_rad=angle_delta, **change))
    return {'top_world': delta(before['top'], after['top']),
            'base_world': delta(before['base'], after['base']),
            'top_relative_to_base': delta(relative(before['base'], before['top']),
                                          relative(after['base'], after['top'])),
            'servos': servos,
            'articulated_count': sum(s['rotation_deg'] > 0.001 for s in servos)}
