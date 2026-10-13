using UnityEditor;
using UnityEngine;

namespace OceanMotion.FusionPlayback.Editor
{
    [CustomEditor(typeof(CopyTransformToIkTarget))]
    public sealed class CopyTransformToIkTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var copier = (CopyTransformToIkTarget)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !copier.isActiveAndEnabled))
            {
                if (GUILayout.Button("Calibrate Neutral And Start Copying"))
                    copier.CalibrateAndStartCopying();

                using (new EditorGUI.DisabledScope(!copier.IsCopying))
                {
                    if (GUILayout.Button("Stop Copying"))
                        copier.StopCopying();
                }
            }
        }
    }
}
