using System;
using System.Reflection;
using OceanMotion.Subproject05;
using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    public static class MotionPipelinePoseDisplayChecks
    {
        [MenuItem("Ocean Motion/Subproject 06/Check Pose Display")]
        public static void Run()
        {
            Type sourceType = FindType("OceanMotion.Subproject06.BoatTelemetryPoseSource");
            Type controllerType = FindType("OceanMotion.Subproject06.MotionPipelineController");
            Type displayType = FindType("OceanMotion.Subproject06.MotionPipelinePoseDisplay");
            if (displayType == null)
                throw new InvalidOperationException("MotionPipelinePoseDisplay is not implemented.");

            GameObject boat = new GameObject("SP06 Display Check Boat");
            GameObject displayRoot = new GameObject("SP06 Display Check Root");
            GameObject rawProxy = new GameObject("Raw Proxy");
            GameObject processedProxy = new GameObject("Processed Proxy");
            try
            {
                BoatMotionTelemetry telemetry = boat.AddComponent<BoatMotionTelemetry>();
                Component source = boat.AddComponent(sourceType);
                Component controller = boat.AddComponent(controllerType);
                Component display = displayRoot.AddComponent(displayType);
                rawProxy.transform.SetParent(displayRoot.transform);
                processedProxy.transform.SetParent(displayRoot.transform);
                rawProxy.transform.localPosition = new Vector3(-1f, 0f, 0f);
                processedProxy.transform.localPosition = new Vector3(1f, 0f, 0f);
                rawProxy.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                processedProxy.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);

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
                Set(display, "source", source);
                Set(display, "controller", controller);
                Set(display, "rawPose", rawProxy.transform);
                Set(display, "processedPose", processedProxy.transform);
                Set(display, "translationDisplayScale", 2f);

                Invoke(source, "Awake");
                Invoke(controller, "Awake");
                Invoke(source, "FixedUpdate");
                Invoke(controller, "FixedUpdate");
                Invoke(display, "Awake");
                Invoke(display, "LateUpdate");

                AssertPose(
                    rawProxy.transform,
                    new Vector3(-1f, 0.8f, 0f),
                    Quaternion.Euler(0f, 15f, 0f) * Quaternion.Euler(10f, 4f, -8f),
                    "raw");
                AssertPose(
                    processedProxy.transform,
                    new Vector3(1f, 0.4f, 0f),
                    Quaternion.Euler(0f, 15f, 0f) * Quaternion.Euler(5f, 0f, -2f),
                    "processed");

                Debug.Log("SP06_POSE_DISPLAY_CHECKS: passed=2 failed=0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boat);
                UnityEngine.Object.DestroyImmediate(displayRoot);
            }
        }

        private static void AssertPose(
            Transform actual,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string label)
        {
            if (Vector3.Distance(actual.localPosition, expectedPosition) > 0.00001f ||
                Quaternion.Angle(actual.localRotation, expectedRotation) > 0.001f)
                throw new InvalidOperationException("Incorrect " + label + " display pose.");
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
