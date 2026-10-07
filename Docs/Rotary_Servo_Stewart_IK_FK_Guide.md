# Rotary-Servo Stewart Platform: IK and FK Study Guide

This is a learning example, not the selected Ocean Motion actuator architecture or hardware-ready control code. The first two sections preserve the explanations from the preceding answers; the third works through an illustrative six-servo pose and annotated Unity-style C#.

## 1. The algorithms to learn: IK versus FK

For a six-rotary-servo Stewart platform, learn IK first. IK answers “given the boat platform pose I want, what angle should each servo horn take?” FK reverses that question: “given six actual horn angles, where is the platform?” The rotary design differs from a linear-actuator Stewart platform because each servo moves the end of a short horn, while the connecting rod stays a fixed length.[1][3]

A useful mental model for one of the six legs is:

```text
servo shaft B ── rotating horn ── horn tip H ── fixed rod ── top joint P
```

1. Define the geometry once. For each leg, measure the servo shaft position `B`, horn length `a`, rod length `d`, and the horn’s direction `u` when its angle is zero. Also record the corresponding top-joint position `p` in the platform’s own coordinates. The six legs can have different measured values; don’t assume perfect symmetry after assembly.[1]

Use Unity’s Y-up convention consistently: `n` is the upward unit vector, and `u` lies in the base plane. The horn tip at angle `θ` is:

```text
H(θ) = B + a [cos(θ) u + sin(θ) n]
```

This describes a horn that rises as `θ` increases. A servo mounted in the opposite direction needs its own calibrated sign and zero offset.

2. IK: pose → six servo angles. Describe the desired platform pose by translation `T` and rotation `R`. For each top joint, transform its platform-local position into the base frame:

```text
P = T + R p
```

Now find `θ` such that the fixed rod reaches it:

```text
|P − H(θ)| = d
```

That is the entire geometric constraint. Expanding it gives a one-variable equation for each leg. With `L = P − B`:

```text
e = 2a (L · n)
f = 2a (L · u)
g = |L|² + a² − d²
```

Solve `e sin(θ) + f cos(θ) = g`. One candidate is:

```text
θ = asin(g / √(e² + f²)) − atan2(f, e)
```

There is generally a second candidate angle. Choose the branch that matches how that horn is assembled and remains continuous with its previous angle. If `|g|` exceeds `√(e² + f²)`, the requested joint position is unreachable; don’t merely clamp the `asin` input and command a misleading angle. Then check servo travel, joint articulation and collisions.[1]

3. FK: six measured angles → pose. With `θ` known, compute each horn tip `H`. The unknowns are now the platform’s three position components and three orientation components. Find the pose for which all six rods have their fixed lengths:

```text
residual_i(T, R) = |T + R p_i − H_i|² − d_i² = 0
```

Unlike IK, the six equations are coupled through the *same* platform pose. A practical FK solver starts at the last known pose, calculates all six residuals, makes a small pose correction using a numerical Jacobian and a damped least-squares/Newton-style step, then repeats until the errors are acceptably small. It must reject non-convergence or an implausible solution: multiple mathematical poses can fit actuator inputs, and the starting guess matters.[2]

For the Unity-to-hardware path, the order to implement is: model the six measured horn/rod/joint positions; solve and visualize IK around calibrated neutral; test small heave, pitch and roll separately; then add limits and collision checks before commanding hardware. FK is useful later for estimating or checking the *actual* pose—but commanded hobby-servo angles alone do not measure actual horn angles. You would need position feedback (or another pose sensor) to make FK an observation rather than a prediction.

One important separation: the boat’s motion pose is not a servo command. Keep the calibrated neutral rotation and waterline, bounded desired platform pose, IK result, and final per-servo trim/inversion/limits as distinct stages. That makes a wrong-moving horn diagnosable without distorting the boat simulation.

## 2. Equation-by-equation: how IK and FK fit together

Separate two problems:

```text
Desired platform pose → location of each top joint → angle of each servo
```

The first arrow is rigid-body geometry. The second is the horn-and-rod geometry. Solve the second arrow independently for each of the six legs.[1]

Use Unity’s Y-up coordinates. For one leg:

```text
B = fixed servo shaft position
p = top joint position, measured in platform-local coordinates
P = that top joint’s desired position in base coordinates
H = moving tip of the servo horn
a = horn length
d = fixed rod length
θ = horn angle
```

