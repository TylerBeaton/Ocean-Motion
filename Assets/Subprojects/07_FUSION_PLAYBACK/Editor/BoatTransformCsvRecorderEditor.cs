using UnityEditor;
using UnityEngine;

namespace OceanMotion.FusionPlayback.Editor
{
    [CustomEditor(typeof(BoatTransformCsvRecorder))]
    public sealed class BoatTransformCsvRecorderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var recorder = (BoatTransformCsvRecorder)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !recorder.isActiveAndEnabled))
            {
                if (GUILayout.Button(recorder.IsRecording ? "Stop Recording" : "Start Recording"))
                {
                    if (recorder.IsRecording)
                        recorder.StopRecording();
                    else
                        recorder.StartRecording();
                }
            }
        }
    }
}
