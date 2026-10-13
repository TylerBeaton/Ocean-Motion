"""Fusion script: ordinary-joint-driven numerical 6DOF CSV playback candidate."""
import json
import math
from pathlib import Path
import time
import traceback
import uuid

try:
    from . import core, controller
except ImportError:
    import core
    import controller

_CANCEL=False
UNKNOWN_GRAPH=('Observed unresolved graph: root Rigid 4 / Rigid 10 endpoint getters failed; '
               'six contextual Rigid 7 as-built suppression getters failed. This script does '
               'not read or edit them and does NOT establish full-graph health.')


def items(collection):
    return [collection.item(i) for i in range(collection.count)]


def discover(root,cast):
    occurrences=items(root.allOccurrences)
    result=[]
    for number in range(1,7):
        label='Leg Assembly:'+str(number)
        matches=[o for o in occurrences if o.name==label and o.fullPathName==label]
        if len(matches)!=1: raise ValueError('Need unique root '+label)
        leg=matches[0]
        candidates=[]
        # Only ordinary joints within the explicit six leg contexts. No root joints,
        # allJoints, asBuiltJoints, or unknown suppression getters are inspected.
        for native in items(leg.component.joints):
            joint=native.createForAssemblyContext(leg)
            if joint is None: raise ValueError('Missing contextual joint in '+label)
            motion=cast(joint.jointMotion)
            if motion is None: continue
            if joint.isSuppressed: raise ValueError('Suppressed revolute in '+label)
            angle=math.degrees(motion.rotationValue)
            if not math.isfinite(angle): raise ValueError('Unreadable angle in '+label)
            one=joint.occurrenceOne
            two=joint.occurrenceTwo
            if one is None or two is None: raise ValueError('Missing servo endpoints in '+label)
            a,b=one.fullPathName,two.fullPathName
            if a==b or not a.startswith(label+'+') or not b.startswith(label+'+'):
                raise ValueError('Endpoints are not distinct root-context leg bodies: '+label)
            limits=motion.rotationLimits
            lo=math.degrees(limits.minimumValue) if limits.isMinimumValueEnabled else None
            hi=math.degrees(limits.maximumValue) if limits.isMaximumValueEnabled else None
            if any(v is not None and not math.isfinite(v) for v in (lo,hi)):
                raise ValueError('Non-finite enabled limit')
            candidates.append(dict(label=label,joint_name=joint.name,motion=motion,joint=joint,
                                   one=a,two=b,minimum=lo,maximum=hi,initial=angle))
        if len(candidates)!=1: raise ValueError('Expected exactly one readable ordinary revolute in '+label)
        result.extend(candidates)
    return result


def pose(occurrence):
    m=occurrence.transform2
    p=[[m.getCell(r,c) for c in range(4)] for r in range(3)]
    core.residual(p,p)
    return p


def articulation(before,after,joints):
    result=[]
    for j in joints:
        a,b=j['one'],j['two']
        e=core.residual(core.relative(after['bodies'][a],after['bodies'][b]),
                        core.relative(before['bodies'][a],before['bodies'][b]))
        result.append(dict(label=j['label'],relative_residual=e,
                           relative_mm=core.norm(e[:3]),relative_degrees=core.norm(e[3:]),
                           articulated=core.norm(e[3:])>.002))
    return result


class Report:
    def __init__(self):
        folder=Path(__file__).resolve().parent/'reports'
        folder.mkdir(exist_ok=True)
        self.path=folder/('pose-feedback-'+time.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex+'.jsonl')
        self.file=self.path.open('x',encoding='utf-8')
        self.start=time.monotonic()
    def log(self,event,**data):
        self.file.write(json.dumps(dict(event=event,elapsed=time.monotonic()-self.start,**data),allow_nan=False)+'\n')
        self.file.flush()