All positions and lengths must use the same unit—for example, millimetres.

### Step 2A — Move a top joint with the desired platform pose

First decide where the platform’s origin should be, `T`, and how it should be rotated, `R`. Then:

```text
P = T + R p
```

Read that from right to left: `p` says where this particular top joint sits on the platform; `R` rotates that offset along with the platform; `T` places the rotated platform in the base coordinate system.

Do this for all six top joints. Pitch, yaw, roll, and heave are not solved as separate servo motions. They combine into one desired pose, which gives six new `P` positions. That is why a simple pitch may move several—or all—servos.[1]

For Ocean Motion, define `T` and `R` relative to a calibrated neutral platform pose. Unity’s local X, Y, and Z rotations correspond to the pitch, yaw, and roll convention; Y displacement from the neutral waterline is heave. Keep the orientation as a rotation/quaternion through this calculation rather than using raw wrapped Euler readings.

### Step 2B — Describe where a servo horn can reach

Let `u` be the horizontal direction the horn points at `θ = 0`, and `n` be up, `(0, 1, 0)`. For a servo whose horn rotates in that vertical plane:

```text
H(θ) = B + a [cos(θ)u + sin(θ)n]
```

`a cos(θ)` is how far the horn tip reaches horizontally from the shaft; `a sin(θ)` is how high the tip rises. At `θ = 0`, the horn is horizontal. At a positive angle, its tip rises. This is a *geometric* angle, not necessarily the number sent to the servo: mounting direction, horn indexing, neutral trim, and inversion are applied later.[1]

### Step 2C — Impose the fixed-rod constraint

The rod joins `H` to `P` and cannot change length:

```text
|P − H(θ)| = d
```

Imagine a sphere of radius `d` around `P`. The horn tip travels on a circle around `B`. IK finds where that circle intersects the sphere. There might be two intersections, one, or none. This is the essential rotary-servo IK equation.

### Step 2D — Turn the distance constraint into an angle

Set `L = P − B`: the vector from the servo shaft to the desired top joint. Substitute the horn-tip equation and square the rod length:

```text
|L − a[cos(θ)u + sin(θ)n]|² = d²
```

Because `u` and `n` are perpendicular unit vectors, expanding the square gives:

```text
|L|² + a² − 2a[(L · u)cos(θ) + (L · n)sin(θ)] = d²
```

Rearrange it into one sine and one cosine:

```text
e sin(θ) + f cos(θ) = g

e (vertical contribution) = 2a (L · n)
f (horizontal horn-direction contribution) = 2a (L · u)
g (length-geometry requirement) = |L|² + a² − d²
```

The dot products are projections. `L · n` asks how far above the shaft the target is. `L · u` asks how far along the horn’s horizontal direction it is. `|L|²` also includes sideways separation, even though the horn itself cannot move sideways.[1][3]

Combine sine and cosine into one shifted sine. With `ρ = √(e² + f²)` and `φ = atan2(f, e)`:

```text
ρ sin(θ + φ) = g
θ₁ = asin(g / ρ) − φ
θ₂ = π − asin(g / ρ) − φ
```

Before accepting either candidate, check that `|g| ≤ ρ` and `ρ` is not effectively zero. Otherwise there is no usable solution from this calculation. Choose the candidate inside the servo’s measured travel and the intended assembled branch, preferably continuous with its previous angle. Don’t silently clamp an unreachable pose into a seemingly valid result.[1]

A small numerical example: let `B = (0, 0, 0)`, `u` point along X, `a = 20 mm`, and `P = (0, 0, 100) mm`. With a rod length of about `91.65 mm`, the calculation gives `e = 4000`, `f = 0`, and `g = 2000`. Thus `sin(θ) = 0.5`: both `30°` and `150°` satisfy the rod length. Both horn-tip positions were checked against the distance equation. The real servo’s range and assembly should select one branch.

### Step 2E — Turn six geometric angles into safe commands

Repeat 2A–2D for legs 1–6, using each leg’s own measured `B`, `p`, `u`, `a`, and `d`. Only then map each geometric `θ` through its calibrated neutral offset and direction to a servo command. Check the *whole* pose: every angle in range, rod ends within joint articulation, no collisions, and no abrupt jumps. Six individually valid angles do not alone prove that the assembled mechanism can move there safely.

