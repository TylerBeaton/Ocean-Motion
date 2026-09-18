using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    public static class MotionPipelineSceneChecks
    {
        private const string ScenePath =
            "Assets/Subprojects/06_MOTION_PIPELINE/Scenes/SP06_Motion_Pipeline.unity";

        [MenuItem("Ocean Motion/Subproject 06/Check Saved Scene")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BoatTelemetryPoseSource source =
                UnityEngine.Object.FindFirstObjectByType<BoatTelemetryPoseSource>();
            MotionPipelineController controller =
                UnityEngine.Object.FindFirstObjectByType<MotionPipelineController>();
            MotionPipelinePoseDisplay display =
                UnityEngine.Object.FindFirstObjectByType<MotionPipelinePoseDisplay>();

            Require(source != null, "Saved scene is missing BoatTelemetryPoseSource.");
            Require(controller != null, "Saved scene is missing MotionPipelineController.");
            Require(display != null, "Saved scene is missing MotionPipelinePoseDisplay.");

            var sourceObject = new SerializedObject(source);
            var controllerObject = new SerializedObject(controller);
            var displayObject = new SerializedObject(display);

            Require(
                sourceObject.FindProperty("telemetry").objectReferenceValue != null,
                "Telemetry source reference is missing.");
            Require(
                controllerObject.FindProperty("source").objectReferenceValue == source,
                "Controller source reference is incorrect.");
            Require(
                displayObject.FindProperty("source").objectReferenceValue == source &&
                displayObject.FindProperty("controller").objectReferenceValue == controller,
                "Pose display pipeline references are incorrect.");

            Transform rawPose = (Transform)displayObject
                .FindProperty("rawPose").objectReferenceValue;
            Transform processedPose = (Transform)displayObject
                .FindProperty("processedPose").objectReferenceValue;
            Require(rawPose != null && processedPose != null && rawPose != processedPose,
                "Display proxy references are missing or identical.");
            Require(!AreHierarchyRelated(rawPose, source.transform) &&
                !AreHierarchyRelated(processedPose, source.transform),
                "Display proxies must remain outside the source boat hierarchy.");
            Require(rawPose.GetComponentsInChildren<Collider>(true).Length == 0 &&
                processedPose.GetComponentsInChildren<Collider>(true).Length == 0,
                "Display proxies must not contain colliders.");

            SerializedProperty settings = controllerObject.FindProperty("settings");
            RequireVector(settings, "translationGain", Vector3.one);
            RequireVector(settings, "rotationGain", Vector3.one);
            RequireVector(settings, "translationLimitMeters", new Vector3(0f, 0.25f, 0f));
            RequireVector(settings, "rotationLimitDegrees", new Vector3(10f, 5f, 10f));
            RequireVector(
                settings,
                "translationVelocityLimitMetersPerSecond",
                new Vector3(0f, 0.75f, 0f));
            RequireVector(
                settings,
                "rotationVelocityLimitDegreesPerSecond",
                new Vector3(90f, 45f, 90f));
            RequireVector(
                settings,
                "translationAccelerationLimitMetersPerSecondSquared",
                new Vector3(0f, 3f, 0f));
            RequireVector(
                settings,
                "rotationAccelerationLimitDegreesPerSecondSquared",
                new Vector3(360f, 180f, 360f));
            Require(!settings.FindPropertyRelative("smoothingEnabled").boolValue,
                "Smoothing must remain disabled by default.");
            RequireApproximately(
                settings.FindPropertyRelative("translationSmoothingTimeSeconds").floatValue,
                0.1f,
                "Translation smoothing time is incorrect.");
            RequireApproximately(
                settings.FindPropertyRelative("rotationSmoothingTimeSeconds").floatValue,
                0.1f,
                "Rotation smoothing time is incorrect.");

            Debug.Log("SP06_SAVED_SCENE_CHECKS: passed=17 failed=0");
        }

        private static bool AreHierarchyRelated(Transform first, Transform second)
        {
            return first == second || first.IsChildOf(second) || second.IsChildOf(first);
        }

        private static void RequireVector(
            SerializedProperty parent,
            string propertyName,
            Vector3 expected)
        {
            Vector3 actual = parent.FindPropertyRelative(propertyName).vector3Value;
            Require(actual == expected, $"{propertyName} is {actual}, expected {expected}.");
        }

        private static void RequireApproximately(float actual, float expected, string message)
        {
            Require(Mathf.Abs(actual - expected) <= 0.00001f, message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
