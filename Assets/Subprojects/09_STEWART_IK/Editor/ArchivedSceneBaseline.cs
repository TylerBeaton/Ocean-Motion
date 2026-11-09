using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Editor
{
    public static class ArchivedSceneBaseline
    {
        public const string ScenePath =
            "Assets/Subprojects/07_FUSION_PLAYBACK/Scenes/SP07_Unity_Fusion360_Scripting.unity";

        [Serializable]
        private sealed class Result
        {
            public string editor_version;
            public string scene;
            public int root_objects;
            public int missing_scripts;
            public bool passed;
        }

        public static void Check()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            int missing = 0;
            foreach (GameObject root in roots)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            var result = new Result
            {
                editor_version = Application.unityVersion,
                scene = ScenePath,
                root_objects = roots.Length,
                missing_scripts = missing,
                passed = scene.IsValid() && roots.Length > 0 && missing == 0
            };
            string report = Path.Combine(Application.dataPath,
                "Subprojects/09_STEWART_IK/Reports/unity-baseline-check.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true) + "\n");
            if (!result.passed)
                throw new InvalidOperationException("SP07 archived-scene baseline failed; see " + report);
            Debug.Log("SP09 BASELINE PASS: archived SP07 scene loaded, no missing scripts; " + report);
        }
    }
}