### Step 3 — Understand FK by reversing the knowns

For FK, assume the six *actual* horn angles are known. The horn tips `H₁…H₆` are known; translation `T` and rotation `R` are not. Find the pose for which all fixed rods fit:

```text
rᵢ(T, R) = |T + R pᵢ − Hᵢ|² − dᵢ²
```

At a perfect solution all six residuals are zero. Changing `T` or `R` moves *all* top joints, so these equations must be solved together.[2]

A practical numerical FK loop: start with the last known pose; calculate six residuals; estimate how each changes when nudging each of six pose variables (the 6×6 Jacobian); compute a small correction, possibly damped; update and repeat; reject non-convergence or an out-of-workspace solution.[2]

The last-pose guess matters because multiple mathematical configurations may fit. Sending `30°` to a hobby servo is a *commanded* angle, not measured shaft feedback: FK based only on commands predicts rather than observes the platform.

## 3. Worked six-servo pose with equations and annotated Unity-style C#

This is a deliberately made-up, unloaded geometry for learning. It is **not** Ocean Motion’s measured geometry, final actuator choice, servo calibration, or approved workspace. We use millimetres inside this example and radians inside the trigonometric solver. Unity’s `Vector3` can hold millimetre-valued vectors if *all* inputs in this isolated model agree; convert a motion-pipeline translation in metres to millimetres at an explicit adapter boundary. The code is a pure math example, not a component you can attach or send to the Arduino.

### Step 0 — Specify six legs and one desired pose

Our example base shafts lie on an 80 mm radius at XZ-plane angles `[-10°, 10°, 110°, 130°, 230°, 250°]`. The top joints lie on a 65 mm radius at `[-25°, 25°, 95°, 145°, 215°, 265°]`, measured in the top platform’s local frame. Each horn is 20 mm, each rod is 95 mm, and the zero-angle horn direction points radially inward. This *illustrative arrangement* is not a verified real linkage; build geometry must be checked for pairing and interference.

The example desired platform-origin position is `T = (3, 105, -2) mm`. It combines a hypothetical 100 mm neutral height with `(3, +5, -2) mm` displacement. Use pitch `+4°` around local X, yaw `+2°` around Y, and roll `−3°` around local Z. We explicitly choose `R = Ry(yaw) Rx(pitch) Rz(roll)`, applied to a vector from right to left. This is one declared composition order; do not interchange it with an unspecified Euler convention.

```csharp
using UnityEngine;

// Illustration only: plain data and math, not a MonoBehaviour or hardware driver.
public static class RotaryIkStudyExample
{
    private static readonly float[] BaseAngles = { -10f, 10f, 110f, 130f, 230f, 250f };
    private static readonly float[] TopAngles  = { -25f, 25f, 95f, 145f, 215f, 265f };
    private const float HornMm = 20f;
    private const float RodMm = 95f;

    private static Vector3 OnXZCircle(float radiusMm, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        return new Vector3(radiusMm * Mathf.Cos(angle), 0f,
                           radiusMm * Mathf.Sin(angle));
    }

    // R = Ry * Rx * Rz. Unity quaternion multiplication applies Rz first.
    private static Quaternion DesiredRotation()
    {
        return Quaternion.AngleAxis(2f, Vector3.up)
             * Quaternion.AngleAxis(4f, Vector3.right)
             * Quaternion.AngleAxis(-3f, Vector3.forward);
    }
}
```

In a real design, store *measured per-leg values*, including horn directions, lengths, servo axes, calibrated zeros, and anchor positions. Do not infer all of them from a perfect circle.

### Step 1 — Desired top-joint position (`P`) for each leg

```text
Pᵢ (desired top joint in base frame) = T + R pᵢ
Lᵢ (shaft-to-top-joint vector) = Pᵢ − Bᵢ
```

For leg 1, `B₁ = (78.785, 0, −13.892) mm` and `p₁ = (58.910, 0, −27.470) mm`. After rotating that top-local point and adding `T`, `P₁ ≈ (60.830, 103.841, −31.655) mm`. Thus `L₁ ≈ (−17.955, 103.841, −17.763) mm`.

