using System;
using UnityEngine;

namespace OceanMotion.Subproject06
{
    [Serializable]
    public struct MotionPipelineSettings
    {
        private static readonly Vector3 Unlimited = new Vector3(
            float.MaxValue,
            float.MaxValue,
            float.MaxValue);

        [SerializeField] private Vector3 translationGain;
        [SerializeField] private Vector3 rotationGain;
        [SerializeField] private Vector3 translationLimitMeters;
        [SerializeField] private Vector3 rotationLimitDegrees;
        [SerializeField] private Vector3 translationVelocityLimitMetersPerSecond;
        [SerializeField] private Vector3 rotationVelocityLimitDegreesPerSecond;
        [SerializeField] private Vector3 translationAccelerationLimitMetersPerSecondSquared;
        [SerializeField] private Vector3 rotationAccelerationLimitDegreesPerSecondSquared;
        [SerializeField] private bool smoothingEnabled;
        [SerializeField, Min(0f)] private float translationSmoothingTimeSeconds;
        [SerializeField, Min(0f)] private float rotationSmoothingTimeSeconds;

        public Vector3 TranslationGain => translationGain;
        public Vector3 RotationGain => rotationGain;
        public Vector3 TranslationLimitMeters => translationLimitMeters;
        public Vector3 RotationLimitDegrees => rotationLimitDegrees;
        public Vector3 TranslationVelocityLimitMetersPerSecond =>
            translationVelocityLimitMetersPerSecond;
        public Vector3 RotationVelocityLimitDegreesPerSecond =>
            rotationVelocityLimitDegreesPerSecond;
        public Vector3 TranslationAccelerationLimitMetersPerSecondSquared =>
            translationAccelerationLimitMetersPerSecondSquared;
        public Vector3 RotationAccelerationLimitDegreesPerSecondSquared =>
            rotationAccelerationLimitDegreesPerSecondSquared;
        public bool SmoothingEnabled => smoothingEnabled;
        public float TranslationSmoothingTimeSeconds => translationSmoothingTimeSeconds;
        public float RotationSmoothingTimeSeconds => rotationSmoothingTimeSeconds;

        public MotionPipelineSettings(Vector3 translationGain, Vector3 rotationGain)
            : this(
                translationGain,
                rotationGain,
                Unlimited,
                Unlimited)
        {
        }

        public MotionPipelineSettings(
            Vector3 translationGain,
            Vector3 rotationGain,
            Vector3 translationLimitMeters,
            Vector3 rotationLimitDegrees)
            : this(
                translationGain,
                rotationGain,
                translationLimitMeters,
                rotationLimitDegrees,
                Unlimited,
                Unlimited)
        {
        }

        public MotionPipelineSettings(
            Vector3 translationGain,
            Vector3 rotationGain,
            Vector3 translationLimitMeters,
            Vector3 rotationLimitDegrees,
            Vector3 translationVelocityLimitMetersPerSecond,
            Vector3 rotationVelocityLimitDegreesPerSecond)
            : this(
                translationGain,
                rotationGain,
                translationLimitMeters,
                rotationLimitDegrees,
                translationVelocityLimitMetersPerSecond,
                rotationVelocityLimitDegreesPerSecond,
                Unlimited,
                Unlimited)
        {
        }

        public MotionPipelineSettings(
            Vector3 translationGain,
            Vector3 rotationGain,
            Vector3 translationLimitMeters,
            Vector3 rotationLimitDegrees,
            Vector3 translationVelocityLimitMetersPerSecond,
            Vector3 rotationVelocityLimitDegreesPerSecond,
            Vector3 translationAccelerationLimitMetersPerSecondSquared,
            Vector3 rotationAccelerationLimitDegreesPerSecondSquared)
            : this(
                translationGain,
                rotationGain,
                translationLimitMeters,
                rotationLimitDegrees,
                translationVelocityLimitMetersPerSecond,
                rotationVelocityLimitDegreesPerSecond,
                translationAccelerationLimitMetersPerSecondSquared,
                rotationAccelerationLimitDegreesPerSecondSquared,
                false,
                0.1f,
                0.1f)
        {
        }

        public MotionPipelineSettings(
            Vector3 translationGain,
            Vector3 rotationGain,
            Vector3 translationLimitMeters,
            Vector3 rotationLimitDegrees,
            Vector3 translationVelocityLimitMetersPerSecond,
            Vector3 rotationVelocityLimitDegreesPerSecond,
            Vector3 translationAccelerationLimitMetersPerSecondSquared,
            Vector3 rotationAccelerationLimitDegreesPerSecondSquared,
            bool smoothingEnabled,
            float translationSmoothingTimeSeconds,
            float rotationSmoothingTimeSeconds)
        {
            this.translationGain = translationGain;
            this.rotationGain = rotationGain;
            this.translationLimitMeters = translationLimitMeters;
            this.rotationLimitDegrees = rotationLimitDegrees;
            this.translationVelocityLimitMetersPerSecond =
                translationVelocityLimitMetersPerSecond;
            this.rotationVelocityLimitDegreesPerSecond =
                rotationVelocityLimitDegreesPerSecond;
            this.translationAccelerationLimitMetersPerSecondSquared =
                translationAccelerationLimitMetersPerSecondSquared;
            this.rotationAccelerationLimitDegreesPerSecondSquared =
                rotationAccelerationLimitDegreesPerSecondSquared;
            this.smoothingEnabled = smoothingEnabled;
            this.translationSmoothingTimeSeconds = translationSmoothingTimeSeconds;
            this.rotationSmoothingTimeSeconds = rotationSmoothingTimeSeconds;
        }

        public static MotionPipelineSettings Identity =>
            new MotionPipelineSettings(Vector3.one, Vector3.one);

        public static MotionPipelineSettings Default => new MotionPipelineSettings(
            Vector3.one,
            Vector3.one,
            new Vector3(0f, 0.25f, 0f),
            new Vector3(70f, 5f, 70f),
            new Vector3(0f, 0.75f, 0f),
            new Vector3(90f, 45f, 90f),
            new Vector3(0f, 3f, 0f),
            new Vector3(360f, 180f, 360f));
    }
}
