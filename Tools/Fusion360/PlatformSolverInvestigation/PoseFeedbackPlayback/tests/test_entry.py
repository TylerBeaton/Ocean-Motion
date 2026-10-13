"""Host adapter tests use a small protocol fake, NOT native Fusion evidence."""
import unittest
import types
import core
import PoseFeedbackPlayback as entry

class Collection:
    def __init__(self,items): self.items=items; self.count=len(items)
    def item(self,i): return self.items[i]

class DiscoveryTests(unittest.TestCase):
    def test_six_contextual_revolutes_without_root_graph_reads(self):
        legs=[]
        for i in range(1,7):
            name='Leg Assembly:'+str(i)
            one=types.SimpleNamespace(fullPathName=name+'+body')
            two=types.SimpleNamespace(fullPathName=name+'+horn')
            motion=types.SimpleNamespace(rotationValue=0., rotationLimits=types.SimpleNamespace(
                isMinimumValueEnabled=False,isMaximumValueEnabled=False))
            joint=types.SimpleNamespace(name='servo',jointMotion=motion,isSuppressed=False,
                occurrenceOne=one,occurrenceTwo=two)
            joint.createForAssemblyContext=lambda leg,j=joint:j
            legs.append(types.SimpleNamespace(name=name,fullPathName=name,component=types.SimpleNamespace(joints=Collection([joint]))))
        root=types.SimpleNamespace(allOccurrences=Collection(legs))
        found=entry.discover(root,lambda x:x)
        self.assertEqual(len(found),6)
        self.assertTrue(found[5]['one'].startswith('Leg Assembly:6+'))
        root.allOccurrences.items.pop()
        root.allOccurrences.count=5
        with self.assertRaisesRegex(ValueError,'Leg Assembly:6'): entry.discover(root,lambda x:x)

    def test_articulation_requires_all_six_and_stationary_base(self):
        p=[[1,0,0,0],[0,1,0,0],[0,0,1,0]]
        before={'bodies':{'base':p},'angles':[0]*6,'top':p}
        after={'bodies':{'base':p},'angles':[.1]*6,'top':p}
        joints=[]
        for i in range(6):
            a,b='a'+str(i),'b'+str(i)
            joints.append({'one':a,'two':b,'label':str(i)})
            before['bodies'][a]=before['bodies'][b]=p
            after['bodies'][a]=p
            after['bodies'][b]=core.compose(p,[0,0,0],[0,0,.1])
        evidence=entry.articulation(before,after,joints)
        self.assertEqual(sum(x['articulated'] for x in evidence),6)
        after['bodies']['b5']=p
        self.assertEqual(sum(x['articulated'] for x in entry.articulation(before,after,joints)),5)

class AdapterSafetyTests(unittest.TestCase):
    def plant(self):
        p=entry.FusionPlant.__new__(entry.FusionPlant)
        p.joints=[dict(initial=0.,minimum=-2.,maximum=2.) for _ in range(6)]
        p.base='base'; p.log=lambda *a,**k:None
        pose=[[1,0,0,0],[0,1,0,0],[0,0,1,0]]
        p.initial=dict(top=pose,angles=[0.]*6,bodies={'base':pose,'top':pose,'other':pose})
        return p
    def test_limits_and_total_excursion(self):
        p=self.plant()
        p.bounds([1.]*6)
        with self.assertRaisesRegex(ValueError,'maximum'): p.bounds([2.1]*6)
        with self.assertRaisesRegex(ValueError,'minimum'): p.bounds([-2.1]*6)
        for j in p.joints: j['minimum']=j['maximum']=None
        with self.assertRaisesRegex(ValueError,'excursion'): p.bounds([10.1]*6)
    def test_stationary_base_and_full_plate_envelope(self):
        import copy
        p=self.plant(); state=copy.deepcopy(p.initial)
        state['bodies']['base'][0][3]=.01
        with self.assertRaisesRegex(ValueError,'Base moved'): p.check(state)
        state=copy.deepcopy(p.initial); state['top']=core.compose(state['top'],[.51,0,0],[0,0,0])
        with self.assertRaisesRegex(ValueError,'envelope'): p.check(state)
    def test_restore_checks_unrelated_occurrence_too(self):
        import copy
        p=self.plant(); changed=copy.deepcopy(p.initial)
        changed['bodies']['other']=core.compose(changed['bodies']['other'],[0,.1,0],[0,0,0])
        with self.assertRaisesRegex(ValueError,'DO NOT SAVE'): p.restore_check(p.initial,changed)

