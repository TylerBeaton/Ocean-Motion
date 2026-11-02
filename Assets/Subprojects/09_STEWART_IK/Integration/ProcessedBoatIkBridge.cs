using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Preview;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    /// <summary>Consumes SP06's processed pose. This component has no physical output.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class ProcessedBoatIkBridge : MonoBehaviour
    {
        public MotionPipelineController pipeline;
        public StewartIkPreview preview;
        public BoatMotionTelemetry telemetry;
        public WaveField waves;
        public const float MaximumTranslationGain=.1f;
        public const float MaximumRotationGain=1f;
        [Range(0,MaximumTranslationGain)] public float translationGain=.02f;
        [Range(0,MaximumRotationGain)] public float rotationGain=.45f;
        public bool paused;
        public int AcceptedFrames { get; private set; }
        public float MaximumObservedHeaveMm { get; private set; }
        public float MaximumObservedRotationDegrees { get; private set; }
        private ulong lastSequence;
        private double lastTime, freshAt;
        private bool observed;

        private void OnEnable()
        {
            observed=false;
            if (preview)
            {
                preview.PauseExternal=PauseAtNeutral; preview.ResumeExternal=Resume;
                preview.DrawExternalControls=DrawControls;
                preview.RejectExternalPose("Waiting for fresh processed boat motion");
            }
        }
        public void PauseAtNeutral()
        {
            paused=true;
            if (preview) preview.SetExternalPose(Vector3.zero,Quaternion.identity,"Paused at visual neutral. Boat simulation continues.");
        }
        public void Resume() { paused=false; observed=false; }
        public void CalibrateBoat()
        {
            if (telemetry) telemetry.CalibrateNeutral();
            observed=false;
        }
        private void DrawControls()
        {
            if (GUILayout.Button("Calibrate boat neutral (after settling)")) CalibrateBoat();
            GUILayout.BeginHorizontal();
            foreach (WavePreset preset in System.Enum.GetValues(typeof(WavePreset)))
                if (GUILayout.Button(preset.ToString()) && waves) waves.SetPreset(preset);
            GUILayout.EndHorizontal();
            GUILayout.Label("Waves: "+(waves ? waves.ActivePresetName : "missing"));
            GUILayout.Label("Rotation gain: "+rotationGain.ToString("0.00")+"× (1.00 = processed angle)");
            rotationGain=GUILayout.HorizontalSlider(rotationGain,0,MaximumRotationGain);
        }
        private void LateUpdate()
        {
            if (!preview) return;
            if (paused) { PauseAtNeutral(); return; }
            if (!pipeline || !pipeline.isActiveAndEnabled)
            { preview.RejectExternalPose("Processed source disabled or missing"); return; }
            var command=pipeline.LatestCommand;
            double now=Time.realtimeSinceStartupAsDouble;
            if (command.IsValid && (!observed || command.SourceSequence!=lastSequence))
            {
                if (observed && (command.SourceSequence<lastSequence || command.SourceTimeSeconds<=lastTime))
                { preview.RejectExternalPose("Source sequence/time went backwards; pause and resume to restart"); return; }
                observed=true; lastSequence=command.SourceSequence;
                lastTime=command.SourceTimeSeconds; freshAt=now;
            }
            if (!observed || now-freshAt>.25)
            { preview.RejectExternalPose("Processed source stale (>250 ms); solved linkages hidden"); return; }
            if (!TryMap(command,translationGain,rotationGain,out Vector3 position,out Quaternion rotation,out string error))
            { preview.RejectExternalPose(error); return; }
            preview.SetExternalPose(position,rotation,"Processed boat • seq "+command.SourceSequence+
                "\nHeave ×"+translationGain.ToString("0.00")+" / angles ×"+rotationGain.ToString("0.00")+
                "\nProcessed H "+(command.TranslationMeters.y*1000).ToString("F1")+" mm / P "+command.RotationDegrees.x.ToString("F1")+"° / R "+command.RotationDegrees.z.ToString("F1")+"°"+
                "\nVisual P "+(command.RotationDegrees.x*rotationGain).ToString("F2")+"° / R "+(command.RotationDegrees.z*rotationGain).ToString("F2")+"°"+
                "\nSP06 flags: "+command.LimitFlags);
            if (preview.CurrentResult!=null && preview.CurrentResult.KinematicSolutionFound) AcceptedFrames++;
            MaximumObservedHeaveMm=Mathf.Max(MaximumObservedHeaveMm,Mathf.Abs(position.y*1000));
            Vector3 angles=command.RotationDegrees*rotationGain;
            MaximumObservedRotationDegrees=Mathf.Max(MaximumObservedRotationDegrees,Mathf.Abs(angles.x),Mathf.Abs(angles.y),Mathf.Abs(angles.z));
        }
        public static bool TryMap(MotionPoseCommand command,float translationScale,float rotationScale,
            out Vector3 position,out Quaternion rotation,out string error)
        {
            position=Vector3.zero; rotation=Quaternion.identity; error=null;
            if (!command.IsValid || command.SourceSequence==0 || !Finite(command.SourceTimeSeconds))
                error="Invalid processed source: "+command.LimitFlags;
            else if (!Finite(translationScale) || !Finite(rotationScale) || translationScale<0f || translationScale>MaximumTranslationGain || rotationScale<0f || rotationScale>MaximumRotationGain)
                error="Invalid visual gains (translation 0..0.1; rotation 0..1.0)";
            else if (!Finite(command.TranslationMeters) || !Finite(command.RotationDegrees))
                error="Nonfinite processed pose";
            if (error!=null) return false;
            position=command.TranslationMeters*translationScale;
            Vector3 angles=command.RotationDegrees*rotationScale;
            if (Mathf.Max(Mathf.Abs(position.x),Mathf.Abs(position.y),Mathf.Abs(position.z))>.010001f ||
                Mathf.Max(Mathf.Abs(angles.x),Mathf.Abs(angles.y),Mathf.Abs(angles.z))>5.0001f)
            { error="Outside visual test envelope (±10 mm / ±5°); entire pose rejected"; return false; }
            // SP06 supplies signed, neutral-relative X pitch / Y yaw / Z roll degrees.
            // Quaternion.Euler matches SP06's existing processed-pose display contract.
            rotation=Quaternion.Euler(angles); return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private void OnDisable()
        {
            observed=false;
            if (preview)
            {
                preview.PauseExternal=null; preview.ResumeExternal=null;
                preview.DrawExternalControls=null;
                preview.RejectExternalPose("Boat bridge disabled");
            }
        }
    }
}
