using UnityEngine;
using System;
using System.Collections.Generic;

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
        public const float MaximumTransitionSeconds=30f;
        private WaveComponent[] transitionFrom;
        private float transitionStartedAt,transitionDuration;
        public float TransitionDurationSeconds=>transitionDuration;
        public float TransitionProgress=>TransitionProgressAt(Time.time);
        public bool IsTransitioning=>transitionFrom!=null && TransitionProgress<1;
        public float TransitionProgressAt(float simulationTime)=>transitionDuration>0 ? Mathf.Clamp01((simulationTime-transitionStartedAt)/transitionDuration) : 1;
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
            set { transitionFrom=null;transitionDuration=0;primaryWave=value; }
        }

        public WaveComponent SecondaryWave
        {
            get => secondaryWave;
            set { transitionFrom=null;transitionDuration=0;secondaryWave=value; }
        }

        public WavePreset ActivePreset => activePreset;

        public string ActivePresetName => activePreset.ToString();

        private void Awake()
        {
            ApplyPreset(activePreset);
        }

        public void SetPreset(WavePreset preset)
        {
            SetPreset(preset,0); // Existing scenes retain instantaneous preset semantics.
        }

        /// <summary>Cross-fade complete wave fields on the simulation clock, preserving phase.
        /// Retargeting snapshots the current blend, so another click cannot snap the surface.</summary>
        public void SetPreset(WavePreset preset,float transitionSeconds)
            =>SetPreset(preset,transitionSeconds,Time.time);

        // Explicit simulation clock supports deterministic tests and replay without a render-update dependency.
        public void SetPreset(WavePreset preset,float transitionSeconds,float simulationTime)
        {
            if(!Enum.IsDefined(typeof(WavePreset),preset))throw new ArgumentOutOfRangeException(nameof(preset));
            if(float.IsNaN(transitionSeconds) || float.IsInfinity(transitionSeconds) || transitionSeconds<0 || transitionSeconds>MaximumTransitionSeconds)
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
            if(float.IsNaN(simulationTime) || float.IsInfinity(simulationTime) || simulationTime<0)
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            if(transitionSeconds>0 && preset==activePreset && transitionFrom!=null && TransitionProgressAt(simulationTime)<1)return;
            var from=transitionSeconds>0 ? SnapshotWaves(simulationTime) : null;
            activePreset = preset;
            ApplyPreset(preset);
            transitionFrom=from;transitionStartedAt=simulationTime;transitionDuration=transitionSeconds;
        }

        public float SampleHeight(Vector3 worldPosition, float simulationTime)
        {
            float baseHeight = transform.position.y + baseHeightOffset;

            float target=WaveSampler.SampleHeight(
                new Vector2(worldPosition.x, worldPosition.z),
                simulationTime,
                baseHeight,
                primaryWave,
                secondaryWave
            );
            float blend=TransitionProgressAt(simulationTime);
            if(transitionFrom==null || blend>=1)return target;
            float previous=WaveSampler.SampleHeight(new Vector2(worldPosition.x,worldPosition.z),simulationTime,baseHeight,transitionFrom);
            return Mathf.Lerp(previous,target,blend);
        }

        private WaveComponent[] SnapshotWaves(float time)
        {
            var components=new List<WaveComponent>();
            float blend=TransitionProgressAt(time);
            if(transitionFrom!=null && blend<1)foreach(var wave in transitionFrom)AddWeighted(components,wave,1-blend);
            else blend=1;
            AddWeighted(components,primaryWave,blend);AddWeighted(components,secondaryWave,blend);
            return components.ToArray();
        }
        private static void AddWeighted(List<WaveComponent> components,WaveComponent wave,float weight)
        {
            if(weight<=0 || wave.amplitude==0)return;
            wave.amplitude*=weight;
            // Merge identical travelling waves. Repeated preset clicks do not grow a chain of blends.
            for(int i=0;i<components.Count;i++) {
                var old=components[i];
                if(old.wavelength==wave.wavelength && old.direction.Equals(wave.direction) && old.speed==wave.speed && old.phaseOffsetRadians==wave.phaseOffsetRadians) {
                    old.amplitude+=wave.amplitude;components[i]=old;return;
                }
            }
            components.Add(wave);
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
            transitionFrom=null;transitionDuration=0; // Direct edits retain immediate semantics.
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
