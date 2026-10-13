"""Experimental CSV animation in Fusion; disposable copy only; NEVER saves."""
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
CSV = HERE.parent.parent / 'UnityBoatTelemetryPlayback' / 'recordings' / 'boat-telemetry-test-000-20261005-140749.csv'
CONSENT = 'EXPERIMENT ON DISPOSABLE COPY'
WARNING = ('PARTIAL KNOWN-ATTACHMENT EXPERIMENT, NOT A VALIDATED CONSTRAINT GRAPH. '
           'Only six verified Nub-to-Top rigid as-built attachments are toggled. '
           'Other joints (including unreadable Rigid 4/10 and Rigid 7 proxies) stay untouched and may constrain motion. '
           'Restoration can fail. No save, hardware output, joint deletion/redefinition/locks, or timeline rolls. '
           'Close the disposable copy WITHOUT SAVING afterward.')


def fresh(path):
    spec = importlib.util.spec_from_file_location('reconnect_candidate_'+uuid.uuid4().hex, path)
    if spec is None or spec.loader is None:
        raise RuntimeError('Missing helper '+str(path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


core = fresh(HERE/'core.py')
session = fresh(HERE/'session.py')
measurements = fresh(HERE.parent/'measurements.py')
driver = fresh(HERE.parent/'reconnect.py')
# Read-only helpers loaded fresh. Do not call the parent's gated run/experiment.
parent = fresh(HERE.parent/'PlatformSolverInvestigation.py')


def inventory(top, base, inspected):
    if top.fullPathName != 'Top Platform:1' or base.fullPathName not in ('Base Hexagon:1','Base Hexagon Bottom:1'):
        raise ValueError('Select exact root Top Platform:1 and Base Hexagon:1 (or Base Hexagon Bottom:1)')
    if top.isGrounded or top.isGroundToParent or not base.isGrounded:
        raise ValueError('Top must be ungrounded; selected base must already be grounded')
    attachments, servos = [], []
    nubs = ['Nub:3','Nub(Mirror):1','Nub:8','Nub(Mirror):2','Nub:9','Nub(Mirror):3']
    def require(info, joint, endpoints, kind):
        if info['errors'] or info['suppressed'] is not False or not joint.isValid:
            raise ValueError('Candidate joint unreadable, suppressed or invalid: '+info['name'])
        if {info['one'],info['two']} != set(endpoints):
            raise ValueError('Unexpected root-context endpoints: '+info['name'])
        cast = adsk.fusion.RigidJointMotion.cast if kind=='rigid' else adsk.fusion.RevoluteJointMotion.cast
        if not cast(joint.jointMotion):
            raise ValueError('Wrong joint motion type: '+info['name'])
    for i, nub in enumerate(nubs, 1):
        hits = [(j,d) for j,d in inspected if d['family']=='as_built' and d['name']==f'Nub {i} Rigid']
        if len(hits)!=1:
            raise ValueError('Expected exactly one Nub attachment '+str(i))
        joint, info=hits[0]
        require(info,joint,(nub,top.fullPathName),'rigid')
        attachments.append(joint)
        ends=(f'Leg Assembly:{i}+Crankshaft:1', f'Leg Assembly:{i}+Servo:1')
        hits=[(j,d) for j,d in inspected if d['family']=='joint' and
              {d['one'],d['two']}==set(ends)]
        if len(hits)!=1:
            raise ValueError('Expected one ordinary crank/servo revolute in leg '+str(i))
        joint,info=hits[0]
        require(info,joint,ends,'revolute')
        if joint.isLocked:
            raise ValueError('Servo is locked; this experiment will not unlock it')
        servos.append(joint)
        mounts=[(j,d) for j,d in inspected if d['family']=='as_built' and d['name']==f'Servo {i} Rigid']
        if not mounts:
            raise ValueError('Missing grounded base servo mount '+str(i))
        for joint,info in mounts:
            require(info,joint,(ends[1],base.fullPathName),'rigid')
    return attachments, servos


def angles(servos, originals=None):
    values=[]
    for i,joint in enumerate(servos):
        if not joint.isValid or joint.healthState != adsk.fusion.FeatureHealthStates.HealthyFeatureHealthState:
            raise ValueError('Servo joint health is not healthy: '+joint.name)
        motion=adsk.fusion.RevoluteJointMotion.cast(joint.jointMotion)
        if motion is None:
            raise ValueError('Servo motion no longer revolute')
        value=motion.rotationValue
        if not math.isfinite(value):
            raise ValueError('Non-finite servo angle')
        if originals is not None and (not math.isfinite(originals[i]) or abs(value-originals[i])>math.radians(10)):
            raise ValueError('Servo exceeded raw 10 degree excursion; wrapped turns are not accepted')
        limits=motion.rotationLimits
        if limits is None:
            raise ValueError('Cannot read revolute limits')
        bounds=[]
        for enabled,bound,lower in ((limits.isMinimumValueEnabled,limits.minimumValue,True),
                                    (limits.isMaximumValueEnabled,limits.maximumValue,False)):
            if enabled:
                if not math.isfinite(bound) or (value<bound if lower else value>bound):
                    raise ValueError('Invalid or exceeded enabled Fusion angle limit')
                bounds.append(bound)
        if len(bounds)==2 and bounds[0]>bounds[1]:
            raise ValueError('Inverted enabled angle limits')
        values.append(value)
    return values


def check_summary(summary, articulation, changed):
    numbers=[summary[k] for k in ('top_error_mm','top_error_deg','base_mm','base_deg')]
    numbers += [s[k] for s in summary['servos'] for k in ('relative_mm','relative_deg')]
    if not all(math.isfinite(v) for v in numbers):
        raise ValueError('Non-finite measured result')
    if summary['base_mm']>.02 or summary['base_deg']>.01:
        raise ValueError('Base drift: experiment aborted')
    if summary['top_error_mm']>.05 or summary['top_error_deg']>.02:
        raise ValueError('Full top pose mismatch: experiment aborted')
    if len(summary['servos'])!=6 or any(s['relative_deg']>10 for s in summary['servos']):
        raise ValueError('Invalid servo relative-body rotation / 10 degree bound')
    if articulation=='all' and summary['articulated_count']!=6:
        raise ValueError('Proof lacks real relative-body rotation at all six servos')
    if articulation=='changed' and changed and summary['articulated_count']==0:
        raise ValueError('Changed CSV pose without independent servo-body articulation')


class FusionAdapter:
    flag_error = driver.StateRestoreError

    def __init__(self, app, design, top, base, attachments, servos, record, gate):
        self.app, self.design, self.top, self.base = app, design, top, base
        self.document = app.activeDocument
        self.root = design.rootComponent
        self.attachments, self.servos = attachments, servos
        self.record, self.gate = record, gate

    def guard(self):
        if self.app.activeDocument != self.document or self.app.activeProduct != self.design:
            raise self.flag_error('Active document/product changed; no more mutations; close disposable copy unsaved')

    def pump(self):
        self.guard()
        adsk.doEvents()
        self.guard()
        self.app.activeViewport.refresh()

    def pause(self, seconds):
        deadline = time.monotonic()+seconds
        while time.monotonic()<deadline:
            self.pump()
            time.sleep(min(.02,max(0,deadline-time.monotonic())))

    def prepare(self):
        self.guard()
        self.baseline = parent.capture(self.root)
        self.original_angles = angles(self.servos)
        self.original_flags = [j.isSuppressed for j in self.attachments]
        self.original_ground = self.top.isGrounded
        self.bodies = [(str(i),j.occurrenceOne.fullPathName,j.occurrenceTwo.fullPathName)
                       for i,j in enumerate(self.servos,1)]
        for _,one,two in self.bodies:
            if one not in self.baseline or two not in self.baseline:
                raise ValueError('Servo endpoints are not in root-context capture')
        self.last = self.baseline
        self.last_target = self.baseline[self.top.fullPathName]
        self.record('baseline',dict(poses=self.baseline,angles=self.original_angles,bodies=self.bodies,
                    suppression=self.original_flags,grounded=self.original_ground))
        return self.last_target

    def move(self, target, phase, articulation):
        self.guard()
        try:
            intact = ([j.isSuppressed for j in self.attachments] == self.original_flags and
                      self.top.isGrounded == self.original_ground and not self.top.isGroundToParent and
                      self.base.isGrounded and all(j.isValid for j in self.attachments))
        except Exception as exc:
            raise self.flag_error('Constraint flags no longer readable; no more mutations') from exc
        if not intact:
            raise self.flag_error('Constraint flags changed since baseline; no more mutations')
        matrix = adsk.core.Matrix3D.create()
        for r in range(3):
            for c in range(4):
                if not matrix.setCell(r,c,target[r][c]):
                    raise ValueError('Matrix construction failed')
        changed_mm, changed_deg = measurements.error(self.last_target,target)
        changed = changed_mm>.01 or changed_deg>.001
        started = time.monotonic()
        def observe(state):
            snapshot = parent.capture(self.root)
            values = angles(self.servos,self.original_angles)
            summary = measurements.summarize(self.baseline,snapshot,self.top.fullPathName,
                        self.base.fullPathName,self.bodies,target)
            incremental = measurements.summarize(self.last,snapshot,self.top.fullPathName,
                        self.base.fullPathName,self.bodies,target)
            self.record('observation',dict(phase=phase,state=state,target=target,poses=snapshot,
                        angles=values,summary=summary,incremental=incremental,
                        changed_above_resolution=changed,elapsed_seconds=time.monotonic()-started))
            # Restoration is observed but not interrupted by pose mismatch; always report residuals.
            if phase != 'restore':
                check_summary(summary, 'all' if articulation=='all' else 'none', changed)
                if articulation=='changed':
                    check_summary(incremental,'changed',changed)
                mm,deg=measurements.error(self.baseline[self.top.fullPathName],snapshot[self.top.fullPathName])
                if mm>5 or deg>3:
                    raise ValueError('Measured plate exceeded 5 mm / 3 degree bound')
                for _,one,two in self.bodies:
                    housing=two if '+Servo:' in two else one
                    mm,deg=measurements.error(
                        measurements.relative(self.baseline[self.base.fullPathName],self.baseline[housing]),
                        measurements.relative(snapshot[self.base.fullPathName],snapshot[housing]))
                    if mm>.02 or deg>.01:
                        raise ValueError('Servo housing drifted relative to base')
            if state=='released':
                self.last=snapshot
                self.last_target=target
        driver.reconnect(self.top,self.attachments,matrix,self.pump,observe)
        self.record('pose_complete',dict(phase=phase,elapsed_seconds=time.monotonic()-started))

    def restore_report(self):
        result: dict = dict(restored=False,errors=[])
        try:
            after=parent.capture(self.root)
            residuals={name:measurements.error(pose,after[name]) for name,pose in self.baseline.items()}
            result['occurrence_residuals_mm_deg']=residuals
            result['poses']=after
            values=angles(self.servos,self.original_angles)
            delta=[math.degrees(a-b) for a,b in zip(values,self.original_angles)]
            result['joint_residuals_deg']=delta
            result['suppression']=[j.isSuppressed for j in self.attachments]
            result['grounded']=self.top.isGrounded
            result['restored']=(all(mm<=.05 and deg<=.02 for mm,deg in residuals.values()) and
                                all(abs(v)<=.02 for v in delta) and
                                result['suppression']==self.original_flags and
                                result['grounded']==self.original_ground)
        except Exception as exc:
            result['errors'].append(str(exc))
        return result


def run(context):
    app=adsk.core.Application.get()
    ui=app.userInterface
    answer,cancelled=ui.inputBox(WARNING+'\n\nOpen a DISPOSABLE COPY, not your working model.\n'
        'Type exactly '+CONSENT+' to authorize +0.5 mm proof and CSV animation.',
        'PlatformReconnectPlayback — experimental consent','')
    if cancelled or answer.strip()!=CONSENT:
        return
    report=HERE/'reports'/('playback-'+uuid.uuid4().hex+'.jsonl')
    stream=None
    def record(event,data):
        if stream is None:
            raise RuntimeError('Report is not open; mutation is not authorized')
        stream.write(json.dumps(dict(event=event,monotonic=time.monotonic(),data=data),allow_nan=False)+'\n')
        stream.flush()
        os.fsync(stream.fileno())
    try:
        mode,cancelled=ui.inputBox('PREVIEW: first 3 CSV seconds (default). FULL: entire capture.\n'
            'Both sample about 0.5 s apart, including final row. Native solver can be much slower than real time.\n'
            'After +0.5 mm proof you must visually confirm all six linkages before CSV moves.',
            'Playback length','PREVIEW')
        if cancelled:
            return
        if mode.strip() not in ('PREVIEW','FULL'):
            raise ValueError('Type PREVIEW or FULL')
        with CSV.open(encoding='utf-8-sig',newline='') as handle:
            rows=core.read_csv(handle)
        # Validate all source-row requested extrema before any model mutation, even unsampled rows.
        full=core.frames(rows)
        selected=core.frames(rows,duration=3) if mode.strip()=='PREVIEW' else full
        design=adsk.fusion.Design.cast(app.activeProduct)
        if not design:
            raise ValueError('Activate the intended Fusion design')
        root=design.rootComponent
        occurrences={root.allOccurrences.item(i).fullPathName:root.allOccurrences.item(i)
                     for i in range(root.allOccurrences.count)}
        def select(prompt):
            selection=ui.selectEntity(prompt,'Occurrences')
            if selection is None or selection.entity.fullPathName not in occurrences:
                raise ValueError('Selection cancelled or not a root-context occurrence')
            return occurrences[selection.entity.fullPathName]
        top=select('Select Top Platform:1 COMPONENT in Browser')
        base=select('Select grounded Base Hexagon:1 (or Base Hexagon Bottom:1), NOT assembly root')
        inspected=parent.inspect_joints(root)
        report.parent.mkdir(exist_ok=True)
        stream=report.open('x',encoding='utf-8')
        record('start',dict(warning=WARNING,document=app.activeDocument.name,fusion_version=app.version,
                           csv=str(CSV),source_rows=len(rows),mode=mode.strip(),frames=selected,
                           translation_scale=.005,rotation_scale=.08,neutral='first CSV row',
                           joints=[d for _,d in inspected],top=top.fullPathName,base=base.fullPathName))
        attachments,servos=inventory(top,base,inspected)
        def gate():
            answer,cancelled=ui.inputBox('Numerical +0.5 mm proof passed: all six servo BODY pairs rotated, base fixed.\n'
                'Inspect ALL SIX rods/cranks at BOTH ends. Are all connections intact and base stationary?\n'
                'This is not automatic closure proof. Type CONNECTED only after inspection.\n'
                'Cancel/anything else aborts CSV and attempts restoration.\nReport: '+str(report),
                'Required visual closure gate','')
            accepted=not cancelled and answer.strip()=='CONNECTED'
            record('visual_gate',dict(accepted=accepted))
            return accepted
        adapter=FusionAdapter(app,design,top,base,attachments,servos,record,gate)
        result=session.play(adapter,selected,core)
        record('complete',result)
        ui.messageBox('CSV candidate completed; starting poses/flags restored within stated tolerances.\n'
            'NOT validated inverse kinematics. Close disposable copy WITHOUT SAVING.\nReport: '+str(report))
    except Exception:
        details=traceback.format_exc()
        if stream is not None:
            try: record('error',dict(traceback=details,instruction='DO NOT SAVE'))
            except Exception: pass
        ui.messageBox('Playback stopped. No success is claimed. DO NOT SAVE; close disposable copy unsaved.\n'
                      +details+'\nReport (if created): '+str(report))
    finally:
        if stream is not None:
            stream.close()

