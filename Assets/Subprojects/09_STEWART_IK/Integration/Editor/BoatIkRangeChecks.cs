using System;
using System.IO;
using System.Reflection;
using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Editor;
using OceanMotion.Subproject09.Preview;
using OceanMotion.Subproject09.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    [InitializeOnLoad]
    public static class BoatIkRangeChecks
    {
        private const string Key="SP09.BoatIkRangeCheck";
        private static int stage,frames,peak,rejected;
        private static bool failed;
        private static string firstRunFile;
        private static double stoppedMotionElapsed;
        private static double at;
        private static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static BoatIkRangeChecks()
        {
            EditorApplication.playModeStateChanged+=s=> {
                if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
                stage=frames=peak=rejected=0;failed=false;at=EditorApplication.timeSinceStartup;
                Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
            };
        }
        private static void Log(string s,string trace,LogType t){if(t==LogType.Error || t==LogType.Exception)failed=true;}
        private static void Need(bool c,string why)=>BoatIkChecks.Need(c,why);
        private static uint Token(IndividualIkSweepProbe p)=>(uint)typeof(IndividualIkSweepProbe).GetField("token",Flags).GetValue(p);
        public static void Run()
        {
            if(!File.Exists(BoatIkRangeSetup.ScenePath))BoatIkRangeSetup.Create();else EditorSceneManager.OpenScene(BoatIkRangeSetup.ScenePath);
            var p=UnityEngine.Object.FindFirstObjectByType<IndividualIkSweepProbe>();
            Need(p && p.extendedTravel && p.boatSource && !p.serial.enabled,"Range scene configuration failed");
            for(int cap=25;cap<=500;cap+=25)Need(p.SetAllowance(cap),"Selectable allowance failed");
            foreach(int cap in new[]{0,24,26,251,1000})Need(!p.SetAllowance(cap),"Invalid allowance accepted");
            Need(p.SetAllowance(25) && p.boatSource.SetGains(.002f,1) && !p.boatSource.SetGains(.002f,1.01f) && p.boatSource.SetGains(.002f,.45f),"Gain range failed");
            var command=new MotionPoseCommand(Vector3.zero,new Vector3(10,0,0),1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(command,.002f,1,out _,out _,out _),"Gain1 bypassed 5deg pose envelope");
            var model=PreviewGeometry.Parse(p.Preview.geometry.text).Build();
            var map=new ApproximateIkPulseMap(model);
            Need(!map.TryOffsets(null,525,out _,out _),"Unsupported mapping allowance accepted");
            Need(IkAxisSweep.ValidatePath(model,500,false,10,10,out _),"Expanded single-axis mapping unavailable");
            int absoluteRejected=0;
            for(int pitch=-20;pitch<=20;pitch+=2)for(int roll=-20;roll<=20;roll+=2) {
                var q=Quaternion.Euler(pitch,0,roll);
                var result=model.Solve(IkPose.FromUnity(new IkVector(0,0,0),q.x,q.y,q.z,q.w));
                if(map.TryOffsets(result,500,out int[] offsets,out string why)) {
                    for(int i=0;i<6;i++)Need(ApproximateIkPulseMap.NeutralUs[i]+offsets[i]>=1000 && ApproximateIkPulseMap.NeutralUs[i]+offsets[i]<=2000,"Absolute pulse guard bypassed");
                } else if(why.Contains("absolute"))absoluteRejected++;
            }
            Need(absoluteRejected>0,"Absolute mapping guard not exercised");
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            try {
                Need(!failed,"Range runtime error");
                var p=UnityEngine.Object.FindFirstObjectByType<IndividualIkSweepProbe>();if(!p)return;
                var b=p.boatSource;Need(!p.serial.enabled,"Software tests opened USB");
                double elapsed=EditorApplication.timeSinceStartup-at;
                if(stage==0 && elapsed>3) {
                    Need(p.IsExtended && p.MaximumAllowanceUs==500 && !p.Armed,"Extended startup profile failed");
                    b.CalibrateBoat();Need(b.SetGains(.002f,.45f),"0.45 gain selection failed");stage=1;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==1 && elapsed>.5) {
                    if(!p.FreshCandidate() && elapsed<4)return;
                    Need(p.FreshCandidate(),"Calm source not fresh: "+p.MappingError);
                    p.OnConnectionEvent(true);p.OnMessageArrived("OI2,READY,"+Token(p)+",0,0,0,0,0,0");Need(!p.Ready,"Old receiver accepted");
                    p.OnMessageArrived("OI5,READY,"+Token(p)+",0,500,0,0,0,0");Need(!p.Ready,"Invalid absolute board command accepted");
                    p.OnMessageArrived("OI5,READY,"+Token(p)+",500,0,0,0,0,0");
                    Need(p.Ready && !p.SetAllowance(200),"Retained non-neutral position did not lock configuration");
                    p.Home();p.OnMessageArrived("OI5,HOMING,"+Token(p));
                    Need((double)typeof(IndividualIkSweepProbe).GetField("deadline",Flags).GetValue(p)-Time.realtimeSinceStartupAsDouble>11.5,"Expanded HOME timeout too short");
                    p.OnMessageArrived("OI5,NEUTRAL,"+Token(p));
                    Need(p.SetAllowance(200),"200us selection failed");stage=2;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==2 && elapsed>.1) {
                    if(!p.FreshCandidate() && elapsed<3)return;
                    p.Arm();Need(!p.Armed && p.Status.StartsWith("Settings changed:"),"Unprepared draft mismatch was not blocked");
                    Need(p.SetPreparationSettings(200,5,.45f,.002f) && p.SetNextMovementSpeed(50),"Draft settings failed");
                    p.Arm();p.OnMessageArrived("OI5,ARMED,"+Token(p)+",100,50");Need(!p.Armed,"Mismatched allowance armed");
                    p.OnMessageArrived("OI5,ARMED,"+Token(p)+",200,100");Need(!p.Armed,"Mismatched speed armed");
                    p.OnMessageArrived("OI5,ARMED,"+Token(p)+",200,50");Need(p.Armed,"200us arm failed: "+p.Status);
                    firstRunFile=p.GetComponent<IkSessionLogger>().FilePath;
                    Need(!b.SetPoseAllowance(6),"Armed pose allowance change accepted");
                    Need(!p.SetAllowance(175) && !b.SetGains(.002f,.5f),"Armed reconfiguration accepted");
                    // Inspector edits cannot swap protocol/hard bounds in the active Play session.
                    p.extendedTravel=false;Need(p.IsExtended && p.MaximumAllowanceUs==500,"Runtime profile edit changed wire contract");
                    b.waves.SetPreset(WavePreset.Bow);stage=3;at=EditorApplication.timeSinceStartup;
                }
                else if(stage>=3 && stage<=5) {
                    bool candidate=p.FreshCandidate();
                    if(!candidate) {
                        Need(!p.Armed,"Rejected source did not HOLD");
                        Need(p.MappingError!=null && p.LastStop!=null,"Rejected source missing diagnostic");
                        rejected++;
                    } else {
                        var map=new ApproximateIkPulseMap(PreviewGeometry.Parse(b.preview.geometry.text).Build());
                        Need(map.TryOffsets(b.preview.CurrentResult,200,out int[] exact,out _),"Shared solution exceeds200us");
                        for(int i=0;i<6;i++){Need(exact[i]==p.CandidateOffsets[i],"View/command mismatch");peak=Math.Max(peak,Math.Abs(exact[i]));}
                        typeof(IndividualIkSweepProbe).GetProperty("BoardOffsets").SetValue(p,exact);frames++;
                    }
                    if(elapsed<6)return;
                    if(stage==3)b.waves.SetPreset(WavePreset.Side);
                    if(stage==4)b.waves.SetPreset(WavePreset.Crossed);
                    if(++stage==6)Time.timeScale=0;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==6 && elapsed>.5) {
                    Need(!p.Armed && !p.FreshCandidate(),"Frozen source did not HOLD");Time.timeScale=1;stage=7;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==7 && elapsed>.5) {
                    Need(!p.Armed,"Source recovery auto-armed");b.PauseAtNeutral();
                    p.OnMessageArrived("OI5,HOMING,"+Token(p));p.OnMessageArrived("OI5,NEUTRAL,"+Token(p));
                    Need(p.SetAllowance(250) && b.SetGains(.002f,1),"Neutral full controls unavailable");
                    Need(b.SetPoseAllowance(10) && !b.SetPoseAllowance(11),"Neutral pose allowance controls failed");
                    p.Disconnect();Need(b.PoseAngleAllowance==5,"Reconnect profile did not reset pose allowance");
                    p.OnConnectionEvent(true);p.OnMessageArrived("OI5,READY,"+Token(p)+",0,0,0,0,0,0");
                    Need(p.SetPreparationSettings(500,6,.35f,.0015f),"Expanded draft unavailable");
                    for(int seconds=1;seconds<=3;seconds++)Need(p.SetNextWaveTransition(seconds),"Transition selection failed");
                    Need(!p.SetNextWaveTransition(0) && !p.SetNextWaveTransition(4),"Invalid transition selection accepted");
                    Need(p.SetPreparationSettings(175,6,.35f,.0015f),"Custom draft failed");
                    foreach(string bad in new[]{"", "NaN", "Infinity", "-1", "0.5", "3601", "1e9"})Need(!p.SetTestDuration(bad),"Invalid duration accepted");
                    Need(p.SetTestDuration("0") && p.SetTestDuration("1"),"Valid duration rejected");
                    for(int speed=50;speed<=500;speed+=50)Need(p.SetNextMovementSpeed(speed),"Selectable speed failed");
                    Need(p.SetNextMovementSpeed(150) && !p.SetNextMovementSpeed(0) && !p.SetNextMovementSpeed(75) && !p.SetNextMovementSpeed(550),"Speed draft bounds failed");
                    Need(!p.SetPreparationSettings(525,6,.35f,.0015f) && !p.SetPreparationSettings(175,11,.35f,.0015f) &&
                        !p.SetPreparationSettings(175,6,float.NaN,.0015f) && !p.SetPreparationSettings(175,6,.35f,-1),"Invalid draft accepted");
                    p.PrepareBoatTest();Need(p.Preparing && !p.Armed,"Preparation did not start disarmed");
                    Need(!p.SetPreparationSettings(200,8,.4f,.002f),"Draft changed during preparation");
                    Need(!p.SetNextMovementSpeed(500),"Speed changed during preparation");
                    Need(!p.SetNextWaveTransition(1),"Transition changed during preparation");
                    Need(!p.SetTestDuration("2"),"Duration changed during preparation");
                    p.Arm();Need(!p.Armed,"Arm allowed during preparation");
                    stage=8;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==8) {
                    Need(!p.Armed,"Preparation auto-armed");
                    if(p.Preparing && elapsed<16)return;
                    Need(!p.Preparing && p.Status.StartsWith("Prepared:"),"Preparation failed: "+p.Status);
                    Need(p.AllowanceUs==175 && b.PoseAngleAllowance==6 && Math.Abs(b.RotationGain-.35f)<.001f && Math.Abs(b.TranslationGain-.0015f)<.00001f && !b.paused,"Custom preparation settings failed");
                    Need(p.MovementSpeedUsPerSecond==150,"Prepared speed not applied");
                    Need(p.WaveTransitionSeconds==3 && !b.waves.IsTransitioning,"Preparation did not wait for Calm transition");
                    p.Arm();p.OnMessageArrived("OI5,ARMED,"+Token(p)+",175,50");Need(!p.Armed,"Wrong speed echo armed custom run");
                    p.OnMessageArrived("OI5,ARMED,"+Token(p)+",175,150");Need(p.Armed,"Prepared custom run did not arm");
                    var logger=p.GetComponent<IkSessionLogger>();
                    Need(logger.FilePath!=firstRunFile && File.ReadAllText(firstRunFile).Contains("TEST STOPPED:"),"Run files not separated/finalized");
                    Need(!p.TimerRunning && !p.MotionStarted && p.RunDurationSeconds==1,"Timer did not wait for wave selection");
                    Need(!p.SetTestDuration("3"),"Duration changed while armed");
                    b.SelectWavePreset(WavePreset.Bow);Need(b.waves.IsTransitioning,"Live wave selection skipped transition");stage=9;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==9 && elapsed>1.5) {
                    var logger=p.GetComponent<IkSessionLogger>();
                    Need(!p.Armed && b.paused && !p.TimerRunning && p.MotionStarted,"Timed run failed to stop and pause");
                    Need(p.MotionElapsedSeconds>=1 && p.MotionElapsedSeconds<1.25,"Timed stop was not at the requested duration");
                    Need(File.ReadAllText(logger.FilePath).Contains("TEST STOPPED: Timed duration reached: 1s") &&
                        File.ReadAllText(logger.FilePath).Contains("MOTION STARTED:"),"Timed telemetry markers missing/not flushed");
                    Need(File.ReadAllText(logger.FilePath).Contains("\"movementSpeedUsPerSecond\":150"),"Speed missing from telemetry");
                    Need(File.ReadAllText(logger.FilePath).Contains("\"waveTransitionSeconds\":3") &&
                        File.ReadAllText(logger.FilePath).Contains("\"waveTransitioning\":true"),"Transition missing from telemetry");
                    p.OnMessageArrived("OI5,HOMING,"+Token(p));p.OnMessageArrived("OI5,NEUTRAL,"+Token(p));
                    stoppedMotionElapsed=p.MotionElapsedSeconds;b.Resume();stage=10;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==10 && elapsed>.3) {
                    Need(!p.Armed && !p.TimerRunning && p.MotionElapsedSeconds==stoppedMotionElapsed,"Timer restarted after source recovery");
                    Need(p.SetTestDuration("0") && p.TestDurationSeconds==0,"Manual duration selection failed");
                    p.PrepareBoatTest();p.Hold("Preparation cancellation test");Need(!p.Preparing,"HOLD failed to cancel preparation");
                    p.PrepareBoatTest();p.Disconnect();Need(!p.Preparing && !p.Armed && p.AllowanceUs==25 && p.MovementSpeedUsPerSecond==50,"Disconnect failed to cancel/reset");
                    Need(frames>100 && peak>20 && peak<=200,"Insufficient wave coverage");
                    File.WriteAllText(PreviewChecks.Root+"Reports/boat-ik-range-check.json","{\n  \"software_checks_passed\": true,\n  \"usb_opened\": false,\n  \"physical_expanded_range_test_completed\": false,\n  \"wave_gain_checked\": 0.45,\n  \"heave_gain\": 0.002,\n  \"allowance_checked_us\": 200,\n  \"maximum_selectable_us\": 250,\n  \"rejected_frames\": "+rejected+",\n  \"mapped_frames\": "+frames+",\n  \"peak_offset_us\": "+peak+"\n}\n");
                    Debug.Log("SP09 RANGE PLAY PASS: 0.45 Bow/Side/Crossed, 200us allowance, shared IK, source/pulse rejection HOLD, range/echo/profile locks; NO USB OPENED. Peak="+peak+" rejectedFrames="+rejected);Finish(0);
                }
            }catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void Finish(int code)
        {Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.Exit(code);}
    }
}
