using System;
using System.Reflection;
using OceanMotion.Subproject05;
using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    // Editor-only smoke checks. Reflection keeps these checks able to report a
    // missing runtime component without preventing the project from compiling.
    public static class BoatTelemetryPoseSourceChecks
    {
        [MenuItem("Ocean Motion/Subproject 06/Check Telemetry Snapshot")]
        public static void Run()
        {
            Type sourceType = FindType("OceanMotion.Subproject06.BoatTelemetryPoseSource");
            if (sourceType == null)
                throw new InvalidOperationException("BoatTelemetryPoseSource is not implemented.");

            GameObject boat = new GameObject("SP06 Snapshot Check");
            try
            {
                BoatMotionTelemetry telemetry = boat.AddComponent<BoatMotionTelemetry>();
                Component source = boat.AddComponent(sourceType);
                Set(telemetry, "isCalibrated", true);
                Set(telemetry, "pitch", -12f);
                Set(telemetry, "yaw", 3f);
                Set(telemetry, "roll", 7f);
                Set(telemetry, "heave", -0.25f);
                Set(telemetry, "sequence", 10);
                Set(telemetry, "simulationTime", 2f);
                sourceType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                sourceType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                MotionPoseSample sample = (MotionPoseSample)sourceType.GetProperty("LatestSample").GetValue(source);
                if (!sample.IsValid || sample.Sequence != 1 || sample.SourceTimeSeconds != 2d ||
                    sample.TranslationMeters != new Vector3(0f, -0.25f, 0f) ||
                    sample.RotationDegrees != new Vector3(-12f, 3f, 7f))
                    throw new InvalidOperationException("Snapshot did not preserve calibrated telemetry.");

                sourceType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                sample = (MotionPoseSample)sourceType.GetProperty("LatestSample").GetValue(source);
                if (sample.Sequence != 1)
                    throw new InvalidOperationException("Repeated telemetry created a duplicate snapshot.");

                // SP05 resets its sequence when neutral is recalibrated.
                Set(telemetry, "sequence", 1);
                Set(telemetry, "simulationTime", 3f);
                sourceType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                sample = (MotionPoseSample)sourceType.GetProperty("LatestSample").GetValue(source);
                if (!sample.IsValid || sample.Sequence != 2 || sample.SourceTimeSeconds != 3d)
                    throw new InvalidOperationException("Snapshot sequence did not survive source recalibration.");

                Set(telemetry, "isCalibrated", false);
                CheckInvalid(source, "uncalibrated input");
                Set(telemetry, "isCalibrated", true);
                Set(telemetry, "sequence", 0);
                CheckInvalid(source, "calibration before first physics sample");
                Set(telemetry, "sequence", 2);
                Set(telemetry, "pitch", float.NaN);
                CheckInvalid(source, "non-finite angle");
                Set(telemetry, "pitch", -12f);
                Set(telemetry, "simulationTime", float.PositiveInfinity);
                CheckInvalid(source, "non-finite timestamp");
                Set(telemetry, "simulationTime", 4f);
                telemetry.enabled = false;
                CheckInvalid(source, "disabled telemetry");
                telemetry.enabled = true;
                sourceType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                sample = (MotionPoseSample)sourceType.GetProperty("LatestSample").GetValue(source);
                if (!sample.IsValid || sample.Sequence != 3)
                    throw new InvalidOperationException("Recovery lost the snapshot sequence.");
                sourceType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
                sample = (MotionPoseSample)sourceType.GetProperty("LatestSample").GetValue(source);
                if (sample.IsValid)
                    throw new InvalidOperationException("Disabled adapter retained a valid snapshot.");
                Set(source, "telemetry", null);
                CheckInvalid(source, "missing telemetry");

                Debug.Log("SP06_SNAPSHOT_CHECKS: passed=11 failed=0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boat);
            }
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void CheckInvalid(Component source, string scenario)
        {
            Type type = source.GetType();
            type.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, null);
            MotionPoseSample sample = (MotionPoseSample)type.GetProperty("LatestSample").GetValue(source);
            if (sample.IsValid)
                throw new InvalidOperationException("Accepted " + scenario + ".");
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
