# Subproject 05 — Boat Water

## Current stage: shared wave source and visible surface

This stage intentionally stops before boat physics and buoyancy. It establishes one deterministic wave source that both graphics and later physics sampling can use.

## Scene

Open `Scenes/SP05_BoatWater.unity` and enter Play mode. The `Water Surface` object contains:

- `WaveField` — owns the shared wave parameters and samples world-space water height.
- `WaveSurface` — generates and deforms the visible grid by calling `WaveField.SampleHeight` for every vertex.
- `MeshFilter` and `MeshRenderer` — display the generated grid with `Materials/SP05_Water.mat`.

Future buoyancy code must receive a reference to this same `WaveField` and call `SampleHeight(worldPosition, simulationTime)`. Do not duplicate the sine-wave equation in boat code or a separate shader.

## Wave equation

Each component contributes:

```text
amplitude * sin((2π / wavelength) * (dot(worldXZ, normalizedDirection) - speed * time) + phaseOffset)
```

The final height is the water object's world Y position plus its base-height offset and the sum of both components.

## Initial Inspector values

| Component | Amplitude | Wavelength | Direction XZ | Speed | Phase offset |
|---|---:|---:|---|---:|---:|
| Primary | 0.15 | 8.0 | (1.0, 0.0) | 0.8 | 0.0 |
| Secondary | 0.06 | 5.0 | (0.35, 0.94) | 0.45 | 0.7 rad |

These are deliberately low-amplitude starting values, not final tuned settings.

## Step-by-step validation

1. Set the secondary amplitude to `0` and enter Play mode.
2. Confirm the primary wave is gentle, bounded, and travels in the expected direction.
3. Restore the secondary amplitude to `0.06`.
4. Confirm the crossed pattern remains gentle and bounded.
5. Change one Inspector category at a time and verify that amplitude, wavelength, direction, and speed have the expected effect.
6. Do not add buoyancy until the visible one-wave and two-wave cases are accepted.

## Automated verification

The Edit Mode tests in `Tests/EditMode/WaveSamplerTests.cs` cover:

- Flat/zero-amplitude water
- A predictable one-wave crest
- Two-component summation
- Wave travel from configured speed
- `WaveField` delegation to the shared sampler
- Visible mesh vertices matching `WaveField` at identical world XZ and time inputs

Manual Play-mode visual acceptance is still required before beginning buoyancy.
