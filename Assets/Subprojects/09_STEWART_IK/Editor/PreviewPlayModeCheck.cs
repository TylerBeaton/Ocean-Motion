using System;
using System.IO;
using OceanMotion.Subproject09.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Editor
{
    [InitializeOnLoad]
    public static class PreviewPlayModeCheck
    {
        private const string Key="OceanMotion.SP09.PlayCheck";
        private static int frames;
        private static RenderTexture render;
        private static bool runtimeError;
        static PreviewPlayModeCheck()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if (state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                {
                    frames=0; EditorApplication.update+=Tick;
                    Application.logMessageReceived+=(message,trace,type)=>
                    { if (type==LogType.Exception || type==LogType.Error || type==LogType.Assert) runtimeError=true; };
                }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(PreviewSceneBuilder.ScenePath);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        private static void Need(bool value,string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Tick()
        {
            try
            {
                frames++;
                var preview=UnityEngine.Object.FindFirstObjectByType<StewartIkPreview>();
                if (frames==10)
                {
                    Need(preview && preview.CurrentResult!=null && preview.CurrentResult.KinematicSolutionFound,"Neutral runtime failed.");
                    render=new RenderTexture(960,800,24);
                    Camera.main.targetTexture=render; Camera.main.rect=new Rect(0,0,1,1);
                }
                if (frames<30) return;
                Need(!runtimeError,"Runtime logged an error.");
                RenderTexture.active=render;
                var image=new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,render.width,render.height),0,0); image.Apply();
                File.WriteAllBytes(PreviewChecks.Root+"Reports/unity-preview-neutral.png",image.EncodeToPNG());
                RenderTexture.active=null; UnityEngine.Object.Destroy(image);
                preview.SetPose(new Vector3(0,1,0),Vector3.zero); preview.EvaluateNow();
                Need(preview.CurrentResult.KinematicSolutionFound,"+1mm heave failed.");
                preview.SetPose(Vector3.zero,new Vector3(1,0,0)); preview.EvaluateNow();
                Need(preview.CurrentResult.KinematicSolutionFound,"+1deg pitch failed.");
                preview.ShowUnreachable(); preview.EvaluateNow();
                Need(!preview.CurrentResult.KinematicSolutionFound && preview.CurrentResult.Legs.Length==0,"Unreachable pose not rejected.");
                foreach (var line in preview.GetComponentsInChildren<LineRenderer>())
                    if (line.name.StartsWith("Horn ") || line.name.StartsWith("Rod ")) Need(!line.enabled,"Invalid pose shows stale linkage.");
                preview.ResetNeutral(); preview.EvaluateNow();
                Need(preview.CurrentResult.KinematicSolutionFound,"Neutral recovery failed.");
                preview.useSceneTarget=true;
                preview.target.localPosition+=Vector3.up*.001f;
                preview.target.localRotation=Quaternion.AngleAxis(1,Vector3.forward)*preview.target.localRotation;
                preview.EvaluateNow(); Need(preview.CurrentResult.KinematicSolutionFound,"Scene target failed.");
                // Moving the visual root must not change the neutral-relative solve.
                var before=preview.CurrentResult.AnglesDegrees;
                preview.transform.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(12,23,34));
                preview.EvaluateNow();
                var after=preview.CurrentResult.AnglesDegrees;
                for (int i=0;i<6;i++) Need(Math.Abs(before[i]-after[i])<1e-8,"Root transform changed IK result.");
                Need(!preview.CurrentResult.HardwareOutputEnabled,"Hardware output unexpectedly enabled.");
                File.WriteAllText(PreviewChecks.Root+"Reports/unity-playmode-check.json",
                    "{\n  \"passed\": true,\n  \"neutral_heave_pitch\": true,\n  \"unreachable_hides_linkages\": true,\n  \"neutral_recovery\": true,\n  \"scene_target_and_root_transform\": true,\n  \"hardware_output_enabled\": false\n}\n");
                Debug.Log("SP09 PLAYMODE PASS: neutral/heave/pitch, rejection/recovery, scene target/root, visual capture.");
                Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick;
            if (Camera.main) Camera.main.targetTexture=null;
            if (render) { render.Release(); UnityEngine.Object.Destroy(render); }
            EditorApplication.Exit(code);
        }
    }
}
