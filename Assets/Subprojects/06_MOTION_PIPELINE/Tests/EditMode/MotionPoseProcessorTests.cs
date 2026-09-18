using NUnit.Framework;
using UnityEngine;

namespace OceanMotion.Subproject06.Tests
{
    public class MotionPoseProcessorTests
    {
        [Test]
        public void Process_AppliesPerAxisGainsWithoutChangingRawSample()
        {
            var raw = new MotionPoseSample(
                new Vector3(0f, 0.4f, 0f),
                new Vector3(10f, -6f, 8f),
                sequence: 42,
                sourceTimeSeconds: 1.25d,
                isValid: true);
            var settings = new MotionPipelineSettings(
                translationGain: new Vector3(1f, 0.5f, 1f),
                rotationGain: new Vector3(0.5f, 0f, 0.25f));
            var processor = new MotionPoseProcessor(settings);

            MotionPoseCommand command = processor.Process(raw, deltaTimeSeconds: 1f);

            Assert.That(command.TranslationMeters, Is.EqualTo(new Vector3(0f, 0.2f, 0f)));
            Assert.That(command.RotationDegrees, Is.EqualTo(new Vector3(5f, 0f, 2f)));
            Assert.That(command.SourceSequence, Is.EqualTo(42));
            Assert.That(command.SourceTimeSeconds, Is.EqualTo(1.25d));
            Assert.That(command.IsValid, Is.True);
            Assert.That(command.LimitFlags, Is.EqualTo(MotionLimitFlags.None));
            Assert.That(raw.TranslationMeters, Is.EqualTo(new Vector3(0f, 0.4f, 0f)));
            Assert.That(raw.RotationDegrees, Is.EqualTo(new Vector3(10f, -6f, 8f)));
        }

        [Test]
        public void Process_ClampsScaledValuesToSymmetricPerAxisLimits()
        {
            var raw = new MotionPoseSample(
                new Vector3(0f, 2f, 0f),
                new Vector3(20f, -8f, -30f),
                sequence: 7,
                sourceTimeSeconds: 2d,
                isValid: true);
            var settings = new MotionPipelineSettings(
                translationGain: new Vector3(1f, 2f, 1f),
                rotationGain: new Vector3(2f, 2f, 2f),
                translationLimitMeters: new Vector3(0f, 0.25f, 0f),
                rotationLimitDegrees: new Vector3(10f, 5f, 15f));

            MotionPoseCommand command = new MotionPoseProcessor(settings).Process(
                raw,
                deltaTimeSeconds: 1f);

            Assert.That(command.TranslationMeters, Is.EqualTo(new Vector3(0f, 0.25f, 0f)));
            Assert.That(command.RotationDegrees, Is.EqualTo(new Vector3(10f, -5f, -15f)));
            Assert.That(command.LimitFlags, Is.EqualTo(
                MotionLimitFlags.TranslationClamp | MotionLimitFlags.RotationClamp));
        }

        [Test]
        public void Process_LimitsPerAxisVelocityAcrossStepsAndReversal()
        {
            var settings = new MotionPipelineSettings(
                translationGain: Vector3.one,
                rotationGain: Vector3.one,
                translationLimitMeters: Vector3.one,
                rotationLimitDegrees: new Vector3(90f, 90f, 90f),
                translationVelocityLimitMetersPerSecond: new Vector3(0f, 1f, 0f),
                rotationVelocityLimitDegreesPerSecond: new Vector3(20f, 20f, 20f));
            var processor = new MotionPoseProcessor(settings);
            var positive = new MotionPoseSample(
                new Vector3(0f, 0.5f, 0f),
                new Vector3(10f, -10f, 0f),
                1,
                1d,
                true);

            MotionPoseCommand first = processor.Process(positive, deltaTimeSeconds: 0.1f);

            Assert.That(first.TranslationMeters, Is.EqualTo(new Vector3(0f, 0.1f, 0f)));
            Assert.That(first.RotationDegrees, Is.EqualTo(new Vector3(2f, -2f, 0f)));
            Assert.That(first.LimitFlags, Is.EqualTo(
                MotionLimitFlags.TranslationVelocityLimit |
                MotionLimitFlags.RotationVelocityLimit));

            var reversed = new MotionPoseSample(
                new Vector3(0f, -0.5f, 0f),
                new Vector3(-10f, 10f, 0f),
                2,
                1.1d,
                true);
            MotionPoseCommand second = processor.Process(reversed, deltaTimeSeconds: 0.1f);

            AssertVectorWithin(second.TranslationMeters, Vector3.zero);
            AssertVectorWithin(second.RotationDegrees, Vector3.zero);
        }