```csharp
Vector3 translationMm = new Vector3(3f, 105f, -2f);
Quaternion rotation = DesiredRotation();
Vector3 shaftB = OnXZCircle(80f, BaseAngles[i]);
Vector3 topLocalP = OnXZCircle(65f, TopAngles[i]);
Vector3 topInBaseP = translationMm + rotation * topLocalP; // P = T + R p
Vector3 shaftToTopL = topInBaseP - shaftB;                  // L = P - B
```

`i` is the leg index, zero through five, in the complete method below. `translationMm` is the location of the *platform origin relative to the base*, not the boat’s raw world position.

### Step 2 — Horn-plane orientation and reachability numbers

```text
uᵢ (zero-angle horn direction) = −Bᵢ / 80 mm   [only for this circular example]
n (up direction) = (0, 1, 0)
eᵢ (vertical contribution) = 2a (Lᵢ · n)
fᵢ (horizontal contribution) = 2a (Lᵢ · uᵢ)
gᵢ (length requirement) = |Lᵢ|² + a² − d²
ρᵢ (maximum sine/cosine combination) = √(eᵢ² + fᵢ²)
```

For leg 1, `u₁` is approximately `(-0.985, 0, +0.174)`. Substituting `a = 20 mm` and `d = 95 mm`:

```text
e₁ (vertical contribution) ≈ 4153.625 mm²
f₁ (horizontal contribution) ≈ 583.912 mm²
g₁ (length requirement) ≈ 2795.775 mm²
ρ₁ (maximum contribution) ≈ 4194.467 mm²
g₁ / ρ₁ (reachability ratio) ≈ 0.666539
```

The ratio lies in `[-1, +1]`, so the distance equation has mathematical angle solutions. It does *not* yet prove servo-range or collision feasibility. If `ρ` is near zero, treat the solve as degenerate rather than dividing by it.

```csharp
Vector3 hornZeroU = (-shaftB).normalized; // ONLY for this radial example
Vector3 upN = Vector3.up;
float eVertical = 2f * HornMm * Vector3.Dot(shaftToTopL, upN);
float fHorizontal = 2f * HornMm * Vector3.Dot(shaftToTopL, hornZeroU);
float gLength = shaftToTopL.sqrMagnitude + HornMm * HornMm - RodMm * RodMm;
float rho = Mathf.Sqrt(eVertical * eVertical + fHorizontal * fHorizontal);
```

### Step 3 — Solve and select the horn angle

```text
φᵢ (sine phase) = atan2(fᵢ, eᵢ)
θ₁ᵢ (first mathematical branch) = asin(gᵢ / ρᵢ) − φᵢ
θ₂ᵢ (second mathematical branch) = π − asin(gᵢ / ρᵢ) − φᵢ
```

Leg 1 produces about `33.798°` or `130.197°`. Suppose this illustrative assembly permits geometric horn angles only between `−70°` and `+70°`. Only `33.798°` is permitted. This range is an *example*, not the SP06 demo’s tested servo envelope or a certified platform limit.

```csharp
// Solver excerpt; candidate angles are radians until explicitly converted.
if (rho < 0.001f || Mathf.Abs(gLength) > rho + 0.001f)
    return false; // Degenerate/unreachable: reject the whole desired pose.

float ratio = Mathf.Clamp(gLength / rho, -1f, 1f); // Tiny roundoff only.
float phase = Mathf.Atan2(fHorizontal, eVertical);
float firstDeg = (Mathf.Asin(ratio) - phase) * Mathf.Rad2Deg;
float secondDeg = (Mathf.PI - Mathf.Asin(ratio) - phase) * Mathf.Rad2Deg;

bool firstAllowed = firstDeg >= -70f && firstDeg <= 70f;
bool secondAllowed = secondDeg >= -70f && secondDeg <= 70f;
if (!firstAllowed && !secondAllowed) return false;
// Select the previously calibrated assembly branch; don't switch merely
// because another candidate becomes numerically closer on one frame.
float chosenDeg = firstAllowed ? firstDeg : secondDeg;
```

That branch choice is safe only if your calibrated assembly actually uses the first branch. A real solver records which branch each leg occupies and checks continuity; it must not substitute the other branch automatically. The `Clamp` above only absorbs tiny floating-point overshoot *after* the explicit reachability test. For strict safety, a meaningful `|g| > ρ` failure must never be converted into a command.

### Step 4 — Repeat for six legs, then check each rod

