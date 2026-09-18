using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// Feeds each fresh telemetry snapshot through the architecture-neutral
    /// processor. Current conditioning includes gain, pose, velocity, and
    /// acceleration limits.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(BoatTelemetryPoseSource))]
    public class MotionPipelineController : MonoBehaviour
    {
        private const float MaximumSourceAgeSeconds = 0.25f;

        [SerializeField] private BoatTelemetryPoseSource source;
        [SerializeField] private MotionPipelineSettings settings =
            MotionPipelineSettings.Default;


        private MotionPoseProcessor processor;
        private ulong lastProcessedSourceSequence;
        private bool hasProcessedSourceTime;
        private double lastProcessedSourceTimeSeconds;
        private float sourceAgeSeconds;

        public MotionPoseCommand LatestCommand { get; private set; }

        private void Reset()
        {
            source = GetComponent<BoatTelemetryPoseSource>();
            settings = MotionPipelineSettings.Default;
        }

        private void Awake()
        {
            if (source == null)
                source = GetComponent<BoatTelemetryPoseSource>();

            processor = new MotionPoseProcessor(settings);
        }

        private void OnValidate()
        {
            if (processor == null)
                processor = new MotionPoseProcessor(settings);
            else
                processor.UpdateSettings(settings);
        }

        private void FixedUpdate()
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                sourceAgeSeconds = 0f;
                hasProcessedSourceTime = false;
                Publish(default);
                return;
            }

            MotionPoseSample sample = source.LatestSample;
            if (!sample.IsValid)
            {
                sourceAgeSeconds = 0f;
                hasProcessedSourceTime = false;
                Publish(processor.Process(sample, Time.fixedDeltaTime));
                return;
            }

            if (sample.Sequence == lastProcessedSourceSequence)
            {
                sourceAgeSeconds += Time.fixedDeltaTime;
                if (sourceAgeSeconds > MaximumSourceAgeSeconds && LatestCommand.IsValid)
                {
                    hasProcessedSourceTime = false;
                    Publish(new MotionPoseCommand(
                        LatestCommand.TranslationMeters,
                        LatestCommand.RotationDegrees,
                        sample.Sequence,
                        sample.SourceTimeSeconds,
                        false,
                        MotionLimitFlags.StaleInput));
                }

                return;
            }

            sourceAgeSeconds = 0f;
            lastProcessedSourceSequence = sample.Sequence;
            float deltaTimeSeconds = Time.fixedDeltaTime;
            if (hasProcessedSourceTime)
            {
                double elapsedSourceTime =
                    sample.SourceTimeSeconds - lastProcessedSourceTimeSeconds;
                deltaTimeSeconds = elapsedSourceTime > 0d &&
                    elapsedSourceTime <= float.MaxValue
                    ? (float)elapsedSourceTime
                    : float.NaN;
            }

            MotionPoseCommand command = processor.Process(sample, deltaTimeSeconds);
            if (command.IsValid)
            {
                hasProcessedSourceTime = true;
                lastProcessedSourceTimeSeconds = sample.SourceTimeSeconds;
            }
            else
            {
                hasProcessedSourceTime = false;
            }

            Publish(command);
        }

        private void OnDisable()
        {
            sourceAgeSeconds = 0f;
            hasProcessedSourceTime = false;
            Publish(default);
        }

        private void Publish(MotionPoseCommand command)
        {
            LatestCommand = command;
        }
    }
}
