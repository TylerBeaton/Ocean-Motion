"""Offline parser exercise against the actual Unity recording; no Fusion imports."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path

HERE=Path(__file__).resolve().parent
CSV=HERE.parent.parent/'UnityBoatTelemetryPlayback'/'recordings'/'boat-telemetry-test-000-20261005-140749.csv'

def verify():
    spec=importlib.util.spec_from_file_location('verified_csv_core',HERE/'core.py')
    assert spec and spec.loader
    core=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(core)
    with CSV.open(encoding='utf-8-sig',newline='') as stream:
        rows=core.read_csv(stream)
    full=core.frames(rows)
    preview=core.frames(rows,duration=3)
    every=core.frames(rows,interval=.000001)
    assert len(rows)==278
    assert full[-1]['sequence']==int(rows[-1]['sequence'])
    assert len(preview)>2 and len(full)>len(preview)
    assert len({(f['heave_cm'],f['rotation_vector']) for f in full})>2
    return dict(native_fusion_validated=False,source=str(CSV),sha256=hashlib.sha256(CSV.read_bytes()).hexdigest(),
                source_rows=len(rows),duration_seconds=rows[-1]['time_seconds']-rows[0]['time_seconds'],
                preview_frames=len(preview),full_frames=len(full),last_sequence=full[-1]['sequence'],
                preview_last_seconds=preview[-1]['time'],
                all_row_max_abs_heave_mm=max(abs(f['heave_cm'])*10 for f in every),
                all_row_max_rotation_deg=max(math.degrees(math.sqrt(sum(v*v for v in f['rotation_vector']))) for f in every),
                preview=preview,full=full)

if __name__=='__main__':
    print(json.dumps(verify(),indent=2,allow_nan=False))
