using System;
using System.Globalization;
using OceanMotion.Subproject09.Preview;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    [DefaultExecutionOrder(500)]
    public sealed class IndividualIkSweepProbe : MonoBehaviour
    {
        public SerialController serial;
        public bool extendedTravel;
        private bool extendedMode;
        public bool IsExtended=>Application.isPlaying ? extendedMode : extendedTravel;
        public int MaximumAllowanceUs=>IsExtended ? 250 : 100;
        private string Protocol=>IsExtended ? "OI3" : "OI2";
        public ManualPlatformTarget source;
        public BoatIndividualIkSource boatSource;
        public StewartIkPreview Preview=>boatSource ? boatSource.preview : source ? source.preview : null;
        public string portName="/dev/cu.usbmodem-SET-ME";
        public string Status { get; private set; }="Disconnected; PWM starts OFF";
        public string LastStop { get; private set; }
        public bool Ready { get; private set; }
        public bool Armed { get; private set; }
        public int[] BoardOffsets { get; private set; }=new int[6];
        public int[] CandidateOffsets { get; private set; }
        public string MappingError { get; private set; }
        public int AllowanceUs { get; private set; }=25;
        public bool CanConfigure=>!Armed && !pendingArm && !pendingHome && !homing && Zero(BoardOffsets);
        public bool SetAllowance(int value) {
            if((IsExtended ? value<25 || value>250 || value%25!=0 : value!=25 && value!=50 && value!=100) || !CanConfigure)return false;
            AllowanceUs=value; return true;
        }
        private ApproximateIkPulseMap map;
        private bool connected,pendingArm,pendingHome,homing,haveAck;
        private uint token,seq,lastAck;
        private double deadline,nextSend,nextHello,ackAt,sourceSeenAt;
        private ulong sourceSeq;
        private bool sourceAdvanced;
        private Vector2 panelScroll;
        private double Now=>Time.realtimeSinceStartupAsDouble;
        private static bool Zero(int[] a)=>a!=null && Array.TrueForAll(a,x=>x==0);
        private static string Vector(int[] a)=>a==null ? "unavailable" : string.Join(",",a);
        private void Start()
        {
            extendedMode=extendedTravel;
            try { map=new ApproximateIkPulseMap(PreviewGeometry.Parse(Preview.geometry.text).Build()); }
            catch(Exception e) { Status="Mapping setup failed: "+e.Message; enabled=false; }
            if (serial) serial.SetTearDownFunction(()=>Send("HOLD"));
        }
        public void Connect()
        {
            if (!mapAvailable || string.IsNullOrWhiteSpace(portName) || portName.Contains("SET-ME")) { Status="Set Arduino USB port first"; return; }
            serial.portName=portName; serial.baudRate=115200; serial.messageListener=gameObject; serial.enabled=true;
            Status="Connecting; no PWM enabled";
        }
        private bool mapAvailable=>serial && (source || boatSource) && Preview && map!=null;
        public void OnConnectionEvent(bool value)
        {
            if(boatSource)boatSource.ResetPoseAllowance();
            AllowanceUs=25; connected=value; Ready=Armed=pendingArm=pendingHome=homing=haveAck=false;
            sourceSeq=0; sourceAdvanced=false; seq=lastAck=0;
            token=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0); if(token==0)token=1;
            nextHello=0; Status=value ? "Checking "+Protocol+" firmware" : "Disconnected; watchdog holds PWM";
        }
        private void Send(string kind,string suffix=null)
        {
            if (connected && serial && serial.isActiveAndEnabled)
                serial.SendLatestSerialMessage(Protocol+","+kind+","+token.ToString(CultureInfo.InvariantCulture)+(suffix==null ? "" : ","+suffix));
        }
        public void Hold(string reason="User HOLD")
        {
            Armed=pendingArm=pendingHome=homing=false; LastStop=reason;
            Send("HOLD"); Status="HOLD: "+reason;
        }
        public void Home()
        {
            if (!Ready) return;
            Armed=pendingArm=homing=false; pendingHome=true; deadline=Now+.25;
            Send("HOME"); Status="Gradual neutral requested";
        }
        public void Off()
        {
            Armed=pendingArm=pendingHome=homing=false; Send("OFF"); Status="PWM OFF requested; support plate";
        }
        public void Disconnect()
        {
            Hold("Serial disconnected"); if(serial)serial.enabled=false; OnConnectionEvent(false);
        }
        public bool FreshCandidate()
        {
            CandidateOffsets=null;
            if ((boatSource ? !boatSource.isActiveAndEnabled || boatSource.paused : !source || !source.isActiveAndEnabled) || !Preview || !Preview.isActiveAndEnabled || map==null)
            { MappingError="Pose source/preview unavailable"; return false; }
            var c=boatSource ? boatSource.LatestCommand : source.LatestCommand;
            double updatedAt=boatSource ? boatSource.PoseUpdatedAt : source.PoseUpdatedAt;
            if(c.SourceSequence!=sourceSeq) {
                if(sourceSeq!=0 && c.SourceSequence>sourceSeq)sourceAdvanced=true;
                sourceSeq=c.SourceSequence; sourceSeenAt=Now;
            }
            if(!sourceAdvanced || Now-sourceSeenAt>.25 || Now-updatedAt>.25 || !c.IsValid)
            { MappingError="Waiting/stale/invalid pose source: seq="+sourceSeq+", advanced="+sourceAdvanced+", valid="+c.IsValid+", age="+(Now-updatedAt).ToString("F3")+"s"+(boatSource ? "; "+boatSource.SourceStatus : ""); return false; }
            if(!map.TryOffsets(Preview.CurrentResult,AllowanceUs,out int[] offsets,out string error))
            { MappingError=error; return false; }
            CandidateOffsets=offsets; MappingError=null; return true;
        }
        public void Arm()
        {
            if(!Ready || Armed || pendingArm || pendingHome || homing || !Zero(BoardOffsets) || !FreshCandidate() || !Zero(CandidateOffsets))
            { Status="Arm requires fresh source neutral AND board neutral; Calm/calibrate or Reset cube, then Gradual neutral"; return; }
            // Require the actual target pose neutral, not just offsets rounded to zero.
            if(Preview.translationMm.sqrMagnitude>1e-6f || Quaternion.Angle(Preview.target.localRotation,PreviewGeometry.Parse(Preview.geometry.text).NeutralUnityRotation)>.001f)
            { Status="Return source to exact neutral before Arm"; return; }
            pendingArm=true; deadline=Now+.25; Send("ARM",AllowanceUs.ToString(CultureInfo.InvariantCulture)); Status="Waiting for explicit ARM acknowledgement";
        }
        private void LateUpdate()
        {
            bool valid=FreshCandidate();
            if(Armed && !valid) { Hold(MappingError); return; }
            if(!connected || !serial || !serial.isActiveAndEnabled)return;
            if(!Ready) { if(Now>=nextHello) { Send("HELLO"); nextHello=Now+1; } return; }
            if(pendingArm || pendingHome) { if(Now>deadline)Hold("Board acknowledgement timeout"); return; }
            if(homing) {
                if(Now>deadline) { Hold("Neutral return timeout"); return; }
                if(Now>=nextSend) { Send("PING"); nextSend=Now+.05; } return;
            }
            if(!Armed)return;
            if(!valid) { Hold(MappingError); return; }
            if(Now-ackAt>.25) { Hold("ACK timeout"); return; }
            if(Now>=nextSend) {
                if(seq==uint.MaxValue) { Hold("Sequence exhausted"); return; }
                Send("POSE",(++seq).ToString(CultureInfo.InvariantCulture)+","+Vector(CandidateOffsets)); nextSend=Now+.05;
            }
        }
        private bool ReadSix(string[] f,int at,out int[] values)
        {
            values=new int[6];
            if(f.Length!=at+6)return false;
            for(int i=0;i<6;i++) if(!int.TryParse(f[at+i],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out values[i]) || values[i]<-MaximumAllowanceUs || values[i]>MaximumAllowanceUs)return false;
            return true;
        }
        public void OnMessageArrived(string message)
        {
            if(string.IsNullOrEmpty(message))return;
            var f=message.Trim().Split(',');
            if(f.Length<3 || f[0]!=Protocol || !uint.TryParse(f[2],out uint id) || id!=token)return;
            if(f[1]=="READY" && !Ready && !Armed && !pendingArm && !pendingHome && !homing && ReadSix(f,3,out int[] board)) {
                AllowanceUs=25; BoardOffsets=board; Ready=true; Status=Zero(board) ? "Firmware READY; explicit Arm required" : "Firmware READY; retained offsets need Gradual neutral";
            }
            else if(f[1]=="ARMED" && f.Length==4 && pendingArm && int.TryParse(f[3],out int echo) && echo==AllowanceUs) {
                pendingArm=false; Armed=true; seq=lastAck=0; haveAck=false; ackAt=Now; nextSend=0; LastStop=null; Status="ARMED • six independent provisional IK commands";
            }
            else if(f[1]=="ACK" && Armed && f.Length==10 && uint.TryParse(f[3],out uint ack) && ack>0 && ack<=seq && (!haveAck || ack>lastAck) && ReadSix(f,4,out int[] actual)) {
                BoardOffsets=actual; if(Array.Exists(actual,x=>Math.Abs(x)>AllowanceUs)) { Hold("Board exceeded selected allowance"); return; } lastAck=ack; haveAck=true; ackAt=Now;
            }
            else if(f[1]=="HOMING" && f.Length==3 && pendingHome) { pendingHome=false; homing=true; deadline=Now+(IsExtended ? 7 : 4); nextSend=0; Status="Ramping to neutral"; }
            else if(f[1]=="NEUTRAL" && f.Length==3 && homing) { homing=false; BoardOffsets=new int[6]; Status="Board commanded neutral; observe before Arm"; }
            else if((f[1]=="ERR" || f[1]=="WATCHDOG" || f[1]=="HOLD" || f[1]=="OFF")) {
                Armed=pendingArm=pendingHome=homing=false;
                if(f[1]=="OFF")BoardOffsets=new int[6];
                if(f[1]=="ERR") { Ready=false; nextHello=Now+1; }
                if(f[1]=="ERR" || f[1]=="WATCHDOG")LastStop=message;
                Status=message+"; manual rearm only";
            }
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width-395,12,383,Mathf.Min(Screen.height-24,900)),GUI.skin.box);
            GUILayout.Label(boatSource ? "BOAT → INDIVIDUAL IK" : "INDIVIDUAL IK • STATIC SWEEP");
            panelScroll=GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("Offset −5mm estimate; 11us/degree estimate. Selected ±"+AllowanceUs+"us from saved neutrals; ramp1us/20ms.");
            GUILayout.Label(Status); if(!string.IsNullOrEmpty(LastStop))GUILayout.Label("Last stop: "+LastStop);
            GUILayout.Label("Candidate offsets 0..5 (us): "+Vector(CandidateOffsets));
            GUILayout.Label("Board offsets 0..5 (us): "+Vector(BoardOffsets));
            if(MappingError!=null)GUILayout.Label(MappingError);
            GUI.enabled=!connected; portName=GUILayout.TextField(portName);
            if(GUILayout.Button("Connect USB — no PWM"))Connect(); GUI.enabled=true;
            if(GUILayout.Button("Disconnect link"))Disconnect();
            GUI.enabled=Ready && !Armed && !pendingArm && !pendingHome && !homing;
            if(GUILayout.Button(boatSource ? "ARM at Calm + board neutral" : "ARM at cube + board neutral"))Arm(); GUI.enabled=true;
            GUI.enabled=CanConfigure;
            GUILayout.BeginHorizontal();
            int[] allowances=IsExtended ? new[]{25,50,75,100,125,150,175,200,225,250} : new[]{25,50,100};
            for(int i=0;i<allowances.Length;i++) {
                if(i>0 && i%5==0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if(GUILayout.Button("±"+allowances[i]+"us"))SetAllowance(allowances[i]);
            }
            GUILayout.EndHorizontal(); GUI.enabled=true;
            var sweep=GetComponent<IkAxisSweep>(); if(sweep)sweep.DrawControls();
            GUILayout.EndScrollView();
            if(GUILayout.Button("HOLD current PWM"))Hold();
            if(GUILayout.Button("Gradual neutral"))Home();
            if(GUILayout.Button("PWM OFF — support plate"))Off();
            GUILayout.Label("IK with estimated geometry/calibration; commanded offsets are not measured pose feedback.");
            GUILayout.EndArea();
        }
        private void OnDisable() { Hold("Component disabled / Play stopped"); Ready=false; }
    }
}
