using UnityEngine;

namespace OceanMotion.Subproject05
{
    public enum WavePreset
    {
        Calm,
        Bow,
        Side,
        Crossed
    }

    public class WaveField : MonoBehaviour
    {
        [Header("Shared wave height")]
        [SerializeField] private float baseHeightOffset;

        [Header("Preset")]
        [SerializeField] private WavePreset activePreset = WavePreset.Bow;
        [SerializeField, HideInInspector] private WavePreset lastAppliedPreset =
            (WavePreset)(-1);

        [Header("Primary wave")]
        [SerializeField] private WaveComponent primaryWave = new WaveComponent(
            amplitude: 0.15f,
            wavelength: 30f,
            direction: new Vector2(0f, 1f),
            speed: 0.8f,
            phaseOffsetRadians: 0f
        );

        [Header("Secondary wave")]
        [SerializeField] private WaveComponent secondaryWave = new WaveComponent(
            amplitude: 0f,
            wavelength: 30f,
            direction: new Vector2(1f, 0f),
            speed: 0.65f,
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

        public WavePreset ActivePreset => activePreset;

        public string ActivePresetName => activePreset.ToString();

        private void Awake()
        {
            ApplyPreset(activePreset);
        }

        public void SetPreset(WavePreset preset)
        {
            activePreset = preset;
            ApplyPreset(preset);
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

        private void ApplyPreset(WavePreset preset)
        {
            switch (preset)
            {
                case WavePreset.Calm:
                    primaryWave = CreateWave(
                        0f, 30f, new Vector2(0f, 1f), 0.8f, 0f);
                    secondaryWave = CreateWave(
                        0f, 30f, new Vector2(1f, 0f), 0.65f, 0.7f);
                    break;

                case WavePreset.Bow:
                    primaryWave = CreateWave(
                        0.5f, 20, new Vector2(0f, 1f), 2f, 0f);
                    secondaryWave = CreateWave(
                        0f, 20f, new Vector2(1f, 0f), 0.65f, 0.7f);
                    break;

                case WavePreset.Side:
                    primaryWave = CreateWave(
                        0.5f, 20f, new Vector2(1f, 0f), 2f, 0f);
                    secondaryWave = CreateWave(
                        0f, 20f, new Vector2(0f, 1f), 0.65f, 0.7f);
                    break;

                case WavePreset.Crossed:
                    primaryWave = CreateWave(
                        0.5f, 20f, new Vector2(0f, 1f), 2f, 0f);
                    secondaryWave = CreateWave(
                        0.5f, 20f, new Vector2(1f, 0f), 1.5f, 0.7f);
                    break;
            }

            lastAppliedPreset = preset;
        }

        private static WaveComponent CreateWave(
            float amplitude,
            float wavelength,
            Vector2 direction,
            float speed,
            float phaseOffsetRadians
        )
        {
            return new WaveComponent(
                amplitude,
                wavelength,
                direction,
                speed,
                phaseOffsetRadians
            );
        }

        private void OnValidate()
        {
            if (activePreset != lastAppliedPreset)
            {
                ApplyPreset(activePreset);
            }

            primaryWave.amplitude = Mathf.Max(0f, primaryWave.amplitude);
            primaryWave.wavelength = Mathf.Max(0.01f, primaryWave.wavelength);
            secondaryWave.amplitude = Mathf.Max(0f, secondaryWave.amplitude);
            secondaryWave.wavelength = Mathf.Max(0.01f, secondaryWave.wavelength);
        }
    }
}