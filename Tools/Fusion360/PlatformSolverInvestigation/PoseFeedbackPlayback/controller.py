"""Bounded, remeasured numerical feedback, with mandatory probe restoration."""
try:
    from . import core
except ImportError:
    import core

PROBE=.25
MAX_UPDATE=1.0
MAX_ITERATIONS=4
MAX_WRITES=60  # batches; adapter separately budgets individual angle setters
POSITION_TOL=.08  # mm norm
ROTATION_TOL=.04  # degrees norm


class Controller:
    def __init__(self,plant,log):
        self.plant=plant
        self.log=log
        self.writes=0

    def write(self,q,restoration=False):
        # Reserve six restoration batches even when ordinary work is exhausted.
        limit=MAX_WRITES if restoration else MAX_WRITES-6
        if self.writes>=limit: raise ValueError('Per-frame write budget exhausted')
        self.plant.bounds(q)
        current=self.plant.snapshot()['angles']
        if max(abs(a-b) for a,b in zip(q,current))>MAX_UPDATE+1e-7:
            raise ValueError('Angular update exceeds 1 degree')
        self.writes+=1
        self.plant.drive(q)
        state=self.plant.snapshot()
        self.log('after_write',restoration=restoration,state=state,writes=self.writes)
        self.plant.check(state)
        return state

    def restore(self,saved):
        try:
            self._restore(saved)
        except Exception as exc:
            self.plant.restore_failed=True
            self.log('restoration_failed',error=str(exc))
            raise ValueError('DO NOT SAVE: restoration failed: '+str(exc)) from exc

    def _restore(self,saved):
        self.log('restoration_begin',saved=saved)
        # One verified restore attempt; never proceed after imperfect restoration.
        for attempt in range(2):
            current=self.plant.snapshot()
            try:
                self.plant.restore_check(saved,current)
                self.log('restoration_verified',attempt=attempt,state=current)
                return
            except ValueError:
                if attempt==1: raise
            self.write(saved['angles'],restoration=True)
        raise ValueError('DO NOT SAVE: restoration failed')

    def jacobian(self):
        columns=[]
        for i in range(6):
            saved=self.plant.snapshot()
            self.plant.check(saved)
            q=list(saved['angles'])
            q[i]+=PROBE
            try: self.plant.bounds(q)
            except ValueError:
                q[i]-=2*PROBE
                self.plant.bounds(q)
            step=q[i]-saved['angles'][i]
            try:
                measured=self.write(q)
                actual_step=measured['angles'][i]-saved['angles'][i]
                response=core.residual(measured['top'],saved['top'])
                self.log('probe',axis=i,step=step,actual_step=actual_step,
                         before=saved,after=measured,response=response)
                if abs(actual_step-step)>.02:
                    raise ValueError('unresponsive or unreliable probe input readback')
                if core.norm(response)<1e-4: raise ValueError('unresponsive probe')
                # Partial derivative of the actual sequential setter operation.
                columns.append([v/step for v in response])
            except Exception as exc:
                self.log('probe_error',axis=i,step=step,error=str(exc))
                raise
            finally:
                self.restore(saved)
        j=[list(row) for row in zip(*columns)]
        core.dls(j,[0.0]*6)  # explicit undamped rank gate
        self.log('jacobian',matrix=j)
        return j

    def follow(self,target):
        self.writes=0
        initial=self.plant.snapshot()
        for iteration in range(MAX_ITERATIONS+1):
            state=self.plant.snapshot()
            self.plant.check(state)
            e=core.residual(target,state['top'])
            self.log('feedback',iteration=iteration,target=target,actual=state,
                     residual=e,position_mm=core.norm(e[:3]),rotation_deg=core.norm(e[3:]))
            if core.norm(e[:3])<=POSITION_TOL and core.norm(e[3:])<=ROTATION_TOL:
                return state
            if iteration==MAX_ITERATIONS: break
            j=self.jacobian()
            dq=core.dls(j,e)
            scale=max(1.,max(map(abs,dq))/MAX_UPDATE)
            q=[a+d/scale for a,d in zip(state['angles'],dq)]
            after=self.write(q)
            if core.norm(core.residual(target,after['top']))>=core.norm(e)*.999:
                raise ValueError('No measurable feedback improvement; aborting')
        raise ValueError('Target not reached within per-frame iteration budget')
