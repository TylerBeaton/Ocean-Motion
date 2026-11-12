using System;
using System.IO;
using OceanMotion.Subproject09.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    public static class IkPoseRangeSetup
    {
        public const string ScenePath=PreviewChecks.Root+"Scenes/SP09_IkPoseRangeSweep.unity";
        [MenuItem("Ocean Motion/SP09/Open IK Pose Range Sweep")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Create();
        }
        public static void Create()=>BuildAt(ScenePath,BoatIkRangeSetup.ScenePath);
        public static void BuildAt(string destination,string boatScene)
        {
            if(File.Exists(destination))throw new InvalidOperationException("Scene exists; open instead.");
            ArchivedSceneBaseline.Check();
            if(!File.Exists(boatScene))throw new InvalidOperationException("Build the current boat range scene first.");
            var scene=EditorSceneManager.OpenScene(boatScene);
            EditorSceneManager.SaveScene(scene,destination,true);scene=EditorSceneManager.OpenScene(destination);
            var probe=UnityEngine.Object.FindFirstObjectByType<IndividualIkSweepProbe>();
            var preview=probe.Preview;var boat=probe.boatSource;probe.boatSource=null;
            var overlay=probe.GetComponent<BoatPresentationOverlay>();
            if(overlay) {
                UnityEngine.Object.DestroyImmediate(overlay);
                preview.viewCamera.orthographicSize=.22f;
            }
            UnityEngine.Object.DestroyImmediate(boat);
            foreach(var root in scene.GetRootGameObjects())
                if(root!=probe.transform.root.gameObject && root!=preview.transform.root.gameObject && root!=preview.viewCamera.transform.root.gameObject)
                    UnityEngine.Object.DestroyImmediate(root);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name="Debug Cube - select to move or rotate in Play mode";
            cube.transform.localScale=new Vector3(.12f,.04f,.18f);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            Axis(cube.transform,'X',Vector3.right,Color.red);
            Axis(cube.transform,'Y',Vector3.up,Color.green);
            Axis(cube.transform,'Z',Vector3.forward,Color.blue);
            var source=cube.AddComponent<ManualPlatformTarget>();
            source.cube=cube.transform;source.preview=preview;source.individualIkTest=true;
            source.smallAngleExtent=10;source.smallHeaveExtent=10;source.smallStep=.5f;source.enforceSmallPoseEnvelope=true;
            probe.source=source;probe.extendedTravel=true;probe.serial.enabled=false;
            probe.gameObject.name="OI5 pose range sweep - USB OFF until Connect";
            var sweep=probe.gameObject.AddComponent<IkAxisSweep>();sweep.probe=probe;sweep.extendedAngles=true;
            preview.sourceDescription="Manual cube → shared individual IK → six servo pulses";
            preview.outputDescription="Shared static IK target; physical output status is in the right panel";
            preview.resumeLabel="Manual control";
            var camera=new GameObject("Cube Camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(.3f,.22f,-.35f);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=.20f;camera.nearClipPlane=.01f;camera.farClipPlane=3;
            camera.cullingMask=~(1<<30);camera.rect=new Rect(.27f,0,.35f,1);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.14f);
            var light=new GameObject("Cube Light").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(40,-35,0);
            EditorSceneManager.SaveScene(scene);AssetDatabase.Refresh();
            Debug.Log("SP09 POSE RANGE SWEEP BUILT: OI5, USB OFF; "+destination);
        }
        private static void Axis(Transform parent,char axisName,Vector3 axis,Color colour)
        {
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name=axisName+" axis";
            marker.transform.SetParent(parent,false);marker.transform.localPosition=axis*.75f;
            marker.transform.localScale=new Vector3(.08f,.08f,.08f)+axis*.6f;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            string path=PreviewChecks.Root+"Materials/DebugAxis"+axisName+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)throw new InvalidOperationException("Missing current axis material: "+path);
            marker.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
