using System;
using System.Reflection;
using OceanMotion.Subproject05;
using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    public static class MotionPipelineControllerChecks
    {
        [MenuItem("Ocean Motion/Subproject 06/Check Runtime Scaling")]
        public static void Run()
        {
            Type sourceType = FindType("OceanMotion.Subproject06.BoatTelemetryPoseSource");
            Type controllerType = FindType("OceanMotion.Subproject06.MotionPipelineController");
            if (controllerType == null)
                throw new InvalidOperationException("MotionPipelineController is not implemented.");

            GameObject boat = new GameObject("SP06 Runtime Scaling Check");
            try
            {
                BoatMotionTelemetry telemetry = boat.AddComponent<BoatMotionTelemetry>();
                Component source = boat.AddComponent(sourceType);
                Component controller = boat.AddComponent(controllerType);
                Set(telemetry, "isCalibrated", true);
                Set(telemetry, "pitch", 10f);
                Set(telemetry, "yaw", 4f);
                Set(telemetry, "roll", -8f);
                Set(telemetry, "heave", 0.4f);
                Set(telemetry, "sequence", 1);
                Set(telemetry, "simulationTime", 2f);
                Set(controller, "settings", new MotionPipelineSettings(
                    new Vector3(1f, 0.5f, 1f),
                    new Vector3(0.5f, 0f, 0.25f)));

                Invoke(source, "Awake");
                Invoke(controller, "Awake");
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                MotionPoseCommand command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);

                if (!command.IsValid || command.SourceSequence != 1 ||
                    command.TranslationMeters != new Vector3(0f, 0.2f, 0f) ||
                    command.RotationDegrees != new Vector3(5f, 0f, -2f))
                    throw new InvalidOperationException("Runtime scaling output was incorrect.");

                Set(controller, "settings", new MotionPipelineSettings(
                    Vector3.one,
                    Vector3.one,
                    new Vector3(0f, 10f, 0f),
                    new Vector3(180f, 180f, 180f),
                    new Vector3(0f, 1f, 0f),
                    new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                    new Vector3(0f, 100f, 0f),
                    new Vector3(100f, 100f, 100f)));
                Invoke(controller, "Awake");

                Set(telemetry, "heave", 0f);
                Set(telemetry, "sequence", 2);
                Set(telemetry, "simulationTime", 3f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");

                Set(telemetry, "heave", 1f);
                Set(telemetry, "sequence", 3);
                Set(telemetry, "simulationTime", 3.1f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);

                if (Mathf.Abs(command.TranslationMeters.y - 0.1f) > 0.00001f)
                    throw new InvalidOperationException(
                        "Skipped source interval did not use elapsed source time.");

                Invoke(controller, "OnValidate");
                Set(telemetry, "heave", 1f);
                Set(telemetry, "sequence", 4);
                Set(telemetry, "simulationTime", 3.2f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);
                if (Mathf.Abs(command.TranslationMeters.y - 0.2f) > 0.00001f)
                {
                    throw new InvalidOperationException(
                        "Runtime settings validation reset processor state.");
                }

                int staleSteps = Mathf.CeilToInt(0.25f / Time.fixedDeltaTime) + 1;
                for (int step = 0; step < staleSteps; step++)
                    Invoke(controller, "FixedUpdate");

                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);
                if (command.IsValid ||
                    !command.LimitFlags.HasFlag(MotionLimitFlags.StaleInput))
                {
                    throw new InvalidOperationException(
                        "Stalled source remained valid past the freshness timeout.");
                }

                Set(telemetry, "heave", 1f);
                Set(telemetry, "sequence", 5);
                Set(telemetry, "simulationTime", 10f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);
                if (!command.IsValid ||
                    Mathf.Abs(command.TranslationMeters.y - 0.22f) > 0.00001f)
                {
                    throw new InvalidOperationException(
                        "Stale recovery integrated the entire source-time outage.");
                }

                Set(telemetry, "sequence", 6);
                Set(telemetry, "simulationTime", 9f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);
                if (command.IsValid ||
                    !command.LimitFlags.HasFlag(MotionLimitFlags.InvalidDeltaTime))
                {
                    throw new InvalidOperationException(
                        "Non-monotonic source time was not rejected.");
                }

                Set(telemetry, "sequence", 7);
                Set(telemetry, "simulationTime", 20f);
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                command = (MotionPoseCommand)controllerType
                    .GetProperty("LatestCommand").GetValue(controller);
                if (!command.IsValid ||
                    Mathf.Abs(command.TranslationMeters.y - 0.24f) > 0.00001f)
                {
                    throw new InvalidOperationException(
                        "Rejected-sample recovery integrated the rejected interval.");
                }

                Debug.Log("SP06_RUNTIME_SCALING_CHECKS: passed=7 failed=0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boat);
            }
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
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
