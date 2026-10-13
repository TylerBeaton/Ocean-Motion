"""Standard-library Unity CSV contract; positions in metres, targets in cm."""
import csv
import math

FIELDS = 'schema_version sequence time_seconds pos_x_m pos_y_m pos_z_m rot_x rot_y rot_z rot_w'.split()


def read_csv(stream):
    reader = csv.DictReader(stream)
    if reader.fieldnames != FIELDS:
        raise ValueError('Unexpected Unity boat CSV header')
    rows = []
    for raw in reader:
        if None in raw or any(raw[k] is None for k in FIELDS):
            raise ValueError('Malformed CSV row')
        row = {k: float(raw[k]) for k in FIELDS}
        if not all(math.isfinite(x) for x in row.values()):
            raise ValueError('Non-finite CSV value')
        if row['schema_version'] != 1 or row['sequence'] != int(row['sequence']):
            raise ValueError('Unsupported schema or sequence')
        if rows and (row['time_seconds'] <= rows[-1]['time_seconds'] or row['sequence'] <= rows[-1]['sequence']):
            raise ValueError('CSV time and sequence must increase')
        rows.append(row)
    if len(rows) < 2:
        raise ValueError('Need multiple CSV poses')
    return rows


def frames(rows, interval=.5, duration=None, translation_scale=.005, rotation_scale=.08):
    if not all(math.isfinite(v) and v > 0 for v in (interval, translation_scale, rotation_scale)):
        raise ValueError('Scales and interval must be positive finite')
    if duration is not None and (not math.isfinite(duration) or duration <= 0):
        raise ValueError('Duration must be positive finite')
    neutral = rows[0]
    q0 = quaternion(neutral)
    converted = []
    for row in rows:
        q = multiply((-q0[0], -q0[1], -q0[2], q0[3]), quaternion(row))
        if q[3] < 0:
            q = tuple(-v for v in q)
        n = math.sqrt(sum(v*v for v in q[:3]))
        factor = 2 * math.atan2(n, q[3]) / n if n > 1e-12 else 2
        # C R C with C swapping Y/Z is an improper basis; axial vectors map -C*v.
        rv = (-q[0]*factor*rotation_scale, -q[2]*factor*rotation_scale, 0.)
        f = dict(sequence=int(row['sequence']), time=row['time_seconds']-neutral['time_seconds'],
                 heave_cm=(row['pos_y_m']-neutral['pos_y_m'])*100*translation_scale,
                 rotation_vector=rv)
        validate_frame(f)
        converted.append(f)
    result = []
    candidates = [r for r in converted if duration is None or r['time'] <= duration]
    for row in candidates:
        if not result or row['time'] >= result[-1]['time'] + interval or row is candidates[-1]:
            result.append(row)
    if len(result) < 2:
        raise ValueError('Selection needs multiple CSV frames')
    return result


def quaternion(row):
    q = tuple(row[k] for k in ('rot_x', 'rot_y', 'rot_z', 'rot_w'))
    norm = math.sqrt(sum(v*v for v in q))
    if not math.isfinite(norm) or abs(norm-1) > .01:
        raise ValueError('CSV quaternion must be near unit length')
    return tuple(v/norm for v in q)


def multiply(a, b):
    x,y,z,w = a
    X,Y,Z,W = b
    return (w*X+x*W+y*Z-z*Y, w*Y-x*Z+y*W+z*X,
            w*Z+x*Y-y*X+z*W, w*W-x*X-y*Y-z*Z)


def validate_frame(frame):
    values = (frame['heave_cm'], *frame['rotation_vector'])
    if not all(math.isfinite(v) for v in values):
        raise ValueError('Non-finite requested pose')
    if abs(values[0]) > .5 or math.sqrt(sum(v*v for v in values[1:])) > math.radians(3):
        raise ValueError('Requested pose exceeds 5 mm / 3 degree plate bound')


def target(baseline, frame):
    validate_frame(frame)
    v = frame['rotation_vector']
    angle = math.sqrt(sum(x*x for x in v))
    x,y,z = tuple(a/angle for a in v) if angle > 1e-12 else (1.,0.,0.)
    c,s,d = math.cos(angle), math.sin(angle), 1-math.cos(angle)
    r = ((c+x*x*d,x*y*d-z*s,x*z*d+y*s),
         (y*x*d+z*s,c+y*y*d,y*z*d-x*s),
         (z*x*d-y*s,z*y*d+x*s,c+z*z*d))
    return tuple(tuple(sum(baseline[i][k]*r[k][j] for k in range(3)) for j in range(3)) +
                 (baseline[i][3] + (frame['heave_cm'] if i == 2 else 0),) for i in range(3))

