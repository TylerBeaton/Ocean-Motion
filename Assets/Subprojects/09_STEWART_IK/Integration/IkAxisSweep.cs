using System;
using OceanMotion.Subproject09.Core;
using OceanMotion.Subproject09.Preview;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    [DefaultExecutionOrder(-200)]
    public sealed class IkAxisSweep : MonoBehaviour
    {
        public IndividualIkSweepProbe probe;
        public bool extendedAngles;
        public bool Running { get; private set; }
        public string Status { get; private set; }="One-shot sweep; operator observes throughout";
        private bool roll;
        private float amplitude=1, value;
        private int phase;
        private double settledAt=-1, phaseAt;
        private Vector3 ownedAngles;
        private bool subscribed;
        private float MaximumAmplitude=>extendedAngles && probe && probe.IsExtended ? 10 : 3;
        private double Now=>Time.realtimeSinceStartupAsDouble;
        private void OnEnable() { Subscribe(); }
        private void Start() { Subscribe(); }
        private void Subscribe()
        {
            if(!subscribed && probe && probe.source) { probe.source.BeforeReset+=ResetRequested;subscribed=true; }
        }
        private void ResetRequested() { if(Running)StopSweep("Cube reset: sweep cancelled; use Gradual neutral"); }
        public static bool ValidatePath(RotaryStewartModel model,int limit,bool roll,float amplitude,out string error)
            =>ValidatePath(model,limit,roll,amplitude,3,out error);
        public static bool ValidatePath(RotaryStewartModel model,int limit,bool roll,float amplitude,float maximumAmplitude,out string error)
        {
            error=null;
            if(float.IsNaN(amplitude) || float.IsInfinity(amplitude) || float.IsNaN(maximumAmplitude) || float.IsInfinity(maximumAmplitude) ||
                maximumAmplitude<.5f || maximumAmplitude>10 || amplitude<.5f || amplitude>maximumAmplitude)
            { error="Amplitude must be 0.5–"+maximumAmplitude+"°"; return false; }
            var map=new ApproximateIkPulseMap(model);
            for(int n=-120;n<=120;n++) {
                float a=amplitude*n/120;
                Quaternion q=Quaternion.Euler(roll ? new Vector3(0,0,a) : new Vector3(a,0,0));
                if(!map.TryOffsets(model.Solve(IkPose.FromUnity(new IkVector(0,0,0),q.x,q.y,q.z,q.w)),limit,out _,out error))return false;
            }
            return true;
        }
        public bool StartSweep(bool rollAxis,float degrees)
        {
            if(Running || !probe || !probe.source || !probe.Armed || !probe.FreshCandidate() ||
                !Array.TrueForAll(probe.BoardOffsets,x=>x==0) || probe.source.preview.translationMm.sqrMagnitude>1e-6f ||
                Quaternion.Angle(probe.source.preview.target.localRotation,PreviewGeometry.Parse(probe.source.preview.geometry.text).NeutralUnityRotation)>.001f)
            { Status="Start requires armed, settled cube + board neutral"; return false; }
            var model=PreviewGeometry.Parse(probe.source.preview.geometry.text).Build();
            if(!ValidatePath(model,probe.AllowanceUs,rollAxis,degrees,MaximumAmplitude,out string error))
            { Status="Sweep refused before movement: "+error; return false; }
            roll=rollAxis; amplitude=degrees; phase=0; value=0; settledAt=-1; phaseAt=Now;
            probe.source.useTransformHandles=false; probe.source.offsetMm=Vector3.zero;
            ownedAngles=probe.source.anglesDegrees=Vector3.zero; probe.source.automatedControl=true;
            Running=true; Status="Sweeping "+(roll ? "roll" : "pitch")+" ±"+degrees+"°; 0.5°/s; 2s dwell"; return true;
        }
        public void StopSweep(string reason="User stopped sweep")
        {
            Running=false; if(probe && probe.source)probe.source.automatedControl=false;
            Status=reason; if(probe && probe.Armed)probe.Hold(reason);
        }
        private void Update()
        {
            if(!Running)return;
            if(!probe.Armed) { StopSweep("Sweep cancelled by board/host stop; no automatic restart"); return; }
            var source=probe.source;
            if((source.anglesDegrees-ownedAngles).sqrMagnitude>1e-8f || source.offsetMm.sqrMagnitude>1e-8f || source.useTransformHandles)
            { StopSweep("Manual source edited during sweep"); return; }
            if(Now-phaseAt>amplitude/.5+7) { StopSweep("Sweep phase timeout"); return; }
            float target=phase==0 ? amplitude : phase==2 ? -amplitude : 0;
            value=Mathf.MoveTowards(value,target,.5f*Mathf.Min(Time.unscaledDeltaTime,.05f));
            ownedAngles=roll ? new Vector3(0,0,value) : new Vector3(value,0,0);
            source.anglesDegrees=ownedAngles;
            if(value!=target) { settledAt=-1; return; }
            var candidate=probe.CandidateOffsets;
            if(candidate==null)return;
            for(int i=0;i<6;i++)if(Math.Abs(probe.BoardOffsets[i]-candidate[i])>1) { settledAt=-1; return; }
            if(settledAt<0) { settledAt=Now; return; }
            if(Now-settledAt<2)return;
            if(++phase==4) {
                Running=false; source.automatedControl=false; Status="Sweep completed at commanded neutral; observe plate";
                probe.Home(); return;
            }
            phaseAt=Now; settledAt=-1;
        }
        public void DrawControls()
        {
            GUILayout.Label(Status);
            bool before=GUI.enabled; GUI.enabled=before && !Running;
            GUILayout.BeginHorizontal();
            foreach(float a in new[]{.5f,1,2,3})if(GUILayout.Button("±"+a+"°"))amplitude=a;
            GUILayout.EndHorizontal();
            if(MaximumAmplitude>3) {
                GUILayout.BeginHorizontal();
                for(int a=4;a<=10;a++)if(GUILayout.Button("±"+a+"°"))amplitude=a;
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Precheck selected path")) {
                var model=PreviewGeometry.Parse(probe.Preview.geometry.text).Build();
                bool p=ValidatePath(model,probe.AllowanceUs,false,amplitude,MaximumAmplitude,out string pe);
                bool r=ValidatePath(model,probe.AllowanceUs,true,amplitude,MaximumAmplitude,out string re);
                Status="Software precheck: pitch "+(p ? "fits" : pe)+"; roll "+(r ? "fits" : re)+". Not physical validation.";
            }
            GUILayout.EndHorizontal(); GUILayout.Label("Selected amplitude: ±"+amplitude+"°");
            GUI.enabled=before && probe.Armed && !Running;
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Sweep pitch"))StartSweep(false,amplitude);
            if(GUILayout.Button("Sweep roll"))StartSweep(true,amplitude);
            GUILayout.EndHorizontal(); GUI.enabled=before;
            if(GUILayout.Button("STOP sweep + HOLD"))StopSweep();
        }
        private void OnDisable()
        {
            if(subscribed && probe && probe.source)probe.source.BeforeReset-=ResetRequested;
            subscribed=false;
            if(Running)StopSweep("Sweep component disabled");
        }
    }
}