For our same pose, the illustrative first-branch answers are:

| Leg | e vertical (mm²) | f horizontal (mm²) | g length (mm²) | ρ (mm²) | first θ | second θ |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 4153.625 | 583.912 | 2795.775 | 4194.467 | 33.798° | 130.197° |
| 2 | 4000.327 | 567.857 | 1719.123 | 4040.430 | 17.102° | 146.740° |
| 3 | 4031.154 | 835.025 | 2412.170 | 4116.730 | 24.167° | 132.427° |
| 4 | 4207.166 | 803.930 | 3023.457 | 4283.287 | 34.082° | 124.282° |
| 5 | 4415.221 | 740.245 | 4142.784 | 4476.845 | 58.208° | 102.757° |
| 6 | 4392.507 | 639.388 | 4016.121 | 4438.799 | 56.511° | 106.925° |

The data above were computed with an independent Python geometry check. For all six first-branch angles, reconstructing the horn tip yielded rod-length-squared errors at or below approximately `2×10⁻¹² mm²` in double precision. That verifies the example’s *equations*, not collision clearance, code compilation, real fabrication, or hardware motion.

A compact self-contained solver for this example follows. It returns `false` without publishing partial angles if any leg is invalid. Its geometric-only range and first-branch selection are explicitly provisional:

```csharp
public static bool TrySolveExample(float[] anglesDeg)
{
    if (anglesDeg == null || anglesDeg.Length != 6) return false;
    float[] candidate = new float[6];
    Vector3 T = new Vector3(3f, 105f, -2f); // mm in base coordinates
    Quaternion R = DesiredRotation();

    for (int i = 0; i < 6; i++)
    {
        Vector3 B = OnXZCircle(80f, BaseAngles[i]);
        Vector3 p = OnXZCircle(65f, TopAngles[i]);
        Vector3 u = (-B).normalized; // Radially inward, for this example only
        Vector3 P = T + R * p;
        Vector3 L = P - B;

        float e = 2f * HornMm * Vector3.Dot(L, Vector3.up); // Vertical
        float f = 2f * HornMm * Vector3.Dot(L, u);          // Horizontal
        float g = L.sqrMagnitude + HornMm * HornMm
                  - RodMm * RodMm;                         // Rod constraint
        float rho = Mathf.Sqrt(e * e + f * f);
        if (rho < 0.001f || Mathf.Abs(g) > rho + 0.001f)
            return false;

        float ratio = Mathf.Clamp(g / rho, -1f, 1f);
        float phase = Mathf.Atan2(f, e);
        float firstDeg = (Mathf.Asin(ratio) - phase) * Mathf.Rad2Deg;
        // This example assumes each horn is assembled on the first branch.
        if (float.IsNaN(firstDeg) || firstDeg < -70f || firstDeg > 70f)
            return false;

        float angleRad = firstDeg * Mathf.Deg2Rad;
        Vector3 H = B + HornMm * (Mathf.Cos(angleRad) * u
                                + Mathf.Sin(angleRad) * Vector3.up);
        float rodErrorMm = Mathf.Abs(Vector3.Distance(P, H) - RodMm);
        if (rodErrorMm > 0.01f) return false; // Math consistency only
        candidate[i] = firstDeg;
    }

    // Only expose a complete, geometrically consistent set of six angles.
    for (int i = 0; i < 6; i++) anglesDeg[i] = candidate[i];
    return true;
}
```

Put this method *inside* the `RotaryIkStudyExample` class shown above to use its constants/helpers. The method allocates a temporary array for clarity; a production pure solver would use an explicit result type and avoid avoidable per-frame allocations. It still lacks measured geometry, true branch management, joint/collision and speed limits, calibration, freshness, fault behavior, and a hardware safety gate. Do **not** connect its output directly to the existing serial firmware: that firmware currently expects an architecture-neutral pose packet and its two-servo demonstration maps pitch and roll, not six rotary IK angles.

### Step 5 — Visualize, then apply servo calibration outside IK

At each solved angle, visualize `B`, `H`, `P` and a line from `H` to `P` in Unity. This lets you see a horn spinning in its own vertical plane while the rod swivels to meet a moving platform joint. Reject geometry or motion if a rod pierces the plates or joints exceed their articulation, even when all six rod lengths are correct.

```text
θᵢ (geometric angle) → calibrated servo neutral + direction × θᵢ
                    → measured electrical/mechanical envelope → output
```

A neutral trim or servo-direction inversion belongs in this mapping, *not* in `P = T + R p` or in the boat’s telemetry. Because a trimmed centre may reduce travel on one side, check the resulting pulse/servo limits again after mapping. Add coordinated rate/acceleration constraints and a STOP/power-removal plan before physical tests; unloaded visualization is the first gate.

### Step 6 — FK using the same geometry, if actual angles are measurable

Given *measured* `θᵢ`, first calculate each known horn tip:

```text
Hᵢ (known horn tip) = Bᵢ + aᵢ[cos(θᵢ)uᵢ + sin(θᵢ)nᵢ]
```

For a guessed pose `T, R`, calculate each top joint and residual:

```text
Pᵢ (guessed top joint) = T + R pᵢ
rᵢ (rod-length-squared error) = |Pᵢ − Hᵢ|² − dᵢ²
```

A pseudocode sketch (not a drop-in implementation) shows how the pieces connect:

```text
poseGuess = lastKnownPose, or a known neutral pose on startup
repeat up to a fixed iteration limit:
    r[0..5] = rod residuals at poseGuess
    if all rod-length errors meet tolerance: return poseGuess
    J = finite-difference 6×6 derivatives of r with respect to
        [x, y, z, rotationX, rotationY, rotationZ]
    solve a damped linear step J Δpose ≈ −r
    limit the step, apply it to poseGuess, and check workspace
return failure, NOT a fabricated pose or a blind hardware command
```

The Jacobian entry `J[i,j]` answers, “if pose coordinate `j` changes a little, how much does leg `i`’s residual change?” For example, one finite-difference estimate is `J[i,j] ≈ (rᵢ(pose + ε along j) − rᵢ(pose)) / ε`. Use a translation perturbation in millimetres and an angular perturbation in radians; scale or normalize the columns appropriately, since position and rotation have different units. Damping helps near poorly conditioned poses, but does not certify them safe. Use the last valid pose to stay on the intended solution branch, and reject failure or unexpected jumps.[2]

### Recommended learning exercise

First implement the single-leg horn tip equation and draw its arc. Then implement `P = T + R p` for one top joint, and verify your IK angle reconstructs a 95 mm rod. Only after that loop over six legs, add pose feasibility checks, and finally explore FK with actual feedback. Keeping simulation, conditioned pose, rotary IK, servo calibration, and physical output separate preserves the project’s architecture gate.

## 4. Practice problems — try these before opening the answers

Work in millimetres, with Unity Y up: `n = (0, 1, 0)`. Use degrees for your reported angles, but convert to radians before calling `sin`, `cos`, or `asin` in most programming languages. A calculator or a short script is fine. Each problem builds on the preceding one; none involves sending a command to hardware.

### Problem 1 — Transform a platform joint

The platform has no rotation (`R = identity`). Its origin is `T = (2, 100, 3) mm`, and one top joint has local position `p = (10, 0, -5) mm`. Its corresponding servo shaft is `B = (0, 0, 0) mm`.

- Find `P (top joint in base coordinates) = T + R p`.
- Find `L (shaft-to-top vector) = P - B`.
- In a Unity script, which expression rotates `p` before adding `T`?

Hint: an identity rotation leaves `p` unchanged.

### Problem 2 — Locate a horn tip

Use `B = (0, 0, 0) mm`, `u = (1, 0, 0)`, `n = (0, 1, 0)`, horn length `a = 20 mm`, and horn angle `θ = 30°`.

- Compute `H (horn tip) = B + a[cos(θ)u + sin(θ)n]` to three decimal places.
- If `θ` were `0°`, where would the tip be?
- Which coordinate cannot change as this particular horn rotates?

Hint: `cos(30°) ≈ 0.8660254`, `sin(30°) = 0.5`.

### Problem 3 — Work backward from a top joint (single-leg IK)

Keep the shaft, directions, and horn length from Problem 2. Set the desired top joint to `P = (0, 100, 0) mm`. Give the fixed rod a *squared* length `d² = 8400 mm²` (`d ≈ 91.652 mm`).

