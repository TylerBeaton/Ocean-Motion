import importlib.util
import pathlib
import unittest
from test_core import load

HERE = pathlib.Path(__file__).resolve().parents[1]

def engine():
    spec = importlib.util.spec_from_file_location('engine_test', HERE/'session.py')
    assert spec and spec.loader
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m

class FlagError(RuntimeError): pass

class Adapter:
    flag_error = FlagError
    def __init__(self, fault=None):
        self.fault=fault
        self.calls=[]
        self.events=[]
        self.baseline=((1,0,0,0),(0,1,0,0),(0,0,1,0))
    def prepare(self): return self.baseline
    def move(self, pose, phase, articulation):
        self.calls.append(phase)
        if self.fault == 'flags' or (self.fault == 'restore_flags' and phase == 'restore'):
            raise FlagError('cleanup failed')
        if self.fault == 'proof' and phase == 'proof': raise ValueError('no articulation')
    def restore_report(self): return {'restored': self.fault != 'residual'}
    def record(self, event, data): self.events.append((event, data))
    def pause(self, seconds): pass
    def gate(self): return self.fault != 'visual'

class SessionTests(unittest.TestCase):
    def test_proof_visual_gate_multiple_csv_then_restore(self):
        self.assertTrue((HERE/'session.py').exists(), 'Animation orchestration missing')
        c=load(); a=Adapter()
        frames=[dict(time=i*.5, sequence=i, heave_cm=i*.01, rotation_vector=(0,0,0)) for i in range(3)]
        result=engine().play(a, frames, c)
        self.assertTrue(result['restored'])
        self.assertEqual(a.calls, ['proof','neutral','csv:0','csv:1','csv:2','restore'])
    def test_failed_proof_or_visual_stops_csv_and_restores(self):
        self.assertTrue((HERE/'session.py').exists())
        for fault in ('proof','visual'):
            a=Adapter(fault)
            with self.assertRaises(ValueError): engine().play(a, [dict(time=0,sequence=0,heave_cm=0,rotation_vector=(0,0,0))]*2,load())
            self.assertEqual(a.calls, ['proof','restore'])
    def test_flag_failure_stops_all_further_mutation(self):
        self.assertTrue((HERE/'session.py').exists())
        a=Adapter('flags')
        with self.assertRaises(FlagError): engine().play(a,[dict(time=0,sequence=0,heave_cm=0,rotation_vector=(0,0,0))]*2,load())
        self.assertEqual(a.calls,['proof'])
        self.assertEqual(a.events[-1][0], 'restoration_skipped')
        self.assertTrue(any(e=='residual_without_restoration' for e,d in a.events))
    def test_failed_restoration_flags_overrides_close_pose(self):
        a=Adapter('restore_flags')
        with self.assertRaises(FlagError):
            engine().play(a,[dict(time=0,sequence=0,heave_cm=0,rotation_vector=(0,0,0))]*2,load())
        self.assertEqual(a.calls[-1],'restore')
        self.assertFalse([d for e,d in a.events if e=='restoration'][-1]['restored'])

    def test_residual_never_claims_success(self):
        self.assertTrue((HERE/'session.py').exists())
        a=Adapter('residual')
        with self.assertRaisesRegex(ValueError,'restoration'):
            engine().play(a,[dict(time=0,sequence=0,heave_cm=0,rotation_vector=(0,0,0))]*2,load())