class RuntimeTests(unittest.TestCase):
    def test_complete_preview_entry_with_explicit_software_api(self):
        import sys
        from unittest.mock import patch
        from pathlib import Path
        identity=[[1,0,0,0],[0,1,0,0],[0,0,1,0]]
        q=[0.0]*6
        class Motion:
            rotationLimits=types.SimpleNamespace(isMinimumValueEnabled=False,isMaximumValueEnabled=False)
            def __init__(self,index): self.index=index
            @property
            def rotationValue(self):
                import math
                return math.radians(q[self.index])
            @rotationValue.setter
            def rotationValue(self,v):
                import math
                q[self.index]=math.degrees(v)
        def top_pose():
            v=[sum(((1. if r==c else 0.)+.12)*q[c] for c in range(6)) for r in range(6)]
            return core.compose(identity,[x/10 for x in v[:3]],v[3:])
        class Occ:
            def __init__(self,name,get_pose):
                self.name=name.split('+')[-1]; self.fullPathName=name; self.get_pose=get_pose
                self.isGrounded=True
            @property
            def transform2(self):
                p=self.get_pose()
                return types.SimpleNamespace(getCell=lambda r,c:p[r][c])
        top=Occ('Top Platform:1',top_pose); base=Occ('Base Hexagon:1',lambda:identity)
        occurrences=[top,base]
        for i in range(6):
            label='Leg Assembly:'+str(i+1)
            one=Occ(label+'+body',lambda:identity)
            two=Occ(label+'+horn',lambda i=i:core.compose(identity,[0,0,0],[0,0,q[i]]))
            joint=types.SimpleNamespace(name='servo',jointMotion=Motion(i),isSuppressed=False,occurrenceOne=one,occurrenceTwo=two)
            joint.createForAssemblyContext=lambda leg,j=joint:j
            leg=Occ(label,lambda:identity)
            leg.component=types.SimpleNamespace(joints=Collection([joint]))
            occurrences.extend([leg,one,two])
        root=types.SimpleNamespace(allOccurrences=Collection(occurrences))
        design=types.SimpleNamespace(rootComponent=root)
        source=str(Path(__file__).resolve().parents[3]/'UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv')
        answers=iter(['DISPOSABLE COPY',source,'PREVIEW'])
        selections=iter([top,base]); messages=[]; events=[]
        progress=types.SimpleNamespace(wasCancelled=False,show=lambda *a:None,hide=lambda:None)
        ui=types.SimpleNamespace(inputBox=lambda *a:(next(answers),False),
            selectEntity=lambda *a:types.SimpleNamespace(entity=next(selections)),
            messageBox=lambda text:messages.append(text),createProgressDialog=lambda:progress)
        app=types.SimpleNamespace(userInterface=ui,activeProduct=design,activeDocument=object(),
                                 activeViewport=types.SimpleNamespace(refresh=lambda:None))
        adsk=types.ModuleType('adsk'); adsk.doEvents=lambda:None
        adsk.core=types.ModuleType('adsk.core'); adsk.core.Application=types.SimpleNamespace(get=lambda:app)
        adsk.fusion=types.ModuleType('adsk.fusion')
        adsk.fusion.Design=types.SimpleNamespace(cast=lambda x:x)
        adsk.fusion.RevoluteJointMotion=types.SimpleNamespace(cast=lambda x:x)
        class MemoryReport:
            path='SOFTWARE-API-TEST-ONLY'
            file=types.SimpleNamespace(close=lambda:None)
            def log(self,event,**data): events.append((event,data))
        tick=[0.0]
        def clock(): tick[0]+=1.; return tick[0]
        with patch.dict(sys.modules,{'adsk':adsk,'adsk.core':adsk.core,'adsk.fusion':adsk.fusion}), \
             patch.object(entry,'Report',MemoryReport), patch.object(entry.time,'monotonic',clock):
            entry.run(None)
        self.assertFalse(any(k=='abort' for k,d in events),messages)
        self.assertEqual(sum(k=='frame' for k,d in events),31)
        self.assertTrue(events[-1][1]['restored'])
        self.assertTrue(events[-1][1]['playback_completed'])
        self.assertTrue(all(abs(a)<.001 for a in q))

if __name__=='__main__': unittest.main()
