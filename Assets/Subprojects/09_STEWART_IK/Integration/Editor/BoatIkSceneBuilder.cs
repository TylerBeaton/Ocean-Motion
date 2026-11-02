using System;
using System.IO;
using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Editor;
using OceanMotion.Subproject09.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    public static class BoatIkSceneBuilder
    {
        public const string ScenePath=PreviewChecks.Root+"Scenes/SP09_BoatMotionIk.unity";
        [MenuItem("Ocean Motion/SP09/Open Boat Motion IK")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
        }
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Boat IK scene exists; open without overwriting.");
            ArchivedSceneBaseline.Check();
            PreviewChecks.Run();
            var scene=EditorSceneManager.OpenScene("Assets/Subprojects/06_MOTION_PIPELINE/Scenes/SP06_Motion_Pipeline.unity");
            // Work in a separate saved scene before making any edits to the source archive.
            EditorSceneManager.SaveScene(scene,ScenePath,true);
            scene=EditorSceneManager.OpenScene(ScenePath);
            foreach (var transport in UnityEngine.Object.FindObjectsByType<MotionSerialTransport>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(transport);
            foreach (var serial in UnityEngine.Object.FindObjectsByType<SerialController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(serial);
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (var comparison in UnityEngine.Object.FindObjectsByType<MotionPipelinePoseDisplay>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                comparison.gameObject.SetActive(false);
            Camera boatCamera=null;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.cullingMask &= ~(1<<30);
                if (camera.enabled) { camera.rect=new Rect(.27f,0,.35f,1); boatCamera=camera; }
            }
            if (!boatCamera) throw new InvalidOperationException("Source scene requires an enabled boat camera.");
            var root=new GameObject("SP09 Processed Boat to Visual IK") {layer=30};
            root.transform.position=new Vector3(100,0,0); // Display isolated from boat physics.
            var target=new GameObject("Target - processed neutral-relative pose"); target.transform.SetParent(root.transform,false);
            var preview=root.AddComponent<StewartIkPreview>();
            preview.geometry=AssetDatabase.LoadAssetAtPath<TextAsset>(PreviewChecks.Root+"Measurements/offline-geometry.json");
            preview.target=target.transform;
            var data=PreviewGeometry.Parse(preview.geometry.text);
            target.transform.localPosition=PreviewGeometry.ToUnity(data.Build().NeutralPositionMm);
            target.transform.localRotation=data.NeutralUnityRotation;
            var bridge=root.AddComponent<ProcessedBoatIkBridge>();
            bridge.preview=preview;
            bridge.pipeline=UnityEngine.Object.FindFirstObjectByType<MotionPipelineController>();
            bridge.telemetry=UnityEngine.Object.FindFirstObjectByType<BoatMotionTelemetry>();
            bridge.waves=UnityEngine.Object.FindFirstObjectByType<WaveField>();
            if (!bridge.pipeline || !bridge.telemetry || !bridge.waves) throw new InvalidOperationException("Missing source components.");
            bridge.waves.SetPreset(WavePreset.Calm);
            var ikCamera=new GameObject("IK Preview Camera").AddComponent<Camera>();
            ikCamera.transform.position=root.transform.position+new Vector3(.4f,.3f,-.4f);
            ikCamera.transform.LookAt(root.transform.position+new Vector3(0,.11f,0));
            ikCamera.orthographic=true; ikCamera.orthographicSize=.22f;
            ikCamera.nearClipPlane=.01f; ikCamera.farClipPlane=5;
            ikCamera.clearFlags=CameraClearFlags.SolidColor; ikCamera.backgroundColor=new Color(.025f,.04f,.06f);
            ikCamera.cullingMask=1<<30; ikCamera.rect=new Rect(.62f,0,.38f,1); ikCamera.depth=10;
            preview.viewCamera=ikCamera;
            EditorSceneManager.SaveScene(scene); AssetDatabase.Refresh();
            Debug.Log("SP09 BOAT IK SCENE CREATED: "+ScenePath);
        }
    }
}
