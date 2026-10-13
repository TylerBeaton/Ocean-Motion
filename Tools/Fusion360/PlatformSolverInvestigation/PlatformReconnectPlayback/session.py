"""Bounded proof -> visual gate -> actual CSV poses -> restoration."""

def play(adapter, frames, core):
    if len(frames) < 2:
        raise ValueError('Playback requires multiple CSV frames')
    for frame in frames:
        core.validate_frame(frame)
    baseline = adapter.prepare()
    targets = [core.target(baseline, frame) for frame in frames]
    proof = core.target(baseline, dict(heave_cm=.05, rotation_vector=(0,0,0)))
    attempted, flags_intact = False, True
    result = None
    try:
        attempted = True
        adapter.move(proof, 'proof', 'all')
        if not adapter.gate():
            raise ValueError('Visual closure not explicitly accepted; CSV blocked')
        adapter.move(baseline, 'neutral', 'none')
        previous = 0.
        for frame, pose in zip(frames, targets):
            adapter.pause(max(0., frame['time']-previous))
            adapter.record('csv_request', dict(frame=frame, target=pose))
            adapter.move(pose, 'csv:'+str(frame['sequence']), 'changed')
            previous = frame['time']
        adapter.record('csv_complete', dict(frames=len(frames)))
    except adapter.flag_error:
        flags_intact = False
        raise
    finally:
        if attempted and flags_intact:
            try:
                adapter.move(baseline, 'restore', 'none')
            except adapter.flag_error:
                flags_intact = False
                adapter.record('restoration_skipped', dict(reason='Constraint flags failed during restoration; no more mutations'))
                raise
            finally:
                # Read-only residual capture even when restoring the pose failed.
                result = adapter.restore_report()
                if not flags_intact:
                    result['restored'] = False
                adapter.record('restoration', result)
        elif attempted:
            residual = adapter.restore_report()
            residual['restored'] = False  # flag integrity failed regardless of pose proximity
            adapter.record('residual_without_restoration', residual)
            adapter.record('restoration_skipped', dict(reason='Constraint flag cleanup failed; no further mutations; DO NOT SAVE'))
    if not result or not result['restored']:
        raise ValueError('Incomplete restoration; DO NOT SAVE')
    return result
