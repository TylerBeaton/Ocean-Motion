using TMPro;
using UnityEngine;

namespace OceanMotion.Subproject05
{
    public class BoatTelemetryDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoatMotionTelemetry telemetry;
        [SerializeField] private WaveField waveField;
        [SerializeField] private TMP_Text telemetryText;

        [Header("Display")]
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private float nextRefreshTime;

        private void Reset()
        {
            telemetryText = GetComponentInChildren<TMP_Text>();
            waveField = FindFirstObjectByType<WaveField>();
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

            string presetName =
                waveField != null ? waveField.ActivePresetName : "Unassigned";

            if (telemetry == null)
            {
                telemetryText.text =
                    "BOAT MOTION\n\n" +
                    $"Preset: {presetName}\n\n" +
                    "Telemetry reference missing";
                return;
            }

            if (!telemetry.IsCalibrated)
            {
                telemetryText.text =
                    $"Preset: {presetName}\n\n" +
                    "Waiting for neutral\ncalibration";
                return;
            }

            telemetryText.text =
                $"Preset: {presetName}\n" +
                $"Pitch: {telemetry.Pitch:+0.00;-0.00;0.00}°\n" +
                $"Roll:  {telemetry.Roll:+0.00;-0.00;0.00}°\n" +
                $"Heave: {telemetry.Heave:+0.00;-0.00;0.00}\n" +
                $"Yaw:   {telemetry.Yaw:+0.00;-0.00;0.00}°\n" +
                $"Speed: {telemetry.Speed * 1.94384f:0.00} knots\n" +
                $"Time:  {telemetry.SimulationTime:0.0} s\n" +
                $"Frame: {telemetry.Sequence}";
        }
    }
}