class FusionPlant:
    def __init__(self,app,design,top,base,joints,log,progress):
        self.app=app; self.design=design; self.top=top.fullPathName; self.base=base.fullPathName
        self.joints=joints; self.log=log; self.progress=progress
        self.document=app.activeDocument
        self.setters=0; self.restoring=False; self.restore_failed=False
        self.initial=self.snapshot()
        self.bounds(self.initial['angles'])
        for j in joints:
            if j['one'] not in self.initial['bodies'] or j['two'] not in self.initial['bodies']:
                raise ValueError('Endpoint absent from root allOccurrences')

    def snapshot(self):
        if self.app.activeDocument!=self.document: raise ValueError('Active document changed')
        bodies={}
        for o in items(self.design.rootComponent.allOccurrences):
            if o.fullPathName in bodies: raise ValueError('Duplicate occurrence path')
            bodies[o.fullPathName]=pose(o)
        angles=[math.degrees(j['motion'].rotationValue) for j in self.joints]
        if not all(map(math.isfinite,angles)): raise ValueError('Non-finite angle readback')
        return dict(angles=angles,top=bodies[self.top],bodies=bodies)

    def bounds(self,q):
        if len(q)!=6 or not all(map(math.isfinite,q)): raise ValueError('Invalid drive vector')
        for a,j in zip(q,self.joints):
            if abs(a-j['initial'])>10+1e-7: raise ValueError('Total excursion exceeds 10 degrees')
            if j['minimum'] is not None and a<j['minimum']-1e-8: raise ValueError('Enabled minimum limit')
            if j['maximum'] is not None and a>j['maximum']+1e-8: raise ValueError('Enabled maximum limit')

    def check(self,s):
        self.bounds(s['angles'])
        core.envelope(self.initial['top'],s['top'])
        e=core.residual(s['bodies'][self.base],self.initial['bodies'][self.base])
        if core.norm(e[:3])>.01 or core.norm(e[3:])>.005:
            raise ValueError('Base moved beyond .01 mm / .005 degrees')

    def drive(self,q):
        import adsk
        self.bounds(q)
        for index,(a,j) in enumerate(zip(q,self.joints)):
            if not self.restoring and (_CANCEL or self.progress.wasCancelled):
                raise ValueError('Cancelled by user')
            current=math.degrees(j['motion'].rotationValue)
            if abs(a-current)>1.000001: raise ValueError('Sequential setter update exceeds 1 degree')
            if abs(a-current)<1e-9: continue
            if self.setters>=360: raise ValueError('Per-frame 360 individual setter budget exceeded')
            self.setters+=1
            self.log('setter_begin',index=index,before=current,requested=a)
            j['motion'].rotationValue=math.radians(a)
            # Each sequential write may already solve; neither events nor refresh
            # can turn six property setters into an atomic six-joint command.
            adsk.doEvents()
            s=self.snapshot()
            self.log('setter_end',index=index,state=s)
            self.check(s)
        self.app.activeViewport.refresh()

    def restore_check(self,a,b):
        errors={}
        if set(a['bodies'])!=set(b['bodies']): errors['topology']='Occurrence set changed'
        for path in a['bodies'].keys() & b['bodies'].keys():
            e=core.residual(a['bodies'][path],b['bodies'][path])
            if core.norm(e[:3])>.01 or core.norm(e[3:])>.005: errors[path]=e
        angle_errors=[x-y for x,y in zip(a['angles'],b['angles'])]
        if max(map(abs,angle_errors))>.01: errors['angles']=angle_errors
        self.log('restoration_residuals',errors=errors,angle_errors=angle_errors,
                 top_residual=core.residual(a['top'],b['top']))
        if errors: raise ValueError('DO NOT SAVE: restoration residual outside .01 mm / .005 deg / .01 joint deg')

    def return_initial(self):
        self.restoring=True
        self.setters=0
        try:
            for attempt in range(12):
                s=self.snapshot()
                delta=[a-b for a,b in zip(self.initial['angles'],s['angles'])]
                if max(map(abs,delta))<=.001: break
                factor=max(1.,max(map(abs,delta)))
                self.drive([b+d/factor for b,d in zip(s['angles'],delta)])
            self.restore_check(self.initial,self.snapshot())
            self.log('initial_restoration_verified',state=self.snapshot())
        except Exception:
            self.restore_failed=True
            self.log('initial_restoration_failed',traceback=traceback.format_exc())
            raise
        finally: self.restoring=False


def select_named(ui,name):
    selection=ui.selectEntity('Select '+name+' (occurrence, not face)','Occurrences')
    if selection is None: raise ValueError('Selection cancelled')
    entity=selection.entity
    if entity.name!=name or entity.fullPathName!=name:
        raise ValueError('Required root occurrence '+name)
    return entity


