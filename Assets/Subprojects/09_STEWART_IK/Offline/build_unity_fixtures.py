"""Export Python reference poses/results for the independent Unity C# implementation."""
import hashlib
import json
from pathlib import Path
from geometry_io import load_geometry
from stewart_ik import Pose
from validate_ik import axis_quaternion

ROOT = Path(__file__).resolve().parents[1]

def main():
    geometry = ROOT / "Measurements/offline-geometry.json"
    model, _ = load_geometry(geometry)
    poses = [("neutral", Pose())]
    for axis in ((1,0,0),(0,1,0),(0,0,1)):
        for sign in (-1,1):
            poses.append((f"translation_{axis}_{sign}", Pose.unity(tuple(.001*sign*x for x in axis),(0,0,0,1))))
            poses.append((f"rotation_{axis}_{sign}", Pose.unity((0,0,0),axis_quaternion(axis,sign))))
    poses += [("combined",Pose.unity((.0005,.00075,-.0005),axis_quaternion((1,2,3),.5))),
              ("unreachable",Pose.unity((0,1,0),(0,0,0,1)))]
    cases = []
    for name, pose in poses:
        solved = model.solve(pose)
        cases.append(dict(name=name, translation_mm=pose.translation_mm,
                          rotation_flat=[x for row in pose.rotation for x in row],
                          expected_solution=solved.found, angles_deg=solved.angles_deg))
    output=dict(geometry_sha256=hashlib.sha256(geometry.read_bytes()).hexdigest(),cases=cases)
    (ROOT / "Reports/unity-reference-fixtures.json").write_text(json.dumps(output,indent=2,allow_nan=False)+"\n")
    print(f"Exported {len(cases)} Python reference fixtures.")

if __name__ == "__main__":
    main()
