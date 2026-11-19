# Subproject 10 — Ocean Waves

## Required proof

Open `Scenes/SP10_OceanWaves.unity` in Unity 6.5 and enter Play Mode. The scene contains a water surface and a fixed camera. It starts on **Existing Waves**, using the **Crossed** preset from Subproject 05. Select **Water Surface → Ocean Surface → Wave Model → JONSWAP** to see the spectral waves with the same camera and grid material. The Scene view previews the selected surface at simulation time zero.

## Settings

| Setting | Starting value |
| --- | --- |
| Existing Crossed waves | Two 20 m sinusoids, each with 0.50 m amplitude, travelling along world +Z and +X at 2.0 and 1.5 m/s |
| JONSWAP significant wave height \(H_{m0}\) | 1.2 m |
| JONSWAP peak period | 3.6 s |
| JONSWAP travel heading and spread | 25° clockwise from world +Z toward +X, with component headings within ±35° |
| JONSWAP frequency band and seed | 32 seeded components from 0.55 to 2.15 times the peak frequency, peak enhancement γ = 3.3, seed 10 |
| Mesh and neutral | 72 m square with 97 × 97 vertices, 1 Unity unit = 1 m, neutral waterline at world Y = 0 |

The spectrum assigns each frequency bin a share of the surface-height variance. Components use deep-water dispersion to derive wavelength and travel speed from frequency. The mesh spacing is 0.75 m and the Inspector limits the peak period to at least 3.2 s so the shortest retained waves remain resolved on the grid. [JONSWAP report, §2.4](https://airsea.ucsd.edu/wp-content/uploads/sites/10/2019/10/Jonswap-Hasselmann1973.pdf)

## Dependency and reuse

This standalone scene reuses Subproject 05's water shader and wave code. Existing Waves uses its `WaveField` directly. JONSWAP generates the same `WaveComponent` data for the shared `WaveSampler`. `OceanSurface.SampleHeight(worldPosition, simulationTime)` exposes world-space Y at world XZ and an explicit time in seconds. The mesh uses that same entry point.

## Verification

- Unity 6000.5.7f1 compiled the project and opened the archived `SP09_BoatMotionIk` scene with zero missing scripts.
- Unity confirmed that the default scene has two root objects, generates displaced geometry, and samples the existing Crossed preset directly.
- The seeded spectrum produced 32 components satisfying deep-water dispersion and a computed \(H_{m0}\) of 1.200 m. A 60-second sample sweep stayed finite and within the component amplitude bound. Setting wave height to zero flattened the surface, and restoring the seed and height reproduced the same sample.
- Both modes rendered in Play Mode with the grid material. The JONSWAP preview ran for 15 seconds without runtime errors. The largest crest-to-trough range across the mesh in a sampled frame was 2.24 m.
