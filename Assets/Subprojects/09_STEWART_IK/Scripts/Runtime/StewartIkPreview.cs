using System;
using OceanMotion.Subproject09.Core;
using UnityEngine;

namespace OceanMotion.Subproject09.Preview
{
    /// <summary>Visual experiment only. No actuator transport or PWM conversion exists here.</summary>
    public sealed class StewartIkPreview : MonoBehaviour
    {
        public TextAsset geometry;
        public Transform target;
        public Camera viewCamera;
        public bool useSceneTarget;
        public string sourceDescription="Source boat (left) → visual IK (right)";
        public string outputDescription="Visual IK preview • hardware output OFF";
        public string resumeLabel="Resume boat";
        public Vector3 translationMm;
        public Vector3 rotationDegrees;
        public IkResult CurrentResult { get; private set; }
        public string CurrentError { get; private set; }
        public bool ExternalControl { get; private set; }
        public string ExternalStatus { get; private set; }
        public Action PauseExternal, ResumeExternal, DrawExternalControls;
        public bool ShowExternalTransportControls { get; set; }=true;
        public bool HasPresentationOverlay { get; set; }
        private bool externalValid;
        private PreviewGeometry data;
        private RotaryStewartModel model;
        private GameObject visualRoot;
        private Material lineMaterial;
        private LineRenderer[] horns, rods;
        private LineRenderer plate;
        private Transform[] balls;
        private double[] previous;
        private bool unreachableDemo;
        private static readonly Color[] Colours={new Color(.22f,.8f,1),new Color(1,.65f,.25f),
            new Color(.55f,.9f,.35f),new Color(1,.4f,.55f),new Color(.7f,.55f,1),new Color(1,.9f,.3f)};

        private void OnEnable()
        {
            try
            {
                if (geometry==null || target==null) throw new IkGeometryException("Assign geometry and target.");
                data=PreviewGeometry.Parse(geometry.text); model=data.Build();
                BuildVisuals(); ResetNeutral(); EvaluateNow();
            }
            catch (Exception ex) { CurrentError=ex.Message; enabled=false; }
        }
        private void Update() { EvaluateNow(); }
        public void ResetNeutral()
        {
            translationMm=rotationDegrees=Vector3.zero;
            unreachableDemo=false; useSceneTarget=false; previous=null;
            if (target && data!=null)
            {
                target.localPosition=PreviewGeometry.ToUnity(model.NeutralPositionMm);
                target.localRotation=data.NeutralUnityRotation;
            }
        }
        public void SetPose(Vector3 translation,Vector3 rotation)
        {
            translationMm=translation; rotationDegrees=rotation;
            unreachableDemo=false; useSceneTarget=false;
        }
        public void ShowUnreachable() { SetPose(new Vector3(0,1000,0),Vector3.zero); unreachableDemo=true; }

        // General pose boundary: no reference to boat physics, processing, or transport.
        public void SetExternalPose(Vector3 positionMeters,Quaternion rotation,string status)
        {
            ExternalControl=true; externalValid=true; ExternalStatus=status;
            translationMm=positionMeters*1000; useSceneTarget=true;
            if (data==null || !target) return;
            target.localPosition=PreviewGeometry.ToUnity(model.NeutralPositionMm)+data.NeutralUnityRotation*positionMeters;
            target.localRotation=data.NeutralUnityRotation*rotation;
            EvaluateNow();
        }
        public void RejectExternalPose(string status)
        {
            ExternalControl=true; externalValid=false; ExternalStatus=status;
            CurrentResult=null; CurrentError=status; previous=null;
            SetSolvedVisible(false); if (plate) plate.enabled=false;
        }