        [Test]
        public void Process_LimitsAccelerationThroughStartupAndReversal()
        {
            var settings = new MotionPipelineSettings(
                translationGain: Vector3.one,
                rotationGain: Vector3.one,
                translationLimitMeters: new Vector3(0f, 100f, 0f),
                rotationLimitDegrees: new Vector3(180f, 180f, 180f),
                translationVelocityLimitMetersPerSecond: new Vector3(0f, 10f, 0f),
                rotationVelocityLimitDegreesPerSecond: new Vector3(100f, 100f, 100f),
                translationAccelerationLimitMetersPerSecondSquared: new Vector3(0f, 2f, 0f),
                rotationAccelerationLimitDegreesPerSecondSquared: new Vector3(20f, 20f, 20f));
            var processor = new MotionPoseProcessor(settings);
            var positive = new MotionPoseSample(
                new Vector3(0f, 10f, 0f),
                new Vector3(90f, 0f, 0f),
                1,
                1d,
                true);

            MotionPoseCommand first = processor.Process(positive, 0.5f);
            MotionPoseCommand second = processor.Process(positive, 0.5f);

            Assert.That(first.TranslationMeters.y, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(second.TranslationMeters.y, Is.EqualTo(1.5f).Within(0.00001f));
            Assert.That(first.RotationDegrees.x, Is.EqualTo(5f).Within(0.00001f));
            Assert.That(second.RotationDegrees.x, Is.EqualTo(15f).Within(0.00001f));
            Assert.That(first.LimitFlags.HasFlag(MotionLimitFlags.TranslationAccelerationLimit), Is.True);
            Assert.That(first.LimitFlags.HasFlag(MotionLimitFlags.RotationAccelerationLimit), Is.True);

            var reversed = new MotionPoseSample(
                new Vector3(0f, -10f, 0f),
                new Vector3(-90f, 0f, 0f),
                2,
                2d,
                true);
            MotionPoseCommand third = processor.Process(reversed, 0.5f);

            Assert.That(third.TranslationMeters.y, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(third.RotationDegrees.x, Is.EqualTo(20f).Within(0.00001f));
            Assert.That(third.LimitFlags.HasFlag(MotionLimitFlags.TranslationAccelerationLimit), Is.True);
            Assert.That(third.LimitFlags.HasFlag(MotionLimitFlags.RotationAccelerationLimit), Is.True);
        }

        [Test]
        public void UpdateSettings_TightenedVelocityLimitIsImmediatelyHardBound()
        {
            var processor = new MotionPoseProcessor(new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 180f, 180f),
                new Vector3(0f, 10f, 0f),
                new Vector3(100f, 100f, 100f),
                new Vector3(0f, 100f, 0f),
                new Vector3(100f, 100f, 100f)));
            var sample = new MotionPoseSample(
                new Vector3(0f, 100f, 0f),
                Vector3.zero,
                1,
                0d,
                true);
            MotionPoseCommand beforeTightening = processor.Process(sample, 0.1f);

            processor.UpdateSettings(new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 180f, 180f),
                new Vector3(0f, 1f, 0f),
                new Vector3(100f, 100f, 100f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(100f, 100f, 100f)));
            MotionPoseCommand afterTightening = processor.Process(sample, 0.1f);

            float movement =
                afterTightening.TranslationMeters.y - beforeTightening.TranslationMeters.y;
            Assert.That(movement, Is.LessThanOrEqualTo(0.10001f));
            Assert.That(
                afterTightening.LimitFlags.HasFlag(MotionLimitFlags.TranslationVelocityLimit),
                Is.True);
        }

