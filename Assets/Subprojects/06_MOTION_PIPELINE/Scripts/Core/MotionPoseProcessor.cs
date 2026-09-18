using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// Pure, deterministic pose conditioning with explicit time steps.
    /// </summary>
    public sealed class MotionPoseProcessor
    {
        private MotionPipelineSettings settings;
        private bool settingsAreValid;
        private Vector3 previousTranslation;
        private Vector3 previousRotation;
        private Vector3 previousTranslationVelocity;
        private Vector3 previousRotationVelocity;
        private Vector3 previousPublishedTranslation;
        private Vector3 previousPublishedRotation;
        private Vector3 previousPublishedTranslationVelocity;
        private Vector3 previousPublishedRotationVelocity;

        public MotionPoseProcessor(MotionPipelineSettings settings)
        {
            UpdateSettings(settings);
        }

        public void UpdateSettings(MotionPipelineSettings settings)
        {
            bool smoothingWasDisabled =
                this.settings.SmoothingEnabled && !settings.SmoothingEnabled;
            this.settings = settings;
            settingsAreValid = AreSettingsValid(settings);

            if (smoothingWasDisabled)
            {
                previousTranslation = previousPublishedTranslation;
                previousRotation = previousPublishedRotation;
                previousTranslationVelocity = previousPublishedTranslationVelocity;
                previousRotationVelocity = previousPublishedRotationVelocity;
            }
        }

        public MotionPoseCommand Process(MotionPoseSample sample, float deltaTimeSeconds)
        {
            if (!settingsAreValid)
                return CreateRejectedCommand(sample, MotionLimitFlags.InvalidSettings);

            if (!sample.IsValid ||
                !IsFinite(sample.TranslationMeters) ||
                !IsFinite(sample.RotationDegrees) ||
                !IsFinite(sample.SourceTimeSeconds))
            {
                return CreateRejectedCommand(sample, MotionLimitFlags.InvalidInput);
            }

            if (!IsFinite(deltaTimeSeconds) || deltaTimeSeconds <= 0f)
            {
                return CreateRejectedCommand(sample, MotionLimitFlags.InvalidDeltaTime);
            }

            Vector3 scaledTranslation =
                Vector3.Scale(sample.TranslationMeters, settings.TranslationGain);
            Vector3 scaledRotation =
                Vector3.Scale(sample.RotationDegrees, settings.RotationGain);
            Vector3 clampedTranslation =
                ClampSymmetric(scaledTranslation, settings.TranslationLimitMeters);
            Vector3 clampedRotation =
                ClampSymmetric(scaledRotation, settings.RotationLimitDegrees);
            MotionLimitFlags flags = MotionLimitFlags.None;

            if (clampedTranslation != scaledTranslation)
                flags |= MotionLimitFlags.TranslationClamp;
            if (clampedRotation != scaledRotation)
                flags |= MotionLimitFlags.RotationClamp;

            Vector3 velocityLimitedTranslation = AdvancePerAxis(
                previousTranslation,
                clampedTranslation,
                settings.TranslationVelocityLimitMetersPerSecond,
                settings.TranslationAccelerationLimitMetersPerSecondSquared,
                deltaTimeSeconds,
                ref previousTranslationVelocity,
                out bool translationVelocityLimited,
                out bool translationAccelerationLimited);
            Vector3 velocityLimitedRotation = AdvancePerAxis(
                previousRotation,
                clampedRotation,
                settings.RotationVelocityLimitDegreesPerSecond,
                settings.RotationAccelerationLimitDegreesPerSecondSquared,
                deltaTimeSeconds,
                ref previousRotationVelocity,
                out bool rotationVelocityLimited,
                out bool rotationAccelerationLimited);

            if (translationVelocityLimited)
                flags |= MotionLimitFlags.TranslationVelocityLimit;
            if (rotationVelocityLimited)
                flags |= MotionLimitFlags.RotationVelocityLimit;
            if (translationAccelerationLimited)
                flags |= MotionLimitFlags.TranslationAccelerationLimit;
            if (rotationAccelerationLimited)
                flags |= MotionLimitFlags.RotationAccelerationLimit;

            Vector3 conditionedTranslation = ClampSymmetric(
                velocityLimitedTranslation,
                settings.TranslationLimitMeters);
            Vector3 conditionedRotation = ClampSymmetric(
                velocityLimitedRotation,
                settings.RotationLimitDegrees);
            previousTranslationVelocity =
                (conditionedTranslation - previousTranslation) / deltaTimeSeconds;
            previousRotationVelocity =
                (conditionedRotation - previousRotation) / deltaTimeSeconds;
            previousTranslation = conditionedTranslation;
            previousRotation = conditionedRotation;

            Vector3 smoothedTranslation = settings.SmoothingEnabled
                ? SmoothTowards(
                    previousPublishedTranslation,
                    conditionedTranslation,
                    settings.TranslationSmoothingTimeSeconds,
                    deltaTimeSeconds)
                : conditionedTranslation;
            Vector3 smoothedRotation = settings.SmoothingEnabled
                ? SmoothTowards(
                    previousPublishedRotation,
                    conditionedRotation,
                    settings.RotationSmoothingTimeSeconds,
                    deltaTimeSeconds)
                : conditionedRotation;

            Vector3 publicationLimitedTranslation = smoothedTranslation;
            Vector3 publicationLimitedRotation = smoothedRotation;
            if (settings.SmoothingEnabled)
            {
                publicationLimitedTranslation = AdvancePerAxis(
                    previousPublishedTranslation,
                    smoothedTranslation,
                    settings.TranslationVelocityLimitMetersPerSecond,
                    settings.TranslationAccelerationLimitMetersPerSecondSquared,
                    deltaTimeSeconds,
                    ref previousPublishedTranslationVelocity,
                    out bool publishedTranslationVelocityLimited,
                    out bool publishedTranslationAccelerationLimited,
                    false);
                publicationLimitedRotation = AdvancePerAxis(
                    previousPublishedRotation,
                    smoothedRotation,
                    settings.RotationVelocityLimitDegreesPerSecond,
                    settings.RotationAccelerationLimitDegreesPerSecondSquared,
                    deltaTimeSeconds,
                    ref previousPublishedRotationVelocity,
                    out bool publishedRotationVelocityLimited,
                    out bool publishedRotationAccelerationLimited,
                    false);

                if (publishedTranslationVelocityLimited)
                    flags |= MotionLimitFlags.TranslationVelocityLimit;
                if (publishedRotationVelocityLimited)
                    flags |= MotionLimitFlags.RotationVelocityLimit;
                if (publishedTranslationAccelerationLimited)
                    flags |= MotionLimitFlags.TranslationAccelerationLimit;
                if (publishedRotationAccelerationLimited)
                    flags |= MotionLimitFlags.RotationAccelerationLimit;
            }

            // Re-clamp before publication so no conditioning step can move the
            // command outside the configured pose envelope.
            Vector3 publishedTranslation = ClampSymmetric(
                publicationLimitedTranslation,
                settings.TranslationLimitMeters);
            Vector3 publishedRotation = ClampSymmetric(
                publicationLimitedRotation,
                settings.RotationLimitDegrees);
            previousPublishedTranslationVelocity =
                (publishedTranslation - previousPublishedTranslation) / deltaTimeSeconds;
            previousPublishedRotationVelocity =
                (publishedRotation - previousPublishedRotation) / deltaTimeSeconds;
            previousPublishedTranslation = publishedTranslation;
            previousPublishedRotation = publishedRotation;

            return new MotionPoseCommand(
                previousPublishedTranslation,
                previousPublishedRotation,
                sample.Sequence,
                sample.SourceTimeSeconds,
                sample.IsValid,
                flags);
        }

        private MotionPoseCommand CreateRejectedCommand(
            MotionPoseSample sample,
            MotionLimitFlags reason)
        {
            return new MotionPoseCommand(
                previousPublishedTranslation,
                previousPublishedRotation,
                sample.Sequence,
                IsFinite(sample.SourceTimeSeconds) ? sample.SourceTimeSeconds : 0d,
                false,
                reason);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool AreSettingsValid(MotionPipelineSettings value)
        {
            return IsFinite(value.TranslationGain) &&
                IsFinite(value.RotationGain) &&
                IsFinite(value.TranslationLimitMeters) &&
                IsFinite(value.RotationLimitDegrees) &&
                IsFinite(value.TranslationVelocityLimitMetersPerSecond) &&
                IsFinite(value.RotationVelocityLimitDegreesPerSecond) &&
                IsFinite(value.TranslationAccelerationLimitMetersPerSecondSquared) &&
                IsFinite(value.RotationAccelerationLimitDegreesPerSecondSquared) &&
                IsFinite(value.TranslationSmoothingTimeSeconds) &&
                value.TranslationSmoothingTimeSeconds >= 0f &&
                IsFinite(value.RotationSmoothingTimeSeconds) &&
                value.RotationSmoothingTimeSeconds >= 0f;
        }

        private static Vector3 SmoothTowards(
            Vector3 current,
            Vector3 target,
            float timeSeconds,
            float deltaTimeSeconds)
        {
            if (!IsFinite(timeSeconds) || timeSeconds <= 0f)
                return target;

            float blend = 1f - Mathf.Exp(-deltaTimeSeconds / timeSeconds);
            return Vector3.LerpUnclamped(current, target, blend);
        }

        private static Vector3 AdvancePerAxis(
            Vector3 current,
            Vector3 target,
            Vector3 velocityLimit,
            Vector3 accelerationLimit,
            float deltaTimeSeconds,
            ref Vector3 currentVelocity,
            out bool velocityLimited,
            out bool accelerationLimited,
            bool applyTargetBraking = true)
        {
            Vector3 nextVelocity = Vector3.zero;
            bool xVelocityLimited;
            bool yVelocityLimited;
            bool zVelocityLimited;
            bool xAccelerationLimited;
            bool yAccelerationLimited;
            bool zAccelerationLimited;
            Vector3 result = new Vector3(
                AdvanceAxis(current.x, target.x, currentVelocity.x, velocityLimit.x,
                    accelerationLimit.x, deltaTimeSeconds, out nextVelocity.x,
                    out xVelocityLimited, out xAccelerationLimited, applyTargetBraking),
                AdvanceAxis(current.y, target.y, currentVelocity.y, velocityLimit.y,
                    accelerationLimit.y, deltaTimeSeconds, out nextVelocity.y,
                    out yVelocityLimited, out yAccelerationLimited, applyTargetBraking),
                AdvanceAxis(current.z, target.z, currentVelocity.z, velocityLimit.z,
                    accelerationLimit.z, deltaTimeSeconds, out nextVelocity.z,
                    out zVelocityLimited, out zAccelerationLimited, applyTargetBraking));

            currentVelocity = nextVelocity;
            velocityLimited = xVelocityLimited || yVelocityLimited || zVelocityLimited;
            accelerationLimited =
                xAccelerationLimited || yAccelerationLimited || zAccelerationLimited;
            return result;
        }

        private static float AdvanceAxis(
            float current,
            float target,
            float currentVelocity,
            float configuredVelocityLimit,
            float configuredAccelerationLimit,
            float deltaTimeSeconds,
            out float nextVelocity,
            out bool velocityLimited,
            out bool accelerationLimited,
            bool applyTargetBraking)
        {
            if (deltaTimeSeconds <= 0f)
            {
                nextVelocity = currentVelocity;
                velocityLimited = false;
                accelerationLimited = target != current;
                return current;
            }

            float unrestrictedVelocity = (target - current) / deltaTimeSeconds;
            float velocityLimit = Mathf.Abs(configuredVelocityLimit);
            float desiredVelocity = float.IsPositiveInfinity(velocityLimit)
                ? unrestrictedVelocity
                : Mathf.Clamp(unrestrictedVelocity, -velocityLimit, velocityLimit);
            velocityLimited = desiredVelocity != unrestrictedVelocity;

            float accelerationLimit = Mathf.Abs(configuredAccelerationLimit);
            if (float.IsPositiveInfinity(accelerationLimit))
            {
                nextVelocity = desiredVelocity;
            }
            else
            {
                float accelerationStep = accelerationLimit * deltaTimeSeconds;
                if (applyTargetBraking)
                {
                    // Cap the desired speed so the axis can brake to zero within
                    // the remaining distance instead of hitting the pose clamp
                    // with hidden outward velocity. This is the discrete-time
                    // solution of d >= v*dt + v^2/(2a).
                    float distance = Mathf.Abs(target - current);
                    float brakingSafeSpeed = Mathf.Max(
                        0f,
                        Mathf.Sqrt(
                            accelerationStep * accelerationStep +
                            2f * accelerationLimit * distance) -
                        accelerationStep);
                    desiredVelocity = Mathf.Sign(desiredVelocity) * Mathf.Min(
                        Mathf.Abs(desiredVelocity),
                        brakingSafeSpeed);
                }

                nextVelocity = currentVelocity + Mathf.Clamp(
                    desiredVelocity - currentVelocity,
                    -accelerationStep,
                    accelerationStep);
            }

            float accelerationLimitedVelocity = nextVelocity;
            if (!float.IsPositiveInfinity(velocityLimit))
            {
                nextVelocity = Mathf.Clamp(
                    nextVelocity,
                    -velocityLimit,
                    velocityLimit);
                if (nextVelocity != accelerationLimitedVelocity)
                    velocityLimited = true;
            }

            accelerationLimited = accelerationLimitedVelocity != desiredVelocity;
            return current + nextVelocity * deltaTimeSeconds;
        }

        private static Vector3 ClampSymmetric(Vector3 value, Vector3 configuredLimit)
        {
            Vector3 limit = new Vector3(
                Mathf.Abs(configuredLimit.x),
                Mathf.Abs(configuredLimit.y),
                Mathf.Abs(configuredLimit.z));

            return new Vector3(
                Mathf.Clamp(value.x, -limit.x, limit.x),
                Mathf.Clamp(value.y, -limit.y, limit.y),
                Mathf.Clamp(value.z, -limit.z, limit.z));
        }
    }
}
