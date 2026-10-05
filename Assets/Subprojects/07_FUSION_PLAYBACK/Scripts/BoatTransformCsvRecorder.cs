using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OceanMotion.FusionPlayback
{
    /// <summary>Records a Transform relative to a captured neutral pose as Fusion-readable CSV.</summary>
    [DisallowMultipleComponent]
    public sealed class BoatTransformCsvRecorder : MonoBehaviour
    {
        private const int SchemaVersion = 1;

        [Header("Source")]
        [SerializeField] private Transform boatTransform;

        [Header("Recording")]
        [Min(0.1f)]
        [SerializeField] private float sampleRateHz = 20f;
        [SerializeField] private bool calibrateOnStart = true;
        [SerializeField] private bool beginRecordingOnStart;
        [SerializeField] private string fileNamePrefix = "boat-telemetry";

        private Vector3 neutralWorldPosition;
        private Quaternion neutralWorldRotation = Quaternion.identity;
        private Quaternion previousRelativeRotation = Quaternion.identity;
        private StreamWriter writer;
        private double recordingStartTime;
        private double nextSampleTime;
        private long sequence;

        public bool IsRecording => writer != null;
        public string LastOutputPath { get; private set; }

        private void Reset() => boatTransform = transform;

        private void Awake()
        {
            if (boatTransform == null)
                boatTransform = transform;
        }

        private void Start()
        {
            if (calibrateOnStart)
                CalibrateNeutral();
            if (beginRecordingOnStart)
                StartRecording();
        }

        private void FixedUpdate()
        {
            if (!IsRecording)
                return;

            double now = Time.fixedTimeAsDouble;
            double interval = 1.0 / Math.Max(0.1, sampleRateHz);
            if (now + 1e-9 < nextSampleTime)
                return;

            WriteSample(now);
            do
            {
                nextSampleTime += interval;
            }
            while (nextSampleTime <= now);
        }

        private void OnDisable() => StopRecording();
        private void OnApplicationQuit() => StopRecording();

        [ContextMenu("Calibrate Neutral")]
        public void CalibrateNeutral()
        {
            if (IsRecording)
            {
                Debug.LogWarning("Stop telemetry recording before recalibrating neutral.", this);
                return;
            }

            neutralWorldPosition = boatTransform.position;
            neutralWorldRotation = boatTransform.rotation;
            previousRelativeRotation = Quaternion.identity;
        }

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            if (IsRecording)
                return;

            string safePrefix = SanitizeFileName(fileNamePrefix);
            string fileName = $"{safePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            LastOutputPath = Path.Combine(Application.persistentDataPath, fileName);
            writer = new StreamWriter(LastOutputPath, false, new UTF8Encoding(false));
            writer.WriteLine("schema_version,sequence,time_seconds,pos_x_m,pos_y_m,pos_z_m,rot_x,rot_y,rot_z,rot_w");

            sequence = 0;
            recordingStartTime = Time.fixedTimeAsDouble;
            nextSampleTime = recordingStartTime;
            previousRelativeRotation = Quaternion.identity;
            Debug.Log($"Fusion telemetry recording started: {LastOutputPath}", this);
        }

        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            if (writer == null)
                return;

            writer.Flush();
            writer.Dispose();
            writer = null;
            Debug.Log($"Fusion telemetry recording saved: {LastOutputPath}", this);
        }

        private void WriteSample(double now)
        {
            Quaternion inverseNeutral = Quaternion.Inverse(neutralWorldRotation);
            Vector3 relativePosition = inverseNeutral * (boatTransform.position - neutralWorldPosition);
            Quaternion relativeRotation = inverseNeutral * boatTransform.rotation;

            if (Quaternion.Dot(previousRelativeRotation, relativeRotation) < 0f)
                relativeRotation = new Quaternion(-relativeRotation.x, -relativeRotation.y, -relativeRotation.z, -relativeRotation.w);
            previousRelativeRotation = relativeRotation;

            writer.WriteLine(string.Join(",",
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                sequence.ToString(CultureInfo.InvariantCulture),
                (now - recordingStartTime).ToString("R", CultureInfo.InvariantCulture),
                relativePosition.x.ToString("R", CultureInfo.InvariantCulture),
                relativePosition.y.ToString("R", CultureInfo.InvariantCulture),
                relativePosition.z.ToString("R", CultureInfo.InvariantCulture),
                relativeRotation.x.ToString("R", CultureInfo.InvariantCulture),
                relativeRotation.y.ToString("R", CultureInfo.InvariantCulture),
                relativeRotation.z.ToString("R", CultureInfo.InvariantCulture),
                relativeRotation.w.ToString("R", CultureInfo.InvariantCulture)));

            sequence++;
            if (sequence % Math.Max(1, Mathf.RoundToInt(sampleRateHz)) == 0)
                writer.Flush();
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "boat-telemetry" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '-');
            return result;
        }
    }
}
