using UnityEngine;

namespace OceanMotion.Subproject05
{
    public class WaveField : MonoBehaviour
    {
        [Header("Shared wave height")]
        [SerializeField] private float baseHeightOffset;

        [Header("Primary wave")]
        [SerializeField] private WaveComponent primaryWave = new WaveComponent(
            amplitude: 0.15f,
            wavelength: 8f,
            direction: new Vector2(1f, 0f),
            speed: 0.8f,
            phaseOffsetRadians: 0f
        );

        [Header("Secondary wave")]
        [SerializeField] private WaveComponent secondaryWave = new WaveComponent(
            amplitude: 0.06f,
            wavelength: 5f,
            direction: new Vector2(0.35f, 0.94f),
            speed: 0.45f,
            phaseOffsetRadians: 0.7f
        );

        public float BaseHeightOffset
        {
            get => baseHeightOffset;
            set => baseHeightOffset = value;
        }

        public WaveComponent PrimaryWave
        {
            get => primaryWave;
            set => primaryWave = value;
        }

        public WaveComponent SecondaryWave
        {
            get => secondaryWave;
            set => secondaryWave = value;
        }

        public float SampleHeight(Vector3 worldPosition, float simulationTime)
        {
            float baseHeight = transform.position.y + baseHeightOffset;

            return WaveSampler.SampleHeight(
                new Vector2(worldPosition.x, worldPosition.z),
                simulationTime,
                baseHeight,
                primaryWave,
                secondaryWave
            );
        }

        private void OnValidate()
        {
            primaryWave.amplitude = Mathf.Max(0f, primaryWave.amplitude);
            primaryWave.wavelength = Mathf.Max(0.01f, primaryWave.wavelength);
            secondaryWave.amplitude = Mathf.Max(0f, secondaryWave.amplitude);
            secondaryWave.wavelength = Mathf.Max(0.01f, secondaryWave.wavelength);
        }
    }
}
