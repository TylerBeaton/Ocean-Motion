using System;
using System.IO;
using System.Reflection;
using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Editor;
using OceanMotion.Subproject09.Preview;
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
            for(int cap=25;cap<=250;cap+=25)Need(p.SetAllowance(cap),"Selectable allowance failed");
            foreach(int cap in new[]{0,24,26,251,1000})Need(!p.SetAllowance(cap),"Invalid allowance accepted");
            Need(p.SetAllowance(25) && p.boatSource.SetGains(.002f,1) && !p.boatSource.SetGains(.002f,1.01f) && p.boatSource.SetGains(.002f,.45f),"Gain range failed");
            var command=new MotionPoseCommand(Vector3.zero,new Vector3(10,0,0),1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(command,.002f,1,out _,out _,out _),"Gain1 bypassed 5deg pose envelope");
            var map=new ApproximateIkPulseMap(PreviewGeometry.Parse(p.Preview.geometry.text).Build());
            Need(!map.TryOffsets(null,275,out _,out _),"Unsupported mapping allowance accepted");
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
                    Need(p.IsExtended && p.MaximumAllowanceUs==250 && !p.Armed,"Extended startup profile failed");
                    b.CalibrateBoat();Need(b.SetGains(.002f,.45f),"0.45 gain selection failed");stage=1;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==1 && elapsed>.5) {
                    if(!p.FreshCandidate() && elapsed<4)return;
                    Need(p.FreshCandidate(),"Calm source not fresh: "+p.MappingError);
                    p.OnConnectionEvent(true);p.OnMessageArrived("OI2,READY,"+Token(p)+",0,0,0,0,0,0");Need(!p.Ready,"Old receiver accepted");
                    p.OnMessageArrived("OI3,READY,"+Token(p)+",125,0,0,0,0,0");
                    Need(p.Ready && !p.SetAllowance(200),"Retained non-neutral position did not lock configuration");
                    p.Home();p.OnMessageArrived("OI3,HOMING,"+Token(p));p.OnMessageArrived("OI3,NEUTRAL,"+Token(p));
                    Need(p.SetAllowance(200),"200us selection failed");stage=2;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==2 && elapsed>.1) {
                    if(!p.FreshCandidate() && elapsed<3)return;
                    p.Arm();p.OnMessageArrived("OI3,ARMED,"+Token(p)+",100");Need(!p.Armed,"Mismatched allowance armed");
                    p.OnMessageArrived("OI3,ARMED,"+Token(p)+",200");Need(p.Armed,"200us arm failed: "+p.Status);
                    Need(!b.SetPoseAllowance(6),"Armed pose allowance change accepted");
                    Need(!p.SetAllowance(175) && !b.SetGains(.002f,.5f),"Armed reconfiguration accepted");
                    // Inspector edits cannot swap protocol/hard bounds in the active Play session.
                    p.extendedTravel=false;Need(p.IsExtended && p.MaximumAllowanceUs==250,"Runtime profile edit changed wire contract");
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
                    p.OnMessageArrived("OI3,HOMING,"+Token(p));p.OnMessageArrived("OI3,NEUTRAL,"+Token(p));
                    Need(p.SetAllowance(250) && b.SetGains(.002f,1),"Neutral full controls unavailable");
                    Need(b.SetPoseAllowance(10) && !b.SetPoseAllowance(11),"Neutral pose allowance controls failed");
                    p.Disconnect();Need(b.PoseAngleAllowance==5,"Reconnect profile did not reset pose allowance");
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
