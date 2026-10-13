"""Stand-ins test guards and orchestration, never native solver validity."""
import importlib.util
import pathlib
import sys
import types
import unittest

HERE=pathlib.Path(__file__).resolve().parents[1]
N=types.SimpleNamespace

def entry():
    adsk=types.ModuleType('adsk'); adsk.core=types.ModuleType('adsk.core'); adsk.fusion=types.ModuleType('adsk.fusion')
    adsk.fusion.FeatureHealthStates=N(HealthyFeatureHealthState=0)
    adsk.fusion.RevoluteJointMotion=N(cast=lambda m: m if getattr(m,'kind',None)=='revolute' else None)
    adsk.fusion.RigidJointMotion=N(cast=lambda m: m if getattr(m,'kind',None)=='rigid' else None)
    sys.modules.update({'adsk':adsk,'adsk.core':adsk.core,'adsk.fusion':adsk.fusion})
    spec=importlib.util.spec_from_file_location('entry_test', HERE/'PlatformReconnectPlayback.py')
    assert spec and spec.loader
    module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module

def fixture():
    top=N(fullPathName='Top Platform:1',isGrounded=False,isGroundToParent=False)
    base=N(fullPathName='Base Hexagon:1',isGrounded=True,isGroundToParent=False)
    pairs=[]
    nubs=['Nub:3','Nub(Mirror):1','Nub:8','Nub(Mirror):2','Nub:9','Nub(Mirror):3']
    def add(family,name,one,two,kind):
        j=N(name=name,isValid=True,healthState=0,isSuppressed=False,isLocked=False,
            jointMotion=N(kind=kind),occurrenceOne=N(fullPathName=one),occurrenceTwo=N(fullPathName=two))
        pairs.append((j,dict(family=family,name=name,one=one,two=two,suppressed=False,errors={})))
    for i in range(1,7):
        add('as_built',f'Nub {i} Rigid',nubs[i-1],top.fullPathName,'rigid')
        add('joint','Revolute 1',f'Leg Assembly:{i}+Crankshaft:1',f'Leg Assembly:{i}+Servo:1','revolute')
        add('as_built',f'Servo {i} Rigid',f'Leg Assembly:{i}+Servo:1',base.fullPathName,'rigid')
    return top,base,pairs

class Matrix:
    def __init__(self, value=None): self.value=[list(r) for r in (value or ((1,0,0,0),(0,1,0,0),(0,0,1,0),(0,0,0,1)))]
    def getCell(self,r,c): return self.value[r][c]
    def setCell(self,r,c,v): self.value[r][c]=v; return True
    def copy(self): return Matrix(self.value)

class Collection:
    def __init__(self, items): self.items=items;self.count=len(items)
    def item(self,i): return self.items[i]

class AdapterTests(unittest.TestCase):
    def test_adapter_real_reconnect_calls_measures_bodies_and_restores(self):
        m=entry()
        self.assertTrue(hasattr(m,'FusionAdapter'), 'Native Fusion adapter missing')
        top,base,pairs=fixture(); attachments,servos=m.inventory(top,base,pairs)
        top.transform2=Matrix();base.transform2=Matrix()
        bodies=[]
        for joint in servos:
            for body in (joint.occurrenceOne,joint.occurrenceTwo): body.transform2=Matrix();bodies.append(body)
            joint.jointMotion.rotationValue=0
            joint.jointMotion.rotationLimits=N(isMinimumValueEnabled=False,isMaximumValueEnabled=False,minimumValue=0,maximumValue=0)
        root=N(allOccurrences=Collection([top,base]+bodies))
        design=N(rootComponent=root)
        app=N(activeProduct=design,activeDocument=object(),activeViewport=N(refresh=lambda:None))
        m.adsk.core.Matrix3D=N(create=Matrix)
        m.adsk.doEvents=lambda:None
        records=[]
        a=m.FusionAdapter(app,design,top,base,attachments,servos,lambda e,d:records.append((e,d)),lambda:True)
        baseline=a.prepare()
        pose=m.core.target(baseline,dict(heave_cm=.05,rotation_vector=(0,0,0)))
        # Moving the plate alone is NOT enough to pass the real measurement guard.
        with self.assertRaisesRegex(ValueError,'relative-body'): a.move(pose,'proof','all')
        self.assertFalse(top.isGrounded)
        self.assertTrue(all(not j.isSuppressed for j in attachments))
        # Explicit fake kinematics for testing plumbing; never native solver evidence.
        import math
        def solve():
            angle=top.transform2.getCell(2,3)
            for j in servos:
                j.occurrenceOne.transform2=Matrix(((math.cos(angle),-math.sin(angle),0,0),(math.sin(angle),math.cos(angle),0,0),(0,0,1,0),(0,0,0,1)))
        m.adsk.doEvents=solve
        a.move(pose,'proof','all')
        a.move(baseline,'restore','none')
        self.assertTrue(a.restore_report()['restored'])
        self.assertTrue(any(e=='observation' and d['state']=='released' for e,d in records))
        attachments[0].isSuppressed=True
        with self.assertRaises(m.driver.StateRestoreError): a.move(pose,'csv:1','changed')
        self.assertTrue(attachments[0].isSuppressed)

    def test_run_cancellation_never_mutates(self):
        m=entry(); self.assertTrue(hasattr(m,'run'), 'Fusion run entrypoint missing')
        m.adsk.core.Application=N(get=lambda:N(userInterface=N(inputBox=lambda *a:('',True))))
        m.run(None)

