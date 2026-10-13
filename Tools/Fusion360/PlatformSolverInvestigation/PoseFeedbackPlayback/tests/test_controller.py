import unittest
import copy
import core
import controller

class Plant:
    def __init__(self, stuck=False, restore_bad=False):
        self.q=[0.0]*6; self.writes=0; self.stuck=stuck; self.restore_bad=restore_bad
        self.neutral=[[1,0,0,0],[0,1,0,0],[0,0,1,0]]
    def snapshot(self):
        q=self.q
        p=core.compose(self.neutral,[q[0]/10,q[1]/10,q[2]/10],q[3:])
        return {'angles':list(q),'top':p,'bodies':{'top':p}}
    def drive(self,q):
        self.writes+=1
        if self.restore_bad and self.writes>1: return
        if not self.stuck: self.q=list(q)
    def check(self,s): core.envelope(self.neutral,s['top'])
    def bounds(self,q):
        if max(map(abs,q))>10: raise ValueError('excursion')
    def restore_check(self,a,b):
        if max(abs(x-y) for x,y in zip(a['angles'],b['angles']))>.01:
            raise ValueError('DO NOT SAVE: restoration failed')

class ControllerTests(unittest.TestCase):
    def test_closed_loop_corrects_all_six_axes(self):
        p=Plant(); events=[]
        c=controller.Controller(p,lambda kind,**d: events.append((kind,d)))
        target=core.compose(p.neutral,[.025,-.025,.05],[.2,-.2,.2])
        c.follow(target)
        self.assertLess(core.norm(core.residual(target,p.snapshot()['top'])),.08)
        self.assertTrue(any(k=='probe' for k,d in events))
        self.assertLessEqual(p.writes,controller.MAX_WRITES)
    def test_native_restore_exception_is_marked_unrestorable(self):
        p=Plant()
        c=controller.Controller(p,lambda *a,**kw: None)
        saved=p.snapshot(); p.q[0]=.25
        def broken(q): raise RuntimeError('native setter failure')
        p.drive=broken
        with self.assertRaisesRegex(ValueError,'DO NOT SAVE.*restoration'):
            c.restore(saved)
        self.assertTrue(p.restore_failed)

    def test_actual_csv_all_frames_on_coupled_software_plant(self):
        from pathlib import Path
        class CoupledPlant(Plant):
            def snapshot(self):
                v=[sum(((1.0 if r==c else 0.0)+.12)*self.q[c] for c in range(6)) for r in range(6)]
                p=core.compose(self.neutral,[x/10 for x in v[:3]],v[3:])
                return {'angles':list(self.q),'top':p,'bodies':{'top':p}}
        p=CoupledPlant(); c=controller.Controller(p,lambda *a,**kw: None)
        path=Path(__file__).resolve().parents[3]/'UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv'
        rows=core.load_csv(path)
        proof=core.hold_heading(p.neutral,core.compose(p.neutral,[0,0,.05],[.1,-.08,0]))
        c.follow(proof)
        self.assertTrue(all(abs(q)>.002 for q in p.q))
        c.follow(p.neutral)
        worst_mm=0.; worst_deg=0.
        for row in rows:
            target=core.target_pose(p.neutral,rows[0],row)
            actual=c.follow(target)
            e=core.residual(target,actual['top'])
            worst_mm=max(worst_mm,core.norm(e[:3])); worst_deg=max(worst_deg,core.norm(e[3:]))
            self.assertLessEqual(core.norm(e[:3]),controller.POSITION_TOL)
            self.assertLessEqual(core.norm(e[3:]),controller.ROTATION_TOL)
        print('SOFTWARE PLANT ONLY:',len(rows),'CSV frames; max residual mm/deg:',worst_mm,worst_deg)

    def test_unresponsive_aborts(self):
        p=Plant(stuck=True); events=[]
        with self.assertRaisesRegex(ValueError,'unresponsive'):
            controller.Controller(p,lambda kind,**data:events.append(kind)).follow(core.compose(p.neutral,[0,0,.05],[0,0,0]))
        self.assertIn('probe_error',events)
    def test_restore_failure_stops_before_second_probe(self):
        p=Plant(restore_bad=True)
        with self.assertRaisesRegex(ValueError,'restoration'):
            controller.Controller(p,lambda *a,**kw: None).follow(core.compose(p.neutral,[0,0,.05],[0,0,0]))
        self.assertEqual(p.writes,2)

if __name__=='__main__': unittest.main()
