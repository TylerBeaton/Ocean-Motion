import unittest
import math
import core

class SolverTests(unittest.TestCase):
    def test_full_six_dof_coupling(self):
        j = [[(1 if r == c else .08 * (r + 1)) for c in range(6)] for r in range(6)]
        wanted = [.3, -.2, .4, -.1, .25, -.3]
        error = [sum(a*b for a,b in zip(row,wanted)) for row in j]
        actual = core.dls(j, error, damping=1e-6)
        for a,b in zip(actual,wanted): self.assertAlmostEqual(a,b,places=5)
        with self.assertRaisesRegex(ValueError, 'singular|unresponsive'):
            core.dls([[0.0]*6 for _ in range(6)], [1]*6)

class MappingTests(unittest.TestCase):
    def test_neutral_mapping_and_full_error(self):
        p = [[1,0,0,2],[0,1,0,3],[0,0,1,4]]
        q = [0,0,0,1]
        first = {'time':0, 'y':2, 'q':q}
        row = {'time':1, 'y':2.1, 'q':[math.sin(.1),0,0,math.cos(.1)]}
        target = core.target_pose(p, first, row)
        self.assertAlmostEqual(target[2][3]-4, .05)
        e = core.residual(target,p)
        self.assertAlmostEqual(e[2], .5)
        self.assertAlmostEqual(e[3], -math.degrees(.2)*.08)
        self.assertEqual(core.residual(p,p), [0.0]*6)
        yaw = core.compose(p, [0,0,0], [0,0,1])
        self.assertAlmostEqual(core.residual(yaw,p)[5],1)

    def test_world_heading_held_for_tilted_neutral(self):
        neutral=core.compose([[1,0,0,0],[0,1,0,0],[0,0,1,0]],[0,0,0],[15,20,30])
        first={'q':[0,0,0,1],'y':0}
        row={'q':[math.sin(.1),0,math.sin(.05),math.sqrt(1-math.sin(.1)**2-math.sin(.05)**2)],'y':0}
        target=core.target_pose(neutral,first,row)
        self.assertAlmostEqual(math.atan2(target[1][0],target[0][0]),math.atan2(neutral[1][0],neutral[0][0]),places=10)

    def test_actual_csv(self):
        from pathlib import Path
        path = Path(__file__).resolve().parents[3] / 'UnityBoatTelemetryPlayback/recordings/boat-telemetry-test-000-20261005-140749.csv'
        rows = core.load_csv(path)
        self.assertEqual(len(rows),278)
        self.assertGreater(rows[-1]['time'],13)
        p = [[1,0,0,0],[0,1,0,0],[0,0,1,0]]
        for row in rows: core.envelope(p,core.target_pose(p,rows[0],row))

if __name__ == '__main__': unittest.main()
