# Configuration-specific endpoint observations — 2026-10-31

Supported, unloaded plate; pushrods attached. All six enabled at recorded references. Only selected channel moves; five others hold reference. User confirmed quiet/level all-six holding before this stage. Supply reported5 V/3 A; no electrical/load measurements supplied. Commanded pulses are not measured joint angles or plate poses.

## Complete recorded table — 11:57:19

User independently continued through channels1–5 and supplied all six endpoint records. Raw data is saved in [recorded-endpoints-2026-10-31.csv](recorded-endpoints-2026-10-31.csv). All records reach1000 and2000 µs, the firmware's outer exploration guardrails. Therefore every endpoint is classified as **software-limited**; none establishes a discovered mechanical hard stop or final operating limit. These raw bounds are not automatically applied to another controller or IK.

| Channel | Neutral (µs) | Raise sign | Down record (µs) | Up record (µs) | Numeric recorded range (µs) |
| --- | --- | --- | --- | --- | --- |
| 0 | 1500 | +1 | 1000 | 2000 | 1000–2000 |
| 1 | 1600 | -1 | 2000 | 1000 | 1000–2000 |
| 2 | 1550 | +1 | 1000 | 2000 | 1000–2000 |
| 3 | 1600 | -1 | 2000 | 1000 | 1000–2000 |
| 4 | 1500 | +1 | 1000 | 2000 | 1000–2000 |
| 5 | 1500 | -1 | 2000 | 1000 | 1000–2000 |

After supplying the complete CSV, user explicitly confirmed that all six stayed quiet and clear throughout and returned normally to neutral, including the last servo: "Yep, everything is perfect." This completes the initial supported-assembly, one-channel-at-a-time command-range check. No strain, interference, abnormal settling or return problems were reported.

The CSV contains stored commands, not physical feedback. Final neutral pose was confirmed visually by the user, not measured by sensors. No timed endurance proof, measured angle/pulse map, supply/load-capacity validation or combined-pose workspace proof follows from these trials. Preserve the full table before any reset/upload and record any later reported issues alongside it. No margins or final IK limits have been assigned.

The earlier servo0 checkpoint below is retained as history. Its partial table is superseded by the complete CSV above.

## Servo 0

- Reference1500 µs, raise sign+1; coarse step25 µs with manually widened windows. User reported motion comfortable at1450,1400 and1350 µs, then continued independently.
- At11:33:43 serial confirmed channel0 command1000 µs, offset-500, window1000..2000. The next decrement was rejected by software. User saved the down endpoint and explicitly confirmed everything looked great and normal return to neutral.
- At11:38:58 serial confirmed channel0 command2000 µs, offset+500, window1000..2000. Next increment rejected by software. User then provided saved up endpoint2000 µs; no physical issue reported. Final upward-pass neutral return was requested but not separately confirmed by a status/physical observation at this checkpoint.
- At11:39:48 CSV row was `0,1500,1,1000,2000,1000,2000`.
- Both endpoints are **software-limited observations**, not discovered physical hard stops or final IK limits. No margin assigned. Actual timed hold/motion duration not measured. Other channels were unrecorded at this earlier checkpoint.

Earlier partial board result table (superseded):

```csv
servo,neutral_us,raise_sign,down_us,up_us,min_us,max_us
0,1500,1,1000,2000,1000,2000
1,1600,-1,0,0,0,0
2,1550,1,0,0,0,0
3,1600,-1,0,0,0,0
4,1500,1,0,0,0,0
5,1500,-1,0,0,0,0
```

Zero denotes unrecorded. Preserve raw records separately from any later conservative operating limits. Do not infer combined-pose reachability from these single-channel trials.
