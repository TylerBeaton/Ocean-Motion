using OceanMotion.Subproject05;
using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// Copies SP05's calibrated telemetry without scaling or filtering it.
    /// Runs after BoatMotionTelemetry's default-order FixedUpdate.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(BoatMotionTelemetry))]
    public class BoatTelemetryPoseSource : MonoBehaviour
    {
        [SerializeField] private BoatMotionTelemetry telemetry;


        private ulong nextSequence;
        private bool hasObservedSample;
        private int lastTelemetrySequence;
        private float lastTelemetryTime;

        public MotionPoseSample LatestSample { get; private set; }

        private void Reset()
        {
            telemetry = GetComponent<BoatMotionTelemetry>();
        }

        private void Awake()
        {
            if (telemetry == null)
                telemetry = GetComponent<BoatMotionTelemetry>();
        }

        private void FixedUpdate()
        {
            // Calibration alone is not a completed physics reading (SP05's
            // source sequence is zero immediately after calibration).
            if (telemetry == null || !telemetry.isActiveAndEnabled ||
                !telemetry.IsCalibrated || telemetry.Sequence == 0 ||
                !IsFinite(telemetry.Heave) || !IsFinite(telemetry.Pitch) ||
                !IsFinite(telemetry.Yaw) || !IsFinite(telemetry.Roll) ||
                !IsFinite(telemetry.SimulationTime))
            {
                Publish(default);
                return;
            }

            // Do not manufacture fresh sequence IDs for an unchanged reading.
            // Keep its original timestamp so consumers can detect its age.
            if (hasObservedSample && telemetry.Sequence == lastTelemetrySequence &&
                telemetry.SimulationTime == lastTelemetryTime)
                return;

            hasObservedSample = true;
            lastTelemetrySequence = telemetry.Sequence;
            lastTelemetryTime = telemetry.SimulationTime;
            Publish(new MotionPoseSample(
                new Vector3(0f, telemetry.Heave, 0f),
                new Vector3(telemetry.Pitch, telemetry.Yaw, telemetry.Roll),
                ++nextSequence,
                telemetry.SimulationTime,
                true));
        }

        private void OnDisable()
        {
            // Do not let consumers keep using an enabled-looking snapshot.
            // Preserve the sequence and last source identity across re-enable.
            Publish(default);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void Publish(MotionPoseSample sample)
        {
            LatestSample = sample;
        }
    }
}