- Compute `L (shaft-to-top vector) = P - B`.
- Compute `e (vertical contribution) = 2a(L · n)`.
- Compute `f (horizontal contribution) = 2a(L · u)`.
- Compute `g (length requirement) = |L|² + a² - d²`.
- Compute `ρ (maximum combined contribution) = √(e² + f²)`. Is `|g| ≤ ρ`?
- Solve `e sin(θ) + f cos(θ) = g` for *both* angles between `0°` and `180°`. Reconstruct `H` for one of them and verify `|P-H|² = d²`.

Hint: this example makes `f = 0` so the angle equation becomes especially simple.

### Problem 4 — Spot an unreachable target before commanding anything

Keep `B`, `P`, `u`, `n`, and `a` from Problem 3, but replace the rod length with `d = 60 mm`.

- Recompute `g (length requirement)` and `ρ (maximum contribution)`.
- Can a real horn angle satisfy the rod constraint? Explain using the inequality, not just a calculator error.
- Should a solver clamp the argument to `asin` and issue a servo command in this case? Why not?

### Problem 5 — Choose a physical branch

Return to the geometry of Problem 3. Suppose the intended assembly has a *geometric* servo-angle range from `-60°` to `+60°` and is calibrated on the low-angle branch.

- Which of the two IK angles is admissible?
- If the servo is physically flipped and its pulse direction is reversed, should you change `P = T + R p` or the downstream angle-to-servo calibration? Explain.
- Name one additional physical check required even after all six legs have admissible angles.

### Problem 6 — Calculate one FK residual

Assume you *measured* the horn angle `θ = 30°` on the geometry of Problem 3, so its tip `H` is known. The rod still has `d² = 8400 mm²`.

- At the candidate joint position `P = (0, 100, 0) mm`, calculate `r = |P-H|² - d²`.
- At the slightly wrong candidate `P' = (0, 101, 0) mm`, calculate `r'`.
- Does a single zero residual establish the pose of an entire six-leg platform? What remains to be solved together?

### Problem 7 — Small program/visualization challenge

In an isolated script (not the hardware sender), implement `HornTip(B, u, a, theta)` and `RodLengthError(P, H, d) = |P-H| - d`. Draw or log the horn tips and rods for the six-leg example in Section 3. Change only heave from `+5 mm` to `+6 mm`, rerun IK, and record which six angles changed and whether all reconstructed rod errors stay near zero. Then deliberately set leg 1's rod length to `5 mm` and verify that the solver *rejects the entire pose* rather than publishing five fresh angles and one stale angle.

Self-check: each accepted leg’s rod error should be close to `0 mm`; the deliberately impossible case should return failure. Keep this exercise in simulation only. The exact angle changes depend on the altered geometry you choose.

<details>
<summary>Answer checks for Problems 1–6 (open after attempting them)</summary>

1. `P = (12, 100, -2) mm` and `L = (12, 100, -2) mm`. Unity expression: `T + R * p` for a `Quaternion R` and `Vector3 p`.
2. `H ≈ (17.321, 10.000, 0) mm`; at `0°`, `H = (20, 0, 0) mm`. Its Z coordinate remains zero.
3. `L = (0, 100, 0) mm`; `e = 4000 mm²`, `f = 0 mm²`, `g = 2000 mm²`, `ρ = 4000 mm²`, and `g/ρ = 0.5`. Angles: `30°` and `150°`. At `30°`, `H ≈ (17.321, 10, 0) mm` and `|P-H|² = 17.321² + 90² ≈ 8400 mm²` (use the unrounded value for an exact check).
4. `g = 6800 mm²`; `ρ = 4000 mm²`. Since `|g| > ρ`, no horn angle works. Clamping would disguise an unreachable target as a valid command.
5. `30°` is inside the allowed range; `150°` is not. Handle the flipped servo at the calibration/output mapping, not by altering the desired pose. Also check joint articulation, interference/collisions, continuity, or another physical constraint.
6. At `P`, `r = 0 mm²`. At `P'`, `r' = 181 mm²`. One rod only constrains its own distance; the six residuals and one shared six-degree-of-freedom platform pose must be solved together.

</details>

## Sources

[1] https://raw.org/research/inverse-kinematics-of-a-stewart-platform
[2] https://pmc.ncbi.nlm.nih.gov/articles/PMC9269243
[3] https://portfolio.limerobotlab.com/wpi/rotary-stewart-platform-kinematics
