using System;
using System.IO;
using OceanMotion.Subproject09.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OceanMotion.Subproject09.Editor
{
    public static class PreviewSceneBuilder
    {
        public const string ScenePath=PreviewChecks.Root+"Scenes/SP09_IkPreview.unity";
        [MenuItem("Ocean Motion/SP09/Open IK Preview")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Create();
        }
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Preview scene already exists; open it instead of overwriting.");
            PreviewChecks.Run();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("SP09 Visual IK Preview");
            var target=new GameObject("Target - neutral-relative pose"); target.transform.SetParent(root.transform,false);
            var preview=root.AddComponent<StewartIkPreview>();
            preview.geometry=AssetDatabase.LoadAssetAtPath<TextAsset>(PreviewChecks.Root+"Measurements/offline-geometry.json");
            preview.target=target.transform;
            var data=PreviewGeometry.Parse(preview.geometry.text);
            target.transform.localPosition=PreviewGeometry.ToUnity(data.Build().NeutralPositionMm);
            target.transform.localRotation=data.NeutralUnityRotation;
            var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(.4f,.3f,-.4f);
            camera.transform.LookAt(new Vector3(0,.11f,0));
            camera.orthographic=true; camera.orthographicSize=.22f;
            camera.nearClipPlane=.01f; camera.farClipPlane=5;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.04f,.06f);
            camera.rect=new Rect(.3f,0,.7f,1);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.Refresh();
            Selection.activeGameObject=root;
            Debug.Log("SP09 PREVIEW SCENE CREATED: "+ScenePath);
        }
    }
}
