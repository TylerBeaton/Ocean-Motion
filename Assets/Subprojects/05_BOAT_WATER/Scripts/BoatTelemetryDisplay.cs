using TMPro;
using UnityEngine;

namespace OceanMotion.Subproject05
{
    public class BoatTelemetryDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoatMotionTelemetry telemetry;
        [SerializeField] private TMP_Text telemetryText;

        [Header("Display")]
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private float nextRefreshTime;

        private void Reset()
        {
            telemetryText = GetComponentInChildren<TMP_Text>();
        }

        private void Update()
        {
            if (telemetryText == null)
            {
                return;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;

            if (telemetry == null)
            {
                telemetryText.text =
                    "BOAT MOTION\n\nTelemetry reference missing";
                return;
            }

            if (!telemetry.IsCalibrated)
            {
                telemetryText.text =
                    "BOAT MOTION\n\nWaiting for neutral calibration";
                return;
            }

            telemetryText.text =
                "BOAT MOTION\n\n" +
                $"Pitch: {telemetry.Pitch:+0.00;-0.00;0.00}°\n" +
                $"Roll:  {telemetry.Roll:+0.00;-0.00;0.00}°\n" +
                $"Heave: {telemetry.Heave:+0.00;-0.00;0.00}\n" +
                $"Yaw:   {telemetry.Yaw:+0.00;-0.00;0.00}°\n" +
                $"Time:  {telemetry.SimulationTime:0.0} s\n" +
                $"Frame: {telemetry.Sequence}";
        }
    }
}