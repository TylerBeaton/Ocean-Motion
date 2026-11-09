using UnityEngine;

namespace OceanMotion.Subproject05
{
    public static class WaveSampler
    {
        // Avoid a params-array allocation for the two-component field sampled by every mesh vertex.
        public static float SampleHeight(Vector2 worldXZ,float time,float baseHeight,WaveComponent first,WaveComponent second)
            =>baseHeight+SampleDisplacement(worldXZ,time,first)+SampleDisplacement(worldXZ,time,second);
        public static float SampleHeight(
            Vector2 worldXZ,
            float time,
            float baseHeight,
            params WaveComponent[] waves
        )
        {
            float height = baseHeight;

            if (waves == null || waves.Length == 0)
            {
                return height;
            }

            foreach (WaveComponent wave in waves)
            {
                height+=SampleDisplacement(worldXZ,time,wave);
            }
            return height;
        }
        private static float SampleDisplacement(Vector2 worldXZ,float time,WaveComponent wave)
        {
            float safeWavelength = Mathf.Max(0.01f, wave.wavelength);
            Vector2 direction = wave.direction.sqrMagnitude > 0.000001f
                ? wave.direction.normalized
                : Vector2.right;
            float waveNumber = (2f * Mathf.PI) / safeWavelength;
            float distanceAlongWave = Vector2.Dot(worldXZ, direction);
            float phase = waveNumber * (distanceAlongWave - wave.speed * time)
                + wave.phaseOffsetRadians;

            return wave.amplitude*Mathf.Sin(phase);
        }
    }
}
