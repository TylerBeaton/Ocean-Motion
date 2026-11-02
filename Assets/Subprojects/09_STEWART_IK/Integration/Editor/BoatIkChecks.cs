using System;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    public static class BoatIkChecks
    {
        public static void Need(bool condition,string message)
        { if (!condition) throw new InvalidOperationException("SP09 boat IK: "+message); }
        public static void Run()
        {
            PreviewChecks.Run();
            foreach (Vector3 axis in new[] {Vector3.right,Vector3.up,Vector3.forward})
            {
                var command=new MotionPoseCommand(axis*.1f,axis*20,1,1,true);
                Need(ProcessedBoatIkBridge.TryMap(command,.02f,.05f,out Vector3 p,out Quaternion q,out _),"Valid input rejected.");
                Need((p-axis*.002f).magnitude<1e-8,"Translation scale/axis mismatch.");
                Need(Quaternion.Angle(q,Quaternion.AngleAxis(1,axis))<.001,"Rotation scale/axis mismatch.");
            }
            Need(!ProcessedBoatIkBridge.TryMap(default,.02f,.05f,out _,out _,out _),"Invalid input accepted.");
            var invalid=new MotionPoseCommand(new Vector3(float.NaN,0,0),Vector3.zero,1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(invalid,.02f,.05f,out _,out _,out _),"NaN accepted.");
            var large=new MotionPoseCommand(new Vector3(0,1,0),Vector3.zero,1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(large,.02f,.05f,out _,out _,out _),"Visual envelope clipped/accepted.");
            var valid=new MotionPoseCommand(Vector3.zero,Vector3.zero,1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(valid,float.NaN,.05f,out _,out _,out _),"Nonfinite gain accepted.");
            // Float .1f is slightly greater than double .1: compare against float constants.
            Need(ProcessedBoatIkBridge.TryMap(valid,.1f,.1f,out _,out _,out _),"Exact Inspector 0.1 gain rejected.");
            Need(ProcessedBoatIkBridge.TryMap(valid,.1f,1f,out _,out _,out _),"Exact gain endpoints rejected.");
            Need(!ProcessedBoatIkBridge.TryMap(valid,.10001f,.45f,out _,out _,out _),"Excess translation gain accepted.");
            Need(!ProcessedBoatIkBridge.TryMap(valid,.02f,1.00001f,out _,out _,out _),"Excess rotation gain accepted.");
            var rotationSample=new MotionPoseCommand(Vector3.up*.1f,new Vector3(8,0,-6),1,1,true);
            Need(ProcessedBoatIkBridge.TryMap(rotationSample,.02f,.09f,out Vector3 smallPosition,out _,out _),"Original small gain failed.");
            Need(ProcessedBoatIkBridge.TryMap(rotationSample,.02f,.45f,out Vector3 largePosition,out Quaternion bigger,out _),"Fivefold rotation gain failed.");
            Need(smallPosition==largePosition,"Rotation gain changed heave.");
            Need(Quaternion.Angle(bigger,Quaternion.Euler(3.6f,0,-2.7f))<.001,"Fivefold mapped rotation wrong.");
            var overTilt=new MotionPoseCommand(Vector3.zero,new Vector3(12,0,0),1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(overTilt,.02f,.45f,out _,out _,out _),"Larger gain bypassed tilt envelope.");
            EditorSceneManager.OpenScene(BoatIkSceneBuilder.ScenePath);
            var bridge=UnityEngine.Object.FindFirstObjectByType<ProcessedBoatIkBridge>();
            Need(bridge && bridge.pipeline && bridge.preview && bridge.telemetry && bridge.waves,"Missing scene references.");
            Need(UnityEngine.Object.FindObjectsByType<SerialController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"Scene contains serial output.");
            Need(UnityEngine.Object.FindObjectsByType<MotionSerialTransport>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"Scene contains motion transport.");
            foreach (var root in bridge.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)==0,"Missing script.");
            Debug.Log("SP09 BOAT MAPPING PASS: processed axes/scales, invalid/envelope/gain rejection, scene references, no serial components.");
        }
    }
}
