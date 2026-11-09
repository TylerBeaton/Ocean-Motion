using System;
using System.IO;
using OceanMotion.Subproject05;
using OceanMotion.Subproject09.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    [InitializeOnLoad]
    public static class BoatIkPlayModeCheck
    {
        private const string Key="OceanMotion.SP09.BoatPlayCheck";
        private static int stage, rejected;
        private static float started;
        private static double pausedAt;
        private static bool runtimeError;
        private static RenderTexture imageTarget;
        [Serializable] private sealed class Report
        {
            public bool passed, hardware_output_enabled, invalid_source_hidden, stale_source_hidden, recovery_passed, pause_neutral_passed;
            public string editor_version;
            public float simulation_seconds, translation_gain, rotation_gain, maximum_heave_mm, maximum_rotation_degrees;
            public int accepted_frames, rejected_frames;
        }
        static BoatIkPlayModeCheck()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if (state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
                stage=0; rejected=0; started=Time.time; runtimeError=false;
                Application.logMessageReceived+=OnLog;
                EditorApplication.update+=Tick;
            };
        }
        private static void OnLog(string message,string stack,LogType type)
        { if (type==LogType.Exception || type==LogType.Error || type==LogType.Assert) runtimeError=true; }
        public static void Run()
        {
            BoatIkChecks.Run();
            EditorSceneManager.OpenScene(BoatIkSceneBuilder.ScenePath);
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            try
            {
                BoatIkChecks.Need(!runtimeError,"Runtime error; see log.");
                var bridge=UnityEngine.Object.FindFirstObjectByType<ProcessedBoatIkBridge>();
                if (!bridge) return;
                if (stage==0 && Time.time-started>3)
                {
                    BoatIkChecks.Need(bridge.preview.CurrentResult==null,"Uncalibrated source should be hidden.");
                    bridge.CalibrateBoat(); bridge.waves.SetPreset(WavePreset.Crossed);
                    started=Time.time; stage=1;
                }
                else if (stage==1)
                {
                    if (Time.time-started<.5) return;
                    if (bridge.preview.CurrentResult==null || !bridge.preview.CurrentResult.KinematicSolutionFound) rejected++;
                    if (Time.time-started<20) return;
                    BoatIkChecks.Need(bridge.AcceptedFrames>100,"Insufficient live processed solutions.");
                    BoatIkChecks.Need(bridge.MaximumObservedHeaveMm>.01f && bridge.MaximumObservedRotationDegrees>.01f,"Boat source did not produce motion.");
                    BoatIkChecks.Need(bridge.MaximumObservedRotationDegrees>2,"Larger rotation default was not exercised.");
                    BoatIkChecks.Need(rejected==0,"Live small-pose path rejected a pose.");
                    var camera=bridge.preview.viewCamera;
                    imageTarget=new RenderTexture(960,800,24); camera.targetTexture=imageTarget; camera.rect=new Rect(0,0,1,1);
                    pausedAt=Time.realtimeSinceStartupAsDouble; stage=2;
                }
                else if (stage==2 && Time.realtimeSinceStartupAsDouble-pausedAt>.25)
                {
                    RenderTexture.active=imageTarget;
                    var texture=new Texture2D(960,800,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,960,800),0,0); texture.Apply();
                    File.WriteAllBytes(PreviewChecks.Root+"Reports/unity-boat-ik.png",texture.EncodeToPNG());
                    RenderTexture.active=null; UnityEngine.Object.Destroy(texture);
                    bridge.preview.viewCamera.targetTexture=null; imageTarget.Release(); UnityEngine.Object.Destroy(imageTarget);
                    Time.timeScale=0; pausedAt=Time.realtimeSinceStartupAsDouble; stage=3;
                }
                else if (stage==3 && Time.realtimeSinceStartupAsDouble-pausedAt>.4)
                {
                    BoatIkChecks.Need(bridge.preview.CurrentResult==null,"Frozen source remained active.");
                    Time.timeScale=1; pausedAt=Time.realtimeSinceStartupAsDouble; stage=4;
                }
                else if (stage==4 && Time.realtimeSinceStartupAsDouble-pausedAt>.3)
                {
                    BoatIkChecks.Need(bridge.preview.CurrentResult!=null && bridge.preview.CurrentResult.KinematicSolutionFound,"Fresh source did not recover.");
                    bridge.pipeline.enabled=false; pausedAt=Time.realtimeSinceStartupAsDouble; stage=5;
                }
                else if (stage==5 && Time.realtimeSinceStartupAsDouble-pausedAt>.1)
                {
                    BoatIkChecks.Need(bridge.preview.CurrentResult==null,"Disabled source retained solved geometry.");
                    bridge.pipeline.enabled=true; bridge.Resume(); pausedAt=Time.realtimeSinceStartupAsDouble; stage=6;
                }
                else if (stage==6 && Time.realtimeSinceStartupAsDouble-pausedAt>.3)
                {
                    BoatIkChecks.Need(bridge.preview.CurrentResult!=null && bridge.preview.CurrentResult.KinematicSolutionFound,"Source enable recovery failed.");
                    bridge.PauseAtNeutral();
                    BoatIkChecks.Need(bridge.preview.translationMm==Vector3.zero && bridge.preview.CurrentResult.KinematicSolutionFound,"Pause neutral failed.");
                    foreach (var leg in bridge.preview.CurrentResult.Legs)
                        BoatIkChecks.Need(Math.Abs(leg.NeutralDeltaDegrees)<1e-4,"Pause did not solve neutral.");
                    var report=new Report { passed=true,editor_version=Application.unityVersion,
                        simulation_seconds=Time.time-started,translation_gain=bridge.translationGain,rotation_gain=bridge.rotationGain,
                        accepted_frames=bridge.AcceptedFrames,rejected_frames=rejected,maximum_heave_mm=bridge.MaximumObservedHeaveMm,
                        maximum_rotation_degrees=bridge.MaximumObservedRotationDegrees,invalid_source_hidden=true,
                        stale_source_hidden=true,recovery_passed=true,pause_neutral_passed=true };
                    File.WriteAllText(PreviewChecks.Root+"Reports/unity-boat-ik-check.json",JsonUtility.ToJson(report,true)+"\n");
                    Debug.Log("SP09 BOAT PLAYMODE PASS: live crossed waves 20s, invalid/stale/recovery and pause-neutral checks; accepted="+bridge.AcceptedFrames);
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            Time.timeScale=1; SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog;
            EditorApplication.Exit(code);
        }
    }
}