class EntryTests(unittest.TestCase):
    def test_only_six_verified_attachments_unknowns_not_mutated(self):
        self.assertTrue((HERE/'PlatformReconnectPlayback.py').exists(), 'Fusion entrypoint missing')
        m=entry(); top,base,pairs=fixture()
        pairs.append((object(),dict(family='as_built',name='Rigid 4',one=None,two=top.fullPathName,suppressed=None,errors={'one':'unreadable'})))
        a,s=m.inventory(top,base,pairs)
        self.assertEqual(len(a),6); self.assertEqual(len(s),6)
        self.assertTrue(all(j.name.startswith('Nub ') for j in a))
        pairs[0][1]['one']='Other:1'
        with self.assertRaises(ValueError): m.inventory(top,base,pairs)
    def test_bad_base_duplicate_attachment_and_locked_servo_fail(self):
        self.assertTrue((HERE/'PlatformReconnectPlayback.py').exists())
        m=entry()
        for fault in ('base','duplicate','lock','suppressed'):
            top,base,pairs=fixture()
            if fault=='base': base.isGrounded=False
            if fault=='duplicate': pairs.append(pairs[0])
            if fault=='lock': pairs[1][0].isLocked=True
            if fault=='suppressed': pairs[0][1]['suppressed']=True
            with self.assertRaises(ValueError): m.inventory(top,base,pairs)
    def test_enabled_limits_finite_raw_excursion(self):
        self.assertTrue((HERE/'PlatformReconnectPlayback.py').exists())
        m=entry()
        limits=N(isMinimumValueEnabled=True,isMaximumValueEnabled=True,minimumValue=-.2,maximumValue=.2)
        joint=N(name='Revolute',healthState=0,isValid=True,jointMotion=N(kind='revolute',rotationValue=0.,rotationLimits=limits))
        self.assertEqual(m.angles([joint],[0]),[0])
        joint.healthState=2
        with self.assertRaisesRegex(ValueError,'health'): m.angles([joint],[0])
        joint.healthState=0
        for value in (float('nan'), .3, 6.283185307179586):
            joint.jointMotion.rotationValue=value
            with self.assertRaises(ValueError): m.angles([joint],[0])
        joint.jointMotion.rotationValue=0; limits.maximumValue=float('nan')
        with self.assertRaises(ValueError): m.angles([joint],[0])
    def test_full_pose_base_and_body_gates(self):
        self.assertTrue((HERE/'PlatformReconnectPlayback.py').exists())
        m=entry()
        good=dict(top_error_mm=0,top_error_deg=0,base_mm=0,base_deg=0,
                  servos=[dict(relative_mm=0,relative_deg=.1) for _ in range(6)],articulated_count=6)
        m.check_summary(good,'all',True)
        for key,value in [('base_mm',.1),('top_error_deg',.1),('articulated_count',0)]:
            bad=dict(good);bad[key]=value
            with self.assertRaises(ValueError):m.check_summary(bad,'all',True)
