"""Standard-library 6DOF numerics. Pose: 3x4, cm; residual: mm, degrees."""
import csv
import math


def norm(v):
    return math.sqrt(sum(x*x for x in v))


def linear(a, b, threshold=1e-10):
    n = len(b)
    m = [list(row) + [v] for row,v in zip(a,b)]
    scale = max(abs(x) for row in a for x in row)
    for c in range(n):
        p = max(range(c,n), key=lambda r: abs(m[r][c]))
        m[c],m[p] = m[p],m[c]
        if abs(m[c][c]) <= max(1e-12, scale*threshold):
            raise ValueError('singular or unresponsive full-six-DOF Jacobian')
        for r in range(c+1,n):
            f = m[r][c]/m[c][c]
            for k in range(c,n+1): m[r][k] -= f*m[c][k]
    x = [0.0]*n
    for r in range(n-1,-1,-1):
        x[r] = (m[r][n]-sum(m[r][c]*x[c] for c in range(r+1,n)))/m[r][r]
    return x


def transpose(r):
    return [list(x) for x in zip(*r)]


def mul(a,b):
    return [[sum(a[r][k]*b[k][c] for k in range(3)) for c in range(3)] for r in range(3)]


def rotation(p):
    return [row[:3] for row in p]


def rotvec(r):
    angle = math.acos(max(-1.,min(1.,(sum(r[i][i] for i in range(3))-1)/2)))
    if angle < 1e-8: return [0.,0.,0.]
    if math.pi-angle < 1e-5: raise ValueError('Ambiguous near-180-degree rotation')
    f = math.degrees(angle)/(2*math.sin(angle))
    return [f*(r[2][1]-r[1][2]), f*(r[0][2]-r[2][0]), f*(r[1][0]-r[0][1])]


def exp(v):
    a = math.radians(norm(v))
    if a < 1e-12: return [[1.,0.,0.],[0.,1.,0.],[0.,0.,1.]]
    x,y,z = [t/norm(v) for t in v]
    k = [[0,-z,y],[z,0,-x],[-y,x,0]]
    kk = mul(k,k)
    return [[float(r==c)+math.sin(a)*k[r][c]+(1-math.cos(a))*kk[r][c]
             for c in range(3)] for r in range(3)]


def residual(target, actual):
    if not all(math.isfinite(x) for p in (target,actual) for row in p for x in row):
        raise ValueError('Non-finite pose')
    return [(target[i][3]-actual[i][3])*10 for i in range(3)] + rotvec(mul(rotation(target),transpose(rotation(actual))))


def relative(a,b):
    rt = transpose(rotation(a))
    rr = mul(rt,rotation(b))
    return [rr[r]+[sum(rt[r][k]*(b[k][3]-a[k][3]) for k in range(3))] for r in range(3)]


def compose(p, delta_cm, local_degrees):
    rr = mul(rotation(p),exp(local_degrees))
    return [rr[r]+[p[r][3]+delta_cm[r]] for r in range(3)]


def envelope(neutral,p):
    e = residual(p,neutral)
    if norm(e[:3])>5.000001 or norm(e[3:])>3.000001:
        raise ValueError('Plate envelope exceeds 5 mm / 3 degrees')


def qmatrix(q):
    n = norm(q)
    if not .5 < n < 1.5: raise ValueError('Invalid quaternion norm')
    x,y,z,w = [v/n for v in q]
    return [[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
            [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
            [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]


def hold_heading(neutral,target):
    # Preserve world-Z yaw (heading of local X projected into world XY), including
    # when the calibrated plate is already tilted. Refuse the vertical singularity.
    for p in (neutral,target):
        if math.hypot(p[0][0],p[1][0])<.1: raise ValueError('World yaw projection singular')
    delta=math.atan2(neutral[1][0],neutral[0][0])-math.atan2(target[1][0],target[0][0])
    rr=mul(exp([0,0,math.degrees(delta)]),rotation(target))
    return [rr[r]+[target[r][3]] for r in range(3)]


def target_pose(neutral, first, row):
    # Local calibrated relative quaternion; C is a reflection (Y/Z swap).
    c = [[1,0,0],[0,0,1],[0,1,0]]
    rel = mul(transpose(qmatrix(first['q'])), qmatrix(row['q']))
    v = rotvec(mul(mul(c,rel),c))
    target = hold_heading(neutral,compose(neutral,[0,0,(row['y']-first['y'])*100*.005], [v[0]*.08,v[1]*.08,0]))
    envelope(neutral,target)
    return target


def load_csv(path):
    rows=[]
    fields='schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w'.split(',')
    with open(path,newline='',encoding='utf-8-sig') as f:
        reader=csv.DictReader(f)
        if reader.fieldnames != fields: raise ValueError('Unexpected CSV schema')
        lastseq=-1
        for raw in reader:
            values={k:float(raw[k]) for k in fields}
            if not all(map(math.isfinite,values.values())): raise ValueError('Non-finite CSV')
            if values['schema_version']!=1 or not values['sequence'].is_integer() or values['sequence']<=lastseq:
                raise ValueError('Invalid schema/sequence')
            t=values['time_seconds']
            if rows and t<=rows[-1]['time']: raise ValueError('Non-increasing CSV time')
            q=[values[k] for k in ('rot_x','rot_y','rot_z','rot_w')]
            qmatrix(q)
            rows.append(dict(time=t,y=values['pos_y_m'],q=q,sequence=int(values['sequence'])))
            lastseq=values['sequence']
    if len(rows)<2: raise ValueError('Need at least two CSV rows')
    return rows


def dls(j, error, damping=.05):
    if len(j)!=6 or any(len(row)!=6 for row in j) or len(error)!=6:
        raise ValueError('Expected six-by-six Jacobian and six residuals')
    if damping <= 0 or not all(math.isfinite(x) for row in j for x in row) or not all(map(math.isfinite,error)):
        raise ValueError('Invalid solver input')
    # Rank gate BEFORE damping: regularization must not fabricate observability.
    linear(j, [0.0]*6, threshold=1e-5)
    normal = [[sum(j[k][r]*j[k][c] for k in range(6)) +
               (damping*damping if r==c else 0) for c in range(6)] for r in range(6)]
    return linear(normal, [sum(j[k][r]*error[k] for k in range(6)) for r in range(6)])
