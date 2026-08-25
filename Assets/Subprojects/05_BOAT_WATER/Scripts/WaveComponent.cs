using System;
using UnityEngine;

namespace OceanMotion.Subproject05
{
    [Serializable]
    public struct WaveComponent
    {
        [Min(0f)] public float amplitude;
        [Min(0.01f)] public float wavelength;
        public Vector2 direction;
        public float speed;
        public float phaseOffsetRadians;

        public WaveComponent(
            float amplitude,
            float wavelength,
            Vector2 direction,
            float speed,
            float phaseOffsetRadians
        )
        {
            this.amplitude = amplitude;
            this.wavelength = wavelength;
            this.direction = direction;
            this.speed = speed;
            this.phaseOffsetRadians = phaseOffsetRadians;
        }
    }
}