        [Test]
        public void UpdateSettings_TightenedVelocityLimitBoundsSmoothedPublishedCommand()
        {
            const float deltaTimeSeconds = 0.1f;
            var processor = new MotionPoseProcessor(new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 180f, 180f),
                new Vector3(0f, 10f, 0f),
                new Vector3(100f, 100f, 100f),
                new Vector3(0f, 100f, 0f),
                new Vector3(1000f, 1000f, 1000f),
                true,
                1f,
                1f));
            var sample = new MotionPoseSample(
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 0f, 0f),
                1,
                0d,
                true);
            MotionPoseCommand beforeTightening = default;
            for (int step = 0; step < 10; step++)
                beforeTightening = processor.Process(sample, deltaTimeSeconds);

            processor.UpdateSettings(new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 180f, 180f),
                new Vector3(0f, 1f, 0f),
                new Vector3(10f, 100f, 100f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(5f, 1000f, 1000f),
                true,
                1f,
                1f));
            MotionPoseCommand afterTightening =
                processor.Process(sample, deltaTimeSeconds);

            Assert.That(
                afterTightening.TranslationMeters.y -
                beforeTightening.TranslationMeters.y,
                Is.LessThanOrEqualTo(0.10001f));
            Assert.That(
                afterTightening.RotationDegrees.x - beforeTightening.RotationDegrees.x,
                Is.LessThanOrEqualTo(1.0001f));
        }

        [Test]
        public void Process_RejectsNonFiniteInputWithoutCorruptingState()
        {
            var processor = new MotionPoseProcessor(CreateAccelerationSettings());
            var valid = new MotionPoseSample(
                new Vector3(0f, 10f, 0f),
                new Vector3(90f, 0f, 0f),
                1,
                1d,
                true);
            MotionPoseCommand first = processor.Process(valid, 0.5f);
            var invalid = new MotionPoseSample(
                new Vector3(0f, float.NaN, 0f),
                Vector3.zero,
                2,
                double.NaN,
                true);

            MotionPoseCommand rejected = processor.Process(invalid, 0.5f);
            MotionPoseCommand recovered = processor.Process(valid, 0.5f);

            Assert.That(rejected.IsValid, Is.False);
            Assert.That(rejected.LimitFlags.HasFlag(MotionLimitFlags.InvalidInput), Is.True);
            AssertVectorWithin(rejected.TranslationMeters, first.TranslationMeters);
            AssertVectorWithin(rejected.RotationDegrees, first.RotationDegrees);
            Assert.That(double.IsNaN(rejected.SourceTimeSeconds), Is.False);
            Assert.That(recovered.TranslationMeters.y, Is.EqualTo(1.5f).Within(0.00001f));
            Assert.That(recovered.RotationDegrees.x, Is.EqualTo(15f).Within(0.00001f));
        }

        [Test]
        public void Process_RejectsNonFiniteSettingsWithFiniteOutput()
        {
            var settings = new MotionPipelineSettings(
                new Vector3(1f, float.NaN, 1f),
                Vector3.one,
                new Vector3(1f, 1f, 1f),
                new Vector3(10f, 10f, 10f),
                new Vector3(1f, 1f, 1f),
                new Vector3(10f, 10f, 10f),
                new Vector3(1f, 1f, 1f),
                new Vector3(10f, 10f, 10f));
            var processor = new MotionPoseProcessor(settings);
            var sample = new MotionPoseSample(
                new Vector3(0f, 1f, 0f),
                Vector3.zero,
                1,
                0d,
                true);

            MotionPoseCommand command = processor.Process(sample, 0.02f);

            Assert.That(command.IsValid, Is.False);
            Assert.That(
                command.LimitFlags.HasFlag(MotionLimitFlags.InvalidSettings),
                Is.True);
            Assert.That(float.IsFinite(command.TranslationMeters.x), Is.True);
            Assert.That(float.IsFinite(command.TranslationMeters.y), Is.True);
            Assert.That(float.IsFinite(command.TranslationMeters.z), Is.True);
            Assert.That(float.IsFinite(command.RotationDegrees.x), Is.True);
            Assert.That(float.IsFinite(command.RotationDegrees.y), Is.True);
            Assert.That(float.IsFinite(command.RotationDegrees.z), Is.True);
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Process_RejectsInvalidDeltaTimeWithoutCorruptingState(float invalidDeltaTime)
        {
            var processor = new MotionPoseProcessor(CreateAccelerationSettings());
            var sample = new MotionPoseSample(
                new Vector3(0f, 10f, 0f),
                new Vector3(90f, 0f, 0f),
                1,
                1d,
                true);
            MotionPoseCommand first = processor.Process(sample, 0.5f);

            MotionPoseCommand rejected = processor.Process(sample, invalidDeltaTime);
            MotionPoseCommand recovered = processor.Process(sample, 0.5f);

            Assert.That(rejected.IsValid, Is.False);
            Assert.That(rejected.LimitFlags.HasFlag(MotionLimitFlags.InvalidDeltaTime), Is.True);
            AssertVectorWithin(rejected.TranslationMeters, first.TranslationMeters);
            AssertVectorWithin(rejected.RotationDegrees, first.RotationDegrees);
            Assert.That(recovered.TranslationMeters.y, Is.EqualTo(1.5f).Within(0.00001f));
            Assert.That(recovered.RotationDegrees.x, Is.EqualTo(15f).Within(0.00001f));
        }

        [Test]
        public void Process_StepResponseIsBoundedAndComparableAcrossTimesteps()
        {
            MotionPipelineSettings settings = CreateResponsiveBaselineSettings();

            StepResult fiftyHertz = RunStep(settings, 0.02f, 1f);
            StepResult hundredHertz = RunStep(settings, 0.01f, 1f);

            Assert.That(fiftyHertz.MaximumHeave, Is.LessThanOrEqualTo(0.25001f));
            Assert.That(fiftyHertz.MaximumPitch, Is.LessThanOrEqualTo(10.0001f));
            Assert.That(hundredHertz.MaximumHeave, Is.LessThanOrEqualTo(0.25001f));
            Assert.That(hundredHertz.MaximumPitch, Is.LessThanOrEqualTo(10.0001f));
            Assert.That(fiftyHertz.FinalHeave, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(fiftyHertz.FinalPitch, Is.EqualTo(10f).Within(0.01f));
            Assert.That(hundredHertz.FinalHeave, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(hundredHertz.FinalPitch, Is.EqualTo(10f).Within(0.01f));
            Assert.That(fiftyHertz.FinalHeave, Is.EqualTo(hundredHertz.FinalHeave).Within(0.001f));
            Assert.That(fiftyHertz.FinalPitch, Is.EqualTo(hundredHertz.FinalPitch).Within(0.01f));
            Assert.That(fiftyHertz.HeaveNinetyPercentTime, Is.InRange(0.4f, 0.5f));
            Assert.That(fiftyHertz.PitchNinetyPercentTime, Is.InRange(0.2f, 0.3f));
            Assert.That(
                fiftyHertz.HeaveNinetyPercentTime,
                Is.EqualTo(hundredHertz.HeaveNinetyPercentTime).Within(0.03f));
            Assert.That(
                fiftyHertz.PitchNinetyPercentTime,
                Is.EqualTo(hundredHertz.PitchNinetyPercentTime).Within(0.03f));
            Debug.Log(
                "SP06_UNFILTERED_STEP_BASELINE " +
                $"50Hz_heave_t90={fiftyHertz.HeaveNinetyPercentTime:0.000}s " +
                $"50Hz_pitch_t90={fiftyHertz.PitchNinetyPercentTime:0.000}s " +
                $"100Hz_heave_t90={hundredHertz.HeaveNinetyPercentTime:0.000}s " +
                $"100Hz_pitch_t90={hundredHertz.PitchNinetyPercentTime:0.000}s");
        }

        [Test]
        public void Process_ReversalAtPoseLimitDoesNotRetainHiddenOutwardVelocity()
        {
            var processor = new MotionPoseProcessor(CreateResponsiveBaselineSettings());
            var positive = new MotionPoseSample(
                new Vector3(0f, 1f, 0f),
                Vector3.zero,
                1,
                0d,
                true);
            MotionPoseCommand command = default;

            for (int step = 0; step < 100 && command.TranslationMeters.y < 0.25f; step++)
                command = processor.Process(positive, 0.02f);

            Assert.That(command.TranslationMeters.y, Is.EqualTo(0.25f).Within(0.00001f));
            var reversed = new MotionPoseSample(
                new Vector3(0f, -1f, 0f),
                Vector3.zero,
                2,
                1d,
                true);

            MotionPoseCommand afterReversal = processor.Process(reversed, 0.02f);

            Assert.That(afterReversal.TranslationMeters.y, Is.LessThan(0.25f));
        }

        [Test]
        public void Process_WhenSmoothingEnabled_SoftensFirstStep()
        {
            var settings = new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(10f, 10f, 10f),
                new Vector3(180f, 180f, 180f),
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                true,
                0.1f,
                0.1f);
            var processor = new MotionPoseProcessor(settings);
            var sample = new MotionPoseSample(
                new Vector3(0f, 1f, 0f),
                new Vector3(90f, 0f, 0f),
                1,
                0d,
                true);

            MotionPoseCommand command = processor.Process(sample, 0.02f);

            Assert.That(command.TranslationMeters.y, Is.GreaterThan(0f));
            Assert.That(command.TranslationMeters.y, Is.LessThan(1f));
            Assert.That(command.RotationDegrees.x, Is.GreaterThan(0f));
            Assert.That(command.RotationDegrees.x, Is.LessThan(90f));
            Assert.That(float.IsFinite(command.TranslationMeters.y), Is.True);
            Assert.That(float.IsFinite(command.RotationDegrees.x), Is.True);
        }

        [Test]
        public void Process_SmoothingDelayIsMeasuredAgainstUnfilteredBaseline()
        {
            StepResult unfiltered = RunStep(
                CreateResponsiveBaselineSettings(),
                0.02f,
                1f);
            StepResult smoothed = RunStep(
                CreateResponsiveBaselineSettings(true),
                0.02f,
                1f);

            Assert.That(smoothed.HeaveNinetyPercentTime, Is.GreaterThan(0f));
            Assert.That(smoothed.PitchNinetyPercentTime, Is.GreaterThan(0f));
            Assert.That(
                smoothed.HeaveNinetyPercentTime,
                Is.GreaterThan(unfiltered.HeaveNinetyPercentTime));
            Assert.That(
                smoothed.PitchNinetyPercentTime,
                Is.GreaterThan(unfiltered.PitchNinetyPercentTime));
            Assert.That(
                smoothed.HeaveNinetyPercentTime - unfiltered.HeaveNinetyPercentTime,
                Is.LessThan(0.3f));
            Assert.That(
                smoothed.PitchNinetyPercentTime - unfiltered.PitchNinetyPercentTime,
                Is.LessThan(0.3f));
            Debug.Log(
                "SP06_SMOOTHED_STEP_BASELINE " +
                $"heave_t90={smoothed.HeaveNinetyPercentTime:0.000}s " +
                $"heave_added_delay=" +
                $"{smoothed.HeaveNinetyPercentTime - unfiltered.HeaveNinetyPercentTime:0.000}s " +
                $"pitch_t90={smoothed.PitchNinetyPercentTime:0.000}s " +
                $"pitch_added_delay=" +
                $"{smoothed.PitchNinetyPercentTime - unfiltered.PitchNinetyPercentTime:0.000}s");
        }

        [Test]
        public void UpdateSettings_DisablingSmoothingPreservesPublishedRateContinuity()
        {
            const float deltaTimeSeconds = 0.02f;
            var processor = new MotionPoseProcessor(
                CreateResponsiveBaselineSettings(true));
            var sample = new MotionPoseSample(
                new Vector3(0f, 1f, 0f),
                Vector3.zero,
                1,
                0d,
                true);
            MotionPoseCommand previous = default;
            MotionPoseCommand beforeDisable = default;

            for (int step = 0; step < 10; step++)
            {
                previous = beforeDisable;
                beforeDisable = processor.Process(sample, deltaTimeSeconds);
            }

            float velocityBefore =
                (beforeDisable.TranslationMeters.y - previous.TranslationMeters.y) /
                deltaTimeSeconds;
            processor.UpdateSettings(CreateResponsiveBaselineSettings());
            MotionPoseCommand afterDisable = processor.Process(sample, deltaTimeSeconds);
            float velocityAfter =
                (afterDisable.TranslationMeters.y - beforeDisable.TranslationMeters.y) /
                deltaTimeSeconds;

            Assert.That(Mathf.Abs(velocityAfter), Is.LessThanOrEqualTo(0.75001f));
            Assert.That(
                Mathf.Abs(velocityAfter - velocityBefore),
                Is.LessThanOrEqualTo(3f * deltaTimeSeconds + 0.00001f));
        }

        private static StepResult RunStep(
            MotionPipelineSettings settings,
            float deltaTimeSeconds,
            float durationSeconds)
        {
            var processor = new MotionPoseProcessor(settings);
            var sample = new MotionPoseSample(
                new Vector3(0f, 1f, 0f),
                new Vector3(20f, 0f, 0f),
                1,
                0d,
                true);
            float maximumHeave = 0f;
            float maximumPitch = 0f;
            float heaveNinetyPercentTime = -1f;
            float pitchNinetyPercentTime = -1f;
            MotionPoseCommand command = default;
            int steps = Mathf.RoundToInt(durationSeconds / deltaTimeSeconds);

            for (int step = 0; step < steps; step++)
            {
                command = processor.Process(sample, deltaTimeSeconds);
                maximumHeave = Mathf.Max(maximumHeave, command.TranslationMeters.y);
                maximumPitch = Mathf.Max(maximumPitch, command.RotationDegrees.x);
                float elapsed = (step + 1) * deltaTimeSeconds;
                if (heaveNinetyPercentTime < 0f && command.TranslationMeters.y >= 0.225f)
                    heaveNinetyPercentTime = elapsed;
                if (pitchNinetyPercentTime < 0f && command.RotationDegrees.x >= 9f)
                    pitchNinetyPercentTime = elapsed;
            }

            return new StepResult(
                command.TranslationMeters.y,
                command.RotationDegrees.x,
                maximumHeave,
                maximumPitch,
                heaveNinetyPercentTime,
                pitchNinetyPercentTime);
        }

        private readonly struct StepResult
        {
            public float FinalHeave { get; }
            public float FinalPitch { get; }
            public float MaximumHeave { get; }
            public float MaximumPitch { get; }
            public float HeaveNinetyPercentTime { get; }
            public float PitchNinetyPercentTime { get; }

            public StepResult(
                float finalHeave,
                float finalPitch,
                float maximumHeave,
                float maximumPitch,
                float heaveNinetyPercentTime,
                float pitchNinetyPercentTime)
            {
                FinalHeave = finalHeave;
                FinalPitch = finalPitch;
                MaximumHeave = maximumHeave;
                MaximumPitch = maximumPitch;
                HeaveNinetyPercentTime = heaveNinetyPercentTime;
                PitchNinetyPercentTime = pitchNinetyPercentTime;
            }
        }

        private static MotionPipelineSettings CreateResponsiveBaselineSettings(
            bool smoothingEnabled = false)
        {
            return new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 0.25f, 0f),
                new Vector3(10f, 5f, 10f),
                new Vector3(0f, 0.75f, 0f),
                new Vector3(90f, 45f, 90f),
                new Vector3(0f, 3f, 0f),
                new Vector3(360f, 180f, 360f),
                smoothingEnabled,
                0.1f,
                0.1f);
        }

        private static MotionPipelineSettings CreateAccelerationSettings()
        {
            return new MotionPipelineSettings(
                Vector3.one,
                Vector3.one,
                new Vector3(0f, 100f, 0f),
                new Vector3(180f, 180f, 180f),
                new Vector3(0f, 10f, 0f),
                new Vector3(100f, 100f, 100f),
                new Vector3(0f, 2f, 0f),
                new Vector3(20f, 20f, 20f));
        }

        private static void AssertVectorWithin(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.00001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.00001f));
        }
    }
}
