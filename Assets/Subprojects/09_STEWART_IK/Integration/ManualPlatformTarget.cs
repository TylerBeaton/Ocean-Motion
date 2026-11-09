using OceanMotion.Subproject06;
using OceanMotion.Subproject09.Preview;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    /// <summary>Direct neutral-relative debug target. Physical output remains the separate empirical mixer.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ManualPlatformTarget : MonoBehaviour
    {
        public Transform cube;
        public StewartIkPreview preview;
        public bool useTransformHandles;
        public bool individualIkTest;
        public bool enforceSmallPoseEnvelope;
        public float smallAngleExtent=2, smallHeaveExtent=5, smallStep=.1f;
        [System.NonSerialized] public bool automatedControl;
        public System.Action BeforeReset;
        public double PoseUpdatedAt { get; private set; }
        public Vector3 offsetMm, anglesDegrees;
        public MotionPoseCommand LatestCommand { get; private set; }
        private ulong sequence;
        private Vector3 origin;
        private Quaternion neutral;

        private void Start()
        {
            origin=cube.localPosition; neutral=cube.localRotation;
            preview.PauseExternal=ResetNeutral;
            preview.ResumeExternal=()=>{};
            preview.DrawExternalControls=DrawControls;
        }
        public void ResetNeutral()
        {
            BeforeReset?.Invoke();
            offsetMm=anglesDegrees=Vector3.zero;
            cube.localPosition=origin; cube.localRotation=neutral;
        }
        public static Vector3 SignedAngles(Quaternion relative)
        {
            Vector3 a=relative.eulerAngles;
            return new Vector3(Mathf.DeltaAngle(0,a.x),Mathf.DeltaAngle(0,a.y),Mathf.DeltaAngle(0,a.z));
        }
        private void Update()
        {
            if (!cube || !preview) return;
            if (!useTransformHandles)
            {
                cube.localPosition=origin+offsetMm*.001f;
                cube.localRotation=neutral*Quaternion.Euler(anglesDegrees);
            }
            Vector3 offset=cube.localPosition-origin;
            Quaternion relative=Quaternion.Inverse(neutral)*cube.localRotation;
            Vector3 signed=SignedAngles(relative);
            // Visual IK receives the quaternion directly, with no boat gains or wave processing.
            preview.SetExternalPose(offset,relative,"Manual cube • direct pose, gain 1\nX pitch / Y yaw / Z roll\nRelative angles: "+signed.ToString("F2")+"°");
            PoseUpdatedAt=Time.realtimeSinceStartupAsDouble;
            bool solved=preview.CurrentResult!=null && preview.CurrentResult.KinematicSolutionFound;
            bool within=!enforceSmallPoseEnvelope ||
                (Mathf.Max(Mathf.Abs(signed.x),Mathf.Abs(signed.y),Mathf.Abs(signed.z))<=Mathf.Clamp(smallAngleExtent,0,10)+.0001f &&
                Mathf.Max(Mathf.Abs(offset.x),Mathf.Abs(offset.y),Mathf.Abs(offset.z))*1000<=Mathf.Clamp(smallHeaveExtent,0,10)+.0001f);
            if(!within)preview.RejectExternalPose("Manual pose allowance exceeded (maximum ±10° / ±10mm); whole pose rejected");
            // The existing physical mixer has no yaw, sway or surge mapping. Reject those inputs,
            // rather than silently pretending the hardware follows the visual solution.
            bool supported=individualIkTest || (Mathf.Abs(offset.x)<.000001f && Mathf.Abs(offset.z)<.000001f && Mathf.Abs(signed.y)<.001f);
            LatestCommand=new MotionPoseCommand(offset,signed,++sequence,Time.realtimeSinceStartupAsDouble,solved && supported && within);
        }
        private void DrawControls()
        {
            GUILayout.Label("CUBE DEBUG • no water / no boat gains");
            bool outer=GUI.enabled; GUI.enabled=outer && !automatedControl;
            bool handles=GUILayout.Toggle(useTransformHandles,"Use cube Transform / Scene handles");
            if (useTransformHandles && !handles)
            {
                offsetMm=(cube.localPosition-origin)*1000;
                anglesDegrees=SignedAngles(Quaternion.Inverse(neutral)*cube.localRotation);
            }
            useTransformHandles=handles;
            bool wasEnabled=GUI.enabled; GUI.enabled=wasEnabled && !handles;
            anglesDegrees.x=Slider("Pitch X",anglesDegrees.x,individualIkTest ? smallAngleExtent : 180,"°");
            anglesDegrees.y=Slider("Yaw Y",anglesDegrees.y,individualIkTest ? smallAngleExtent : 180,"°");
            anglesDegrees.z=Slider("Roll Z",anglesDegrees.z,individualIkTest ? smallAngleExtent : 180,"°");
            offsetMm.y=Slider("Heave Y",offsetMm.y,individualIkTest ? smallHeaveExtent : 50,"mm");
            GUI.enabled=wasEnabled;
            GUI.enabled=outer;
            if (GUILayout.Button("Reset cube to neutral")) ResetNeutral();
            GUILayout.Label(individualIkTest ? "Individual IK → provisional pulses. Per-servo allowance is shown in the USB panel; oversized poses HOLD. Geometry/angle scale are estimates. Hardware starts OFF." : "USB panel sends pair patterns, not solved IK angles. Yaw / XZ translation / rejected IK poses cannot drive hardware. Hardware starts OFF.");
        }
        private float Slider(string label,float value,float extent,string units)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label+": "+value.ToString("F1")+units);
            float step=individualIkTest ? smallStep : 1;
            if (GUILayout.Button("−",GUILayout.Width(25))) value-=step;
            if (GUILayout.Button("+",GUILayout.Width(25))) value+=step;
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value,-extent,extent);
        }
    }
}