        public void EvaluateNow()
        {
            if (model==null || !target) return;
            if (ExternalControl && !externalValid) return;
            try
            {
                Quaternion neutral=data.NeutralUnityRotation;
                Vector3 centre=PreviewGeometry.ToUnity(model.NeutralPositionMm);
                if (!useSceneTarget)
                {
                    target.localPosition=centre+neutral*(translationMm*.001f);
                    target.localRotation=neutral*Quaternion.Euler(rotationDegrees);
                }
                // Read the neutral-relative quaternion directly. Never read wrapped Euler angles.
                Vector3 offset=Quaternion.Inverse(neutral)*(target.localPosition-centre);
                Quaternion q=Quaternion.Inverse(neutral)*target.localRotation;
                var pose=IkPose.FromUnity(new IkVector(offset.x,offset.y,offset.z),q.x,q.y,q.z,q.w);
                CurrentResult=model.Solve(pose,previous);
                CurrentError=CurrentResult.KinematicSolutionFound ? null : string.Join("\n",CurrentResult.Errors);
                if (CurrentResult.KinematicSolutionFound) previous=CurrentResult.AnglesDegrees;
                UpdateVisuals(pose);
            }
            catch (Exception ex)
            {
                CurrentResult=null; CurrentError=ex.Message;
                SetSolvedVisible(false); if (plate) plate.enabled=false;
            }
        }
        private void BuildVisuals()
        {
            visualRoot=new GameObject("Generated preview geometry"); visualRoot.transform.SetParent(transform,false);
            Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("URP Unlit shader unavailable.");
            lineMaterial=new Material(shader) { name="SP09 preview lines" };
            horns=new LineRenderer[6]; rods=new LineRenderer[6]; balls=new Transform[12];
            for (int i=0;i<6;i++)
            {
                var leg=model.Legs[i];
                var axis=Line("Shaft axis "+i,new Color(.55f,.6f,.65f),.0018f,2);
                axis.SetPosition(0,PreviewGeometry.ToUnity(leg.ShaftPointMm-leg.ShaftAxis*8));
                axis.SetPosition(1,PreviewGeometry.ToUnity(leg.ShaftPointMm+leg.ShaftAxis*16));
                horns[i]=Line("Horn "+i,Colours[i],.004f,3);
                rods[i]=Line("Rod "+i,Colours[i],.0025f,2);
                balls[i]=Marker("Horn joint "+i,Colours[i]);
                balls[i+6]=Marker("Top joint "+i,Colours[i]);
                var label=new GameObject("Channel "+i); label.transform.SetParent(visualRoot.transform,false);
                label.transform.localPosition=PreviewGeometry.ToUnity(leg.ShaftPointMm)+Vector3.up*.012f;
                var text=label.AddComponent<TextMesh>(); text.text=i.ToString(); text.fontSize=48;
                text.characterSize=.003f; text.anchor=TextAnchor.MiddleCenter; text.color=Colours[i];
                Camera viewer=viewCamera ? viewCamera : Camera.main;
                if (viewer) label.transform.rotation=viewer.transform.rotation;
            }
            plate=Line("Desired top outline",Color.white,.002f,7);
            var baseLine=Line("Base outline",new Color(.25f,.35f,.42f),.0015f,7);
            for (int i=0;i<=6;i++) baseLine.SetPosition(i,PreviewGeometry.ToUnity(model.Legs[i%6].ShaftPointMm));
            foreach (Transform child in visualRoot.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer=gameObject.layer;
        }
        private LineRenderer Line(string name,Color colour,float width,int count)
        {
            var go=new GameObject(name); go.transform.SetParent(visualRoot.transform,false);
            var line=go.AddComponent<LineRenderer>(); line.useWorldSpace=false;
            line.sharedMaterial=lineMaterial; line.startColor=line.endColor=colour;
            Tint(line,colour);
            line.startWidth=line.endWidth=width; line.positionCount=count;
            line.numCapVertices=4; return line;
        }
        private Transform Marker(string name,Color colour)
        {
            // Small crossed lines keep the preview free of colliders and lighting dependencies.
            var marker=new GameObject(name); marker.transform.SetParent(visualRoot.transform,false);
            foreach (Vector3 axis in new[] {Vector3.right,Vector3.up,Vector3.forward})
            {
                var line=Line(name+" cross",colour,.0035f,2);
                line.transform.SetParent(marker.transform,false);
                line.SetPosition(0,-axis*.003f); line.SetPosition(1,axis*.003f);
            }
            return marker.transform;
        }
        private void SetSolvedVisible(bool visible)
        {
            if (horns==null) return;
            for (int i=0;i<6;i++)
            {
                horns[i].enabled=rods[i].enabled=visible;
                balls[i].gameObject.SetActive(visible); balls[i+6].gameObject.SetActive(visible);
            }
        }
        private void UpdateVisuals(IkPose pose)
        {
            bool valid=CurrentResult.KinematicSolutionFound; SetSolvedVisible(valid);
            plate.enabled=true; plate.startColor=plate.endColor=valid ? Color.white : Color.red;
            Tint(plate,valid ? Color.white : Color.red);
            for (int i=0;i<=6;i++) plate.SetPosition(i,PreviewGeometry.ToUnity(model.TopPoint(model.Legs[i%6],pose)));
            if (!valid) return;
            for (int i=0;i<6;i++)
            {
                var row=CurrentResult.Legs[i]; var leg=model.Legs[i];
                horns[i].SetPosition(0,PreviewGeometry.ToUnity(leg.ShaftPointMm));
                horns[i].SetPosition(1,PreviewGeometry.ToUnity(leg.CircleCentreMm));
                horns[i].SetPosition(2,PreviewGeometry.ToUnity(row.Horn));
                rods[i].SetPosition(0,PreviewGeometry.ToUnity(row.Horn));
                rods[i].SetPosition(1,PreviewGeometry.ToUnity(row.Top));
                balls[i].localPosition=PreviewGeometry.ToUnity(row.Horn);
                balls[i+6].localPosition=PreviewGeometry.ToUnity(row.Top);
            }
        }
        private void OnGUI()
        {
            if (HasPresentationOverlay) return;
            GUI.skin.label.wordWrap=true;
            GUILayout.BeginArea(new Rect(12,12,340,Screen.height-24),GUI.skin.box);
            GUILayout.Label("OCEAN MOTION / SP09",new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold});
            GUILayout.Label(outputDescription);
            GUILayout.Label("Provisional geometry. Horn offsets and pulse/angle calibration remain unverified.");
            GUILayout.Space(8);
            if (ExternalControl)
            {
                GUILayout.Label(ExternalStatus ?? "Waiting for processed motion");
                if(ShowExternalTransportControls) {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Pause / Neutral")) PauseExternal?.Invoke();
                    if (GUILayout.Button(resumeLabel)) ResumeExternal?.Invoke();
                    GUILayout.EndHorizontal();
                }
                DrawExternalControls?.Invoke();
                GUILayout.Label(sourceDescription);
                GUILayout.Label("Target offset mm: "+translationMm.ToString("F2"));
            }
            else
            {
                if (GUILayout.Button("Neutral")) ResetNeutral();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Heave +1 mm")) SetPose(new Vector3(0,1,0),Vector3.zero);
                if (GUILayout.Button("Pitch +1°")) SetPose(Vector3.zero,new Vector3(1,0,0));
                GUILayout.EndHorizontal();
                useSceneTarget=GUILayout.Toggle(useSceneTarget,"Use Target transform (Scene move/rotate handles)");
                bool controls=GUI.enabled; GUI.enabled=!useSceneTarget && !unreachableDemo;
                translationMm.x=Slider("Sway X",translationMm.x,10,"mm");
                translationMm.y=Slider("Heave Y",translationMm.y,10,"mm");
                translationMm.z=Slider("Surge Z",translationMm.z,10,"mm");
                rotationDegrees.x=Slider("Pitch X",rotationDegrees.x,5,"°");
                rotationDegrees.y=Slider("Yaw Y",rotationDegrees.y,5,"°");
                rotationDegrees.z=Slider("Roll Z",rotationDegrees.z,5,"°");
                GUI.enabled=controls;
                GUILayout.Space(6);
                if (GUILayout.Button("Test rejection: +1 m heave")) ShowUnreachable();
            }
            GUILayout.Space(8);
            bool valid=CurrentResult!=null && CurrentResult.KinematicSolutionFound;
            GUILayout.Label(valid ? "SOLUTION FOUND (mathematical only)" : "POSE REJECTED",new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,normal={textColor=valid ? Color.green : Color.red}});
            if (valid)
            {
                GUILayout.Label("Channel    angle / reference     Δ neutral");
                foreach (var row in CurrentResult.Legs)
                    GUILayout.Label(string.Format("    {0}           {1,8:0.000}°             {2,8:0.000}°",row.Channel,row.AngleDegrees,row.NeutralDeltaDegrees));
            }
            else GUILayout.Label(CurrentError ?? "Waiting for geometry");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Angles are about P→Q shaft axes. They are not PWM commands or verified travel limits.");
            GUILayout.EndArea();
        }
        private static float Slider(string name,float value,float extent,string units)
        {
            GUILayout.Label(name+": "+value.ToString("0.00")+" "+units);
            // Do not clamp an out-of-range target merely because the panel was drawn.
            float shown=Mathf.Clamp(value,-extent,extent);
            float changed=GUILayout.HorizontalSlider(shown,-extent,extent);
            return changed==shown ? value : changed;
        }
        private static void Tint(LineRenderer line,Color colour)
        {
            var properties=new MaterialPropertyBlock(); properties.SetColor("_BaseColor",colour);
            line.SetPropertyBlock(properties);
        }
        private void OnDisable()
        {
            if (visualRoot) Destroy(visualRoot);
            if (lineMaterial) Destroy(lineMaterial);
        }
    }
}
