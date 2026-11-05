using System;
using System.IO;
using System.Reflection;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Editor;
using OceanMotion.Subproject09.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    [InitializeOnLoad]
    public static class IkPoseRangeChecks
    {
        private const string Key="SP09.IkPoseRangeCheck";
        private static int stage;
        private static double at;
        private static float maximum,minimum;
        private static bool failed;
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static IkPoseRangeChecks()
        {
            EditorApplication.playModeStateChanged+=s=> {
                if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
                stage=0;maximum=minimum=0;failed=false;at=EditorApplication.timeSinceStartup;
                Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
            };
        }
        private static void Log(string s,string trace,LogType t){if(t==LogType.Error || t==LogType.Exception)failed=true;}
        private static void Need(bool c,string why)=>BoatIkChecks.Need(c,why);
        private static uint Token(IndividualIkSweepProbe p)=>(uint)typeof(IndividualIkSweepProbe).GetField("token",Flags).GetValue(p);
        public static void Run()
        {
            CheckMapping();
            RunSweep();
        }
        public static void CheckMapping()
        {
            var c=new MotionPoseCommand(Vector3.zero,new Vector3(6,0,0),1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(c,0,1,out _,out _,out string error) && error.Contains("Rotation pose allowance"),"Default5deg error/gate failed");
            Need(ProcessedBoatIkBridge.TryMap(c,0,1,6,out _,out _,out _),"Explicit6deg allowance failed");
            foreach(float invalid in new[]{0,11,float.NaN,float.PositiveInfinity})
                Need(!ProcessedBoatIkBridge.TryMap(c,0,1,invalid,out _,out _,out _),"Invalid pose allowance accepted");
            for(int angle=1;angle<=10;angle++) {
                var exact=new MotionPoseCommand(Vector3.zero,new Vector3(angle,0,-angle),1,1,true);
                Need(ProcessedBoatIkBridge.TryMap(exact,0,1,angle,out _,out _,out _),"Exact pose boundary rejected");
                var over=new MotionPoseCommand(Vector3.zero,new Vector3(angle+.01f,0,0),1,1,true);
                Need(!ProcessedBoatIkBridge.TryMap(over,0,1,angle,out _,out _,out _),"Excess angle accepted");
            }
            var translated=new MotionPoseCommand(Vector3.up*.11f,Vector3.zero,1,1,true);
            Need(!ProcessedBoatIkBridge.TryMap(translated,.1f,1,10,out _,out _,out string translationError) &&
                translationError.Contains("Translation pose allowance"),"Translation allowance bypassed by valid gain");
            Debug.Log("SP09 POSE MAPPING PASS: exact1..10deg, default5deg compatibility, explicit limits, valid-gain translation rejection.");
        }
        public static void CheckBoatControls()
        {
            CheckMapping();
            BoatIkRangeChecks.Run();
        }
        private static void RunSweep()
        {
            if(!File.Exists(IkPoseRangeSetup.ScenePath))IkPoseRangeSetup.Create();else EditorSceneManager.OpenScene(IkPoseRangeSetup.ScenePath);
            var p=UnityEngine.Object.FindFirstObjectByType<IndividualIkSweepProbe>();var s=p.GetComponent<IkAxisSweep>();
            Need(p.extendedTravel && s.extendedAngles && p.source.enforceSmallPoseEnvelope && !p.serial.enabled,"Pose sweep scene gate failed");
            var model=PreviewGeometry.Parse(p.Preview.geometry.text).Build();
            foreach(float angle in new[]{4,5})foreach(bool roll in new[]{false,true})
                Need(IkAxisSweep.ValidatePath(model,250,roll,angle,10,out _),"4/5deg preflight failed");
            foreach(bool roll in new[]{false,true}) {
                bool fits=IkAxisSweep.ValidatePath(model,250,roll,10,10,out string why);
                Debug.Log("POSE10 PREFLIGHT "+(roll ? "roll" : "pitch")+": "+(fits ? "fits provisional model" : why));
            }
            Need(!IkAxisSweep.ValidatePath(model,250,false,10.01f,10,out _),"Excess sweep amplitude accepted");
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            try {
                Need(!failed,"Pose sweep runtime error");
                var p=UnityEngine.Object.FindFirstObjectByType<IndividualIkSweepProbe>();if(!p)return;
                var s=p.GetComponent<IkAxisSweep>();Need(!p.serial.enabled,"Software check opened USB");
                double elapsed=EditorApplication.timeSinceStartup-at;
                if(stage==0 && elapsed>.5) {
                    if(!p.FreshCandidate() && elapsed<3)return;
                    p.OnConnectionEvent(true);p.OnMessageArrived("OI3,READY,"+Token(p)+",0,0,0,0,0,0");
                    Need(p.SetAllowance(250),"250us config failed");stage=1;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==1 && elapsed>.1) {
                    if(!p.FreshCandidate() && elapsed<3)return;
                    p.Arm();p.OnMessageArrived("OI3,ARMED,"+Token(p)+",250");Need(p.Armed,"Neutral ARM failed");
                    Need(s.StartSweep(false,5),"5deg sweep refused");stage=2;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==2) {
                    Need(p.Armed || !s.Running,"Unexpected HOLD in 5deg sweep");
                    if(p.CandidateOffsets!=null)typeof(IndividualIkSweepProbe).GetProperty("BoardOffsets").SetValue(p,p.CandidateOffsets);
                    maximum=Mathf.Max(maximum,p.source.anglesDegrees.x);minimum=Mathf.Min(minimum,p.source.anglesDegrees.x);
                    Need(p.source.anglesDegrees.y==0 && p.source.anglesDegrees.z==0,"Sweep mixed axes");
                    if(!s.Running) {
                        Need(maximum>4.99f && minimum< -4.99f && p.source.anglesDegrees==Vector3.zero,"Full5deg cycle/neutral failed");
                        p.OnMessageArrived("OI3,HOMING,"+Token(p));p.OnMessageArrived("OI3,NEUTRAL,"+Token(p));
                        stage=3;at=EditorApplication.timeSinceStartup;
                    }else Need(elapsed<65,"5deg cycle exceeded timeout");
                }
                else if(stage==3 && elapsed>.2) {
                    p.Arm();p.OnMessageArrived("OI3,ARMED,"+Token(p)+",250");Need(p.Armed,"Rearm failed");
                    var model=PreviewGeometry.Parse(p.Preview.geometry.text).Build();
                    bool fits=IkAxisSweep.ValidatePath(model,250,false,10,10,out _);
                    Need(s.StartSweep(false,10)==fits,"10deg startup disagrees with full path preflight");
                    if(s.Running)s.StopSweep("test cancel");else p.Hold("10deg preflight refused correctly");
                    p.source.useTransformHandles=true;p.source.cube.localRotation=Quaternion.Euler(11,0,0);
                    stage=4;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==4 && elapsed>.2) {
                    Need(!p.source.LatestCommand.IsValid && !p.FreshCandidate() && !p.Armed,"Manual handles bypassed10deg/stop");
                    p.source.ResetNeutral();stage=5;at=EditorApplication.timeSinceStartup;
                }
                else if(stage==5 && elapsed>.2) {
                    Need(p.FreshCandidate() && !p.Armed,"Neutral recovery auto-armed or failed");p.Disconnect();
                    File.WriteAllText(PreviewChecks.Root+"Reports/ik-pose-allowance-check.json","{\n  \"software_checks_passed\": true,\n  \"usb_opened\": false,\n  \"physical_test_completed\": false,\n  \"pose_controls_max_deg\": 10,\n  \"full_simulated_pitch_sweep_deg\": 5,\n  \"firmware_pulse_cap_us\": 250\n}\n");
                    Debug.Log("SP09 POSE ALLOWANCE PLAY PASS: default/adjustable bounds,4/5deg preflight, full5deg cycle,10deg preflight gate, handle bounds and recovery; NO USB OPENED.");Finish(0);
                }
            }catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void Finish(int code)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.Exit(code);}
    }
}
