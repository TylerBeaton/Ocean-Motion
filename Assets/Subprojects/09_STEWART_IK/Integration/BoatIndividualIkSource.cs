using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Preview;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    /// <summary>Bounded boat pose drives one shared preview/physical IK result. No transport here.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class BoatIndividualIkSource : MonoBehaviour
    {
        public MotionPipelineController pipeline;
        public BoatMotionTelemetry telemetry;
        public WaveField waves;
        public StewartIkPreview preview;
        public IndividualIkSweepProbe probe;
        public bool paused;
        public MotionPoseCommand LatestCommand { get; private set; }
        public double PoseUpdatedAt { get; private set; }
        public float TranslationGain { get; private set; }=.002f;
        public float RotationGain { get; private set; }=.1f;
        public float PoseAngleAllowance { get; private set; }=5;
        public string SourceStatus { get; private set; }="Waiting for calibrated boat";
        public float MaximumRotationGain=>probe && probe.IsExtended ? 1f : .45f;
        private ulong sequence;
        private double sourceTime,freshAt;
        private bool observed;
        public bool SetPoseAllowance(int degrees)
        {
            if(degrees<1 || degrees>10 || !probe || !probe.IsExtended || !probe.CanConfigure)return false;
            PoseAngleAllowance=degrees;return true;
        }
        internal void ResetPoseAllowance() { PoseAngleAllowance=5; }
        public bool SetGains(float translation,float rotation)
        {
            if((probe && !probe.CanConfigure) || float.IsNaN(translation) || float.IsInfinity(translation) ||
                float.IsNaN(rotation) || float.IsInfinity(rotation) || translation<0 || translation>.02f || rotation<0 || rotation>MaximumRotationGain)
                return false;
            TranslationGain=translation; RotationGain=rotation; return true;
        }
        private void OnEnable()
        {
            observed=false;
            if(preview) {
                preview.PauseExternal=PauseAtNeutral; preview.ResumeExternal=Resume;
                preview.DrawExternalControls=DrawControls;
                preview.RejectExternalPose("Waiting for fresh calibrated boat motion");
            }
        }
        public void PauseAtNeutral()
        {
            paused=true; LatestCommand=default;
            if(probe)probe.Home();
            if(preview)preview.SetExternalPose(Vector3.zero,Quaternion.identity,"Paused at requested neutral; hardware returns gradually if enabled");
        }
        public void Resume() { paused=false; observed=false; }
        public void CalibrateBoat()
        {
            if(probe && !probe.CanConfigure) { SourceStatus="Calibration requires disarmed board neutral"; return; }
            if(telemetry)telemetry.CalibrateNeutral(); observed=false; LatestCommand=default;
        }
        private void Reject(string why)
        {
            SourceStatus=why; LatestCommand=default;
            if(preview)preview.RejectExternalPose(why);
        }
        private void LateUpdate()
        {
            if(paused) { LatestCommand=default; return; }
            if(!pipeline || !pipeline.isActiveAndEnabled || !telemetry || !telemetry.isActiveAndEnabled)
            { Reject("Boat source disabled/missing"); return; }
            var c=pipeline.LatestCommand; double now=Time.realtimeSinceStartupAsDouble;
            if(c.IsValid && (!observed || c.SourceSequence!=sequence)) {
                if(observed && (c.SourceSequence<sequence || c.SourceTimeSeconds<=sourceTime))
                { Reject("Source sequence/time reversed; manual recovery required"); return; }
                observed=true; sequence=c.SourceSequence; sourceTime=c.SourceTimeSeconds; freshAt=now;
            }
            if(!observed || now-freshAt>.25) { Reject("Boat source stale (>250ms)"); return; }
            if(!ProcessedBoatIkBridge.TryMap(c,TranslationGain,RotationGain,PoseAngleAllowance,out Vector3 position,out Quaternion rotation,out string error))
            { Reject(error); return; }
            preview.SetExternalPose(position,rotation,"Boat → shared individual IK • seq "+sequence+
                "\nHeave ×"+TranslationGain.ToString("F3")+" / rotation ×"+RotationGain.ToString("F2")+
                "\nMapped heave "+(position.y*1000).ToString("F2")+"mm / pitch "+(c.RotationDegrees.x*RotationGain).ToString("F2")+
                "° / roll "+(c.RotationDegrees.z*RotationGain).ToString("F2")+"°\nSP06 flags: "+c.LimitFlags);
            bool valid=preview.CurrentResult!=null && preview.CurrentResult.KinematicSolutionFound;
            LatestCommand=new MotionPoseCommand(position,c.RotationDegrees*RotationGain,c.SourceSequence,c.SourceTimeSeconds,valid,c.LimitFlags);
            PoseUpdatedAt=now; SourceStatus=valid ? "Fresh boat → six independent IK angles" : preview.CurrentError;
        }
        private void DrawControls()
        {
            GUILayout.Label(SourceStatus);
            bool before=GUI.enabled; GUI.enabled=before && (!probe || probe.CanConfigure);
            if(GUILayout.Button("Calibrate boat neutral (Calm, after settling)"))CalibrateBoat();
            GUILayout.Label("Shared pose allowance: ±"+PoseAngleAllowance.ToString("F0")+"° per axis / ±10mm");
            if(probe && probe.IsExtended) {
                GUILayout.BeginHorizontal();
                for(int degrees=1;degrees<=10;degrees++) {
                    if(degrees==6) { GUILayout.EndHorizontal();GUILayout.BeginHorizontal(); }
                    if(GUILayout.Button(degrees+"°"))SetPoseAllowance(degrees);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Heave gain: "+TranslationGain.ToString("F3"));
            float h=GUILayout.HorizontalSlider(TranslationGain,0,.02f);
            GUILayout.Label("Rotation gain: "+RotationGain.ToString("F2"));
            float r=GUILayout.HorizontalSlider(RotationGain,0,MaximumRotationGain);
            if(h!=TranslationGain || r!=RotationGain)SetGains(h,r);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("−0.05 rotation"))SetGains(TranslationGain,Mathf.Max(0,RotationGain-.05f));
            if(GUILayout.Button("+0.05 rotation"))SetGains(TranslationGain,Mathf.Min(MaximumRotationGain,RotationGain+.05f));
            GUILayout.EndHorizontal();
            if(probe && probe.IsExtended) {
                GUILayout.BeginHorizontal();
                foreach(float gain in new[]{.4f,.45f,.5f})
                    if(GUILayout.Button("Rotation "+gain.ToString("F2")))SetGains(TranslationGain,gain);
                GUILayout.EndHorizontal();
            }
            GUI.enabled=before;
            GUILayout.BeginHorizontal();
            foreach(WavePreset preset in System.Enum.GetValues(typeof(WavePreset)))
                if(GUILayout.Button(preset.ToString()) && waves)waves.SetPreset(preset);
            GUILayout.EndHorizontal();
            GUILayout.Label("Waves: "+(waves ? waves.ActivePresetName : "missing")+". Gains/calibration require disarmed board neutral.");
        }
        private void OnDisable()
        {
            LatestCommand=default; observed=false;
            if(preview) {
                preview.PauseExternal=null; preview.ResumeExternal=null; preview.DrawExternalControls=null;
                preview.RejectExternalPose("Boat IK source disabled");
            }
        }
    }
}