def run(context):
    import adsk.core
    import adsk.fusion
    global _CANCEL
    _CANCEL=False
    app=adsk.core.Application.get(); ui=app.userInterface
    report=Report(); plant=None; progress=None; completed=False
    try:
        report.log('start',unknown_graph=UNKNOWN_GRAPH,native_validation='pending until this run',
                   limits=dict(probe_deg=.25,update_deg=1,total_deg=10,plate_mm=5,plate_deg=3,
                               frame_iterations=4,frame_batches=60,frame_setters=360))
        design=adsk.fusion.Design.cast(app.activeProduct)
        if design is None: raise ValueError('Activate a Fusion design')
        consent,cancel=ui.inputBox(UNKNOWN_GRAPH+'\n\nUse ONLY an independently opened disposable copy. '
            'Do not save this document. Sequential joint drives may pop or drift. No hardware output. '
            'Type DISPOSABLE COPY to permit small revolute drives; anything else cancels.',
            'PoseFeedbackPlayback — explicit consent','')
        if cancel or consent!='DISPOSABLE COPY':
            report.log('consent_declined'); return
        report.log('consent',value=consent)
        top=select_named(ui,'Top Platform:1'); base=select_named(ui,'Base Hexagon:1')
        if not base.isGrounded: raise ValueError('Base Hexagon:1 must already be grounded; no edits made')
        joints=discover(design.rootComponent,adsk.fusion.RevoluteJointMotion.cast)
        report.log('discovered',joints=[{k:v for k,v in j.items() if k not in ('motion','joint')} for j in joints])
        default=Path(__file__).resolve().parents[2]/'UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv'
        source,cancel=ui.inputBox('CSV path (first row is calibrated neutral):','CSV source',str(default))
        if cancel: return
        rows=core.load_csv(source)
        mode,cancel=ui.inputBox('PREVIEW = up to 31 evenly spaced source rows, including endpoints. '
                              'FULL = every CSV row. All modes write full measurement reports.',
                              'Playback mode','PREVIEW')
        if cancel: return
        if mode not in ('PREVIEW','FULL'): raise ValueError('Mode must be PREVIEW or FULL')
        if mode=='PREVIEW':
            indices=sorted({round(i*(len(rows)-1)/30) for i in range(31)})
        else: indices=list(range(len(rows)))
        progress=ui.createProgressDialog()
        progress.isCancelButtonShown=True
        progress.show('PoseFeedbackPlayback','Proof gate then CSV %v / %m',0,len(indices),1)
        plant=FusionPlant(app,design,top,base,joints,report.log,progress)
        report.log('initial',state=plant.initial,source=str(source),source_rows=len(rows),indices=indices)
        targets=[core.target_pose(plant.initial['top'],rows[0],row) for row in rows]
        control=controller.Controller(plant,report.log)
        proof=core.hold_heading(plant.initial['top'],core.compose(plant.initial['top'],[0,0,.05],[.10,-.08,0]))
        core.envelope(plant.initial['top'],proof)
        state=control.follow(proof)
        evidence=articulation(plant.initial,state,joints)
        report.log('proof',target=proof,actual=state,articulation=evidence)
        if not all(e['articulated'] for e in evidence):
            raise ValueError('Nonzero proof did not demonstrate all six body-relative servo articulations')
        plant.return_initial()
        # Returning the drives is insufficient: above requires every occurrence restored.
        prior_time=rows[0]['time']
        for frame,index in enumerate(indices):
            if _CANCEL or progress.wasCancelled: raise ValueError('Cancelled by user')
            plant.setters=0
            progress.progressValue=frame
            started=time.monotonic()
            state=control.follow(targets[index])
            report.log('frame',source_sequence=rows[index]['sequence'],source_time=rows[index]['time'],
                       target=targets[index],actual=state,residual=core.residual(targets[index],state['top']),
                       articulation=articulation(plant.initial,state,joints))
            # Best-effort source dwell, never catch-up/drop or claim real time.
            wait=max(0.,rows[index]['time']-prior_time-(time.monotonic()-started))
            until=time.monotonic()+wait
            while time.monotonic()<until:
                adsk.doEvents()
                if _CANCEL or progress.wasCancelled: raise ValueError('Cancelled by user')
                time.sleep(.01)
            prior_time=rows[index]['time']
        completed=True
        report.log('playback_completed',frames=len(indices))
        ui.messageBox('Playback completed. Observe the final pose now. Click OK to restore the original angles. '
                      'Do not save. This is not a real-time or full-graph validation.\nReport: '+str(report.path))
    except Exception as exc:
        report.log('abort',error=str(exc),traceback=traceback.format_exc())
        # Failed probe restoration ends further edits; close without saving.
        if plant is not None and 'restoration' in str(exc).lower(): plant.restore_failed=True
        ui.messageBox('PoseFeedbackPlayback aborted: '+str(exc)+'\nDO NOT SAVE.\nReport: '+str(report.path))
    finally:
        if plant is not None:
            if not plant.restore_failed:
                try: plant.return_initial()
                except Exception: pass
            report.log('end',playback_completed=completed,restored=not plant.restore_failed)
            ui.messageBox(('Original occurrence poses and angles verified restored.' if not plant.restore_failed else
                           'RESTORATION UNVERIFIED — DO NOT SAVE. Close this disposable copy without saving.')+
                          '\nNo constraints edited, no save or actuator output requested.\nReport: '+str(report.path))
        if progress is not None: progress.hide()
        report.file.close()


def stop(context):
    global _CANCEL
    _CANCEL=True
