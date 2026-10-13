"""Independent rigid-transform measurements, centimetres internally."""
import math
from typing import Any


def relative(parent, child):
    return tuple(tuple(sum(parent[k][r] * child[k][c] for k in range(3))
                       for c in range(3)) +
                 (sum(parent[k][r] * (child[k][3] - parent[k][3]) for k in range(3)),)
                 for r in range(3))


def error(before, after):
    for pose in (before, after):
        if len(pose) != 3 or any(len(row) != 4 for row in pose):
            raise ValueError("Expected a 3 by 4 pose")
        if not all(math.isfinite(v) for row in pose for v in row):
            raise ValueError("Non-finite component pose")
    mm = math.sqrt(sum((after[r][3] - before[r][3]) ** 2 for r in range(3))) * 10
    rotation = relative(before, after)
    cosine = (sum(rotation[i][i] for i in range(3)) - 1) / 2
    degrees = math.degrees(math.acos(max(-1.0, min(1.0, cosine))))
    return mm, degrees


def summarize(before, after, top, base, servos, target):
    """servos contains (label, root-context body-one path, body-two path)."""
    top_mm, top_deg = error(target, after[top])
    base_mm, base_deg = error(before[base], after[base])
    result: dict[str, Any] = dict(top_error_mm=top_mm, top_error_deg=top_deg,
                  base_mm=base_mm, base_deg=base_deg, servos=[], articulated_count=0)
    for label, one, two in servos:
        mm, degrees = error(relative(before[one], before[two]),
                            relative(after[one], after[two]))
        result["servos"].append(dict(name=label, relative_mm=mm, relative_deg=degrees))
        result["articulated_count"] += degrees > 0.001
    return result
