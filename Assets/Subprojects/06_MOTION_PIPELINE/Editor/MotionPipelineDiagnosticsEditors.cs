using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    [CustomEditor(typeof(BoatTelemetryPoseSource))]
    public sealed class BoatTelemetryPoseSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var source = (BoatTelemetryPoseSource)target;
            MotionPoseSample sample = source.LatestSample;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest Snapshot (Runtime Diagnostics)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Translation Metres", sample.TranslationMeters);
                EditorGUILayout.Vector3Field("Rotation Degrees", sample.RotationDegrees);
                EditorGUILayout.TextField("Sample Sequence", sample.Sequence.ToString());
                EditorGUILayout.DoubleField("Source Time Seconds", sample.SourceTimeSeconds);
                EditorGUILayout.Toggle("Is Valid", sample.IsValid);
            }
        }
    }

    [CustomEditor(typeof(MotionPipelineController))]
    public sealed class MotionPipelineControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (MotionPipelineController)target;
            MotionPoseCommand command = controller.LatestCommand;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Processed Command (Runtime Diagnostics)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Translation Metres", command.TranslationMeters);
                EditorGUILayout.Vector3Field("Rotation Degrees", command.RotationDegrees);
                EditorGUILayout.TextField("Source Sequence", command.SourceSequence.ToString());
                EditorGUILayout.DoubleField("Source Time Seconds", command.SourceTimeSeconds);
                EditorGUILayout.Toggle("Is Valid", command.IsValid);
                EditorGUILayout.EnumFlagsField("Limit Flags", command.LimitFlags);
            }
        }
    }
}
