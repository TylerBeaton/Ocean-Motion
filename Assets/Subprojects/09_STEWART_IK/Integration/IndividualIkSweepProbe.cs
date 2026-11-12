using System;
using System.Globalization;
using OceanMotion.Subproject05;
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
        public int MaximumAllowanceUs=>IsExtended ? 500 : 100;
        private string Protocol=>IsExtended ? "OI5" : "OI2";
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
            if((IsExtended ? value<25 || value>500 || value%25!=0 : value!=25 && value!=50 && value!=100) || !CanConfigure)return false;
            AllowanceUs=value; return true;
        }
        private ApproximateIkPulseMap map;
        private bool connected,pendingArm,pendingHome,homing,haveAck;
        private uint token,seq,lastAck;
        private double deadline,nextSend,nextHello,ackAt,sourceSeenAt;
        private ulong sourceSeq;
        private bool sourceAdvanced;
        public bool Preparing { get; private set; }
        public int NextPulseAllowanceUs { get; private set; }=500;
        public int NextPoseAllowanceDegrees { get; private set; }=10;
        public float NextRotationGain { get; private set; }=.9f;
        public float NextHeaveGain { get; private set; }=.0125f;
        public int MovementSpeedUsPerSecond { get; private set; }=50;
        public int NextMovementSpeedUsPerSecond { get; private set; }=500;
        public int NextWaveTransitionSeconds { get; private set; }=2;
        public int WaveTransitionSeconds { get; private set; }=2;
        public float NextWaterSpeedMultiplier { get; private set; }=2;
        public float WaterSpeedMultiplier=>boatSource && boatSource.waves ? boatSource.waves.SpeedMultiplier : 1;
        public bool SetNextWaterSpeed(float value)
        {
            if(Preparing || !WaveField.IsSupportedSpeedMultiplier(value))return false;
            NextWaterSpeedMultiplier=value;return true;
        }
        public bool SetNextWaveTransition(int seconds)
        {
            if(Preparing || seconds<1 || seconds>3)return false;
            NextWaveTransitionSeconds=seconds;return true;
        }
        private string durationText="60";
        public bool DurationInputValid { get; private set; }=true;
        public double TestDurationSeconds { get; private set; }=60;
        public double RunDurationSeconds { get; private set; }
        public bool MotionStarted { get; private set; }
        private double motionStartedAt,motionStoppedAt=-1;
        public bool TimerRunning=>MotionStarted && motionStoppedAt<0 && Armed && RunDurationSeconds>0;
        private static double TimerNow=>(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;
        public double MotionElapsedSeconds=>MotionStarted ? Math.Max(0,(motionStoppedAt<0 ? TimerNow : motionStoppedAt)-motionStartedAt) : 0;
        public bool SetTestDuration(string text)
        {
            if(Preparing || Armed || pendingArm || pendingHome || homing)return false;
            durationText=text;
            DurationInputValid=double.TryParse(text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,out double seconds) && !double.IsNaN(seconds) && !double.IsInfinity(seconds) &&
                (seconds==0 || seconds>=1 && seconds<=3600);
            if(DurationInputValid)TestDurationSeconds=seconds;
            return DurationInputValid;
        }
        private void CancelTimer() { if(MotionStarted && motionStoppedAt<0)motionStoppedAt=TimerNow; }
        private void UpdateTimedRun()
        {
            if(!Armed || !boatSource || !IsExtended || !boatSource.waves)return;
            if(!MotionStarted && boatSource.waves.ActivePresetName!=WavePreset.Calm.ToString()) {
                MotionStarted=true;motionStartedAt=TimerNow;motionStoppedAt=-1;
                GetComponent<IkSessionLogger>()?.Capture("MOTION STARTED: "+boatSource.waves.ActivePresetName+"; graph time zero");
            }
            if(TimerRunning && MotionElapsedSeconds>=RunDurationSeconds)
                StopTest("Timed duration reached: "+RunDurationSeconds.ToString("0.###",CultureInfo.InvariantCulture)+"s");
        }
        private bool pulseDropdown,angleDropdown,speedDropdown;
        public bool SetNextMovementSpeed(int speed)
        {
            if(Preparing || speed<50 || speed>500 || speed%50!=0)return false;
            NextMovementSpeedUsPerSecond=speed;return true;
        }
        public bool SetPreparationSettings(int pulse,int angle,float rotation,float heave)
        {
            if(Preparing || pulse<25 || pulse>500 || pulse%25!=0 || angle<1 || angle>10 ||
                float.IsNaN(rotation) || float.IsInfinity(rotation) || rotation<0 || rotation>1 ||
                float.IsNaN(heave) || float.IsInfinity(heave) || heave<0 || heave>.02f)return false;
            NextPulseAllowanceUs=pulse;NextPoseAllowanceDegrees=angle;NextRotationGain=rotation;NextHeaveGain=heave;return true;
        }
        private bool preparationCalibrated;
        private double preparationAt,preparationDeadline;
        public bool SimulationMode { get; private set; }
        public bool CanStartSimulation => isActiveAndEnabled && IsExtended && boatSource && boatSource.waves &&
            !SimulationMode && !Preparing && !Armed && !pendingArm && !pendingHome && !homing &&
            (!ShowOutputControls || (connected && Ready && DurationInputValid));
        internal bool WaitingForSimulationArm => SimulationMode && (Preparing || pendingArm);

        public void StartSimulation()
        {
            if(!CanStartSimulation)return;
            GetComponent<IkSessionLogger>()?.CloseRecording();
            BeginPreparation(true);
        }

        public void PrepareBoatTest()
        {
            if(SimulationMode || Preparing || !DurationInputValid || !boatSource || !boatSource.waves || !IsExtended || !Ready || pendingArm || pendingHome || homing)return;
            GetComponent<IkSessionLogger>()?.StartRecording();
            BeginPreparation(false);
        }
        private void BeginPreparation(bool simulation)
        {
            WaveTransitionSeconds=NextWaveTransitionSeconds;
            boatSource.SelectWavePreset(WavePreset.Calm);boatSource.Resume();
            if(Armed || !Zero(BoardOffsets))Home();
            SimulationMode=simulation;
            Preparing=true;preparationCalibrated=false;preparationAt=Now+WaveTransitionSeconds+3;preparationDeadline=Now+15;
            pulseDropdown=angleDropdown=speedDropdown=false;
            Status=simulation ? "Preparing simulation • Calm" : "Preparing: Calm / gradual neutral / settling; no automatic Arm";
        }
        private void UpdatePreparation()
        {
            if(!Preparing)return;
            bool offlineSimulation=SimulationMode && !ShowOutputControls;
            if((!Ready && !offlineSimulation) || !boatSource || !boatSource.waves || !boatSource.isActiveAndEnabled || boatSource.paused ||
                boatSource.waves.ActivePresetName!=WavePreset.Calm.ToString()) {
                Preparing=SimulationMode=false;Status="Preparation cancelled; select Prepare test again";return;
            }
            if(Now>preparationDeadline) { Hold("Preparation timed out: source/board must settle at neutral");return; }
            if(Now<preparationAt || !CanConfigure)return;
            if(!preparationCalibrated) {
                if(!SetAllowance(NextPulseAllowanceUs) || !boatSource.SetPoseAllowance(NextPoseAllowanceDegrees) || !boatSource.SetGains(NextHeaveGain,NextRotationGain)) {
                    Hold("Preparation settings unavailable");return;
                }
                boatSource.waves.SetSpeedMultiplier(NextWaterSpeedMultiplier);
                boatSource.CalibrateBoat();preparationCalibrated=true;preparationAt=Now+.5;
                MovementSpeedUsPerSecond=NextMovementSpeedUsPerSecond;
                Status="Preparing: calibrated Calm; waiting for fresh exact neutral";return;
            }
            if(!FreshCandidate() || !Zero(CandidateOffsets) || !TargetNeutral())return;
            Preparing=false;
            if(SimulationMode) {
                if(ShowOutputControls) {
                    RequestArm();
                    if(!pendingArm)SimulationMode=false;
                } else Status="Simulation running • USB off";
                return;
            }
            Status="Prepared: rotation"+NextRotationGain.ToString("F2")+" / heave"+NextHeaveGain.ToString("F3")+
                " / ±"+NextPoseAllowanceDegrees+"° / ±"+NextPulseAllowanceUs+"us / "+MovementSpeedUsPerSecond+"us/s. Click ARM, then choose waves.";
            GetComponent<IkSessionLogger>()?.Capture("Boat test prepared; manual Arm required");
        }
        private bool TargetNeutral()=>Preview && Preview.translationMm.sqrMagnitude<=1e-6f &&
            Quaternion.Angle(Preview.target.localRotation,PreviewGeometry.Parse(Preview.geometry.text).NeutralUnityRotation)<=.001f;
        private Vector2 panelScroll;
        private double Now=>Time.realtimeSinceStartupAsDouble;
        private static bool Zero(int[] a)=>a!=null && Array.TrueForAll(a,x=>x==0);
        private static string Vector(int[] a)=>a==null ? "unavailable" : string.Join(",",a);
        private void Start()
        {
            extendedMode=extendedTravel;
            if(!GetComponent<IkSessionLogger>())gameObject.AddComponent<IkSessionLogger>();
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
            CancelTimer();
            GetComponent<IkSessionLogger>()?.EndRun("Connection changed");
            Preparing=SimulationMode=false;
            if(boatSource)boatSource.ResetPoseAllowance();
            AllowanceUs=25; MovementSpeedUsPerSecond=50; connected=value; Ready=Armed=pendingArm=pendingHome=homing=haveAck=false;
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
            CancelTimer();
            Preparing=SimulationMode=false;
            Armed=pendingArm=pendingHome=homing=false; LastStop=reason;
            Send("HOLD"); Status="HOLD: "+reason;
            GetComponent<IkSessionLogger>()?.EndRun(reason);
            GetComponent<IkSessionLogger>()?.Capture("HOLD");
        }
        public void Home()
        {
            CancelTimer();
            Preparing=SimulationMode=false;
            if (!Ready) return;
            GetComponent<IkSessionLogger>()?.EndRun("Gradual neutral requested");
            Armed=pendingArm=homing=false; pendingHome=true; deadline=Now+.25;
            Send("HOME"); Status="Gradual neutral requested";
        }
        public void Off()
        {
            CancelTimer();
            Preparing=SimulationMode=false;
            Armed=pendingArm=pendingHome=homing=false; Send("OFF"); Status="PWM OFF requested; support plate";
            GetComponent<IkSessionLogger>()?.EndRun("PWM OFF requested");
        }
        public void Disconnect()
        {
            Hold("Serial disconnected"); if(serial)serial.enabled=false; OnConnectionEvent(false);
        }
        public void StopTest(string reason="Stop test + save clicked")
        {
            CancelTimer();
            LastStop=reason;
            GetComponent<IkSessionLogger>()?.EndRun(reason);
            if(!Ready) { Hold("Test stopped; board not ready for neutral return");return; }
            if(boatSource)boatSource.PauseAtNeutral();else Home();
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
            if(Preparing || SimulationMode)return;
            RequestArm();
            if(pendingArm)GetComponent<IkSessionLogger>()?.StartRecording();
        }
        private void RequestArm()
        {
            if(boatSource && !DurationInputValid) { Status="Enter a valid test duration before Arm";return; }
            if(boatSource && IsExtended && (AllowanceUs!=NextPulseAllowanceUs || boatSource.PoseAngleAllowance!=NextPoseAllowanceDegrees ||
                Mathf.Abs(boatSource.RotationGain-NextRotationGain)>.00001f || Mathf.Abs(boatSource.TranslationGain-NextHeaveGain)>.00001f || MovementSpeedUsPerSecond!=NextMovementSpeedUsPerSecond || WaveTransitionSeconds!=NextWaveTransitionSeconds || Mathf.Abs(WaterSpeedMultiplier-NextWaterSpeedMultiplier)>.00001f)) {
                Status="Settings changed: click Prepare test before Arm";return;
            }
            if(!Ready || Armed || pendingArm || pendingHome || homing || !Zero(BoardOffsets) || !FreshCandidate() || !Zero(CandidateOffsets))
            { Status="Arm requires fresh source neutral AND board neutral; Calm/calibrate or Reset cube, then Gradual neutral"; return; }
            // Require the actual target pose neutral, not just offsets rounded to zero.
            if(!TargetNeutral())
            { Status="Return source to exact neutral before Arm"; return; }
            pendingArm=true; deadline=Now+.25; Send("ARM",AllowanceUs.ToString(CultureInfo.InvariantCulture)+(IsExtended ? ","+MovementSpeedUsPerSecond : "")); Status="Waiting for explicit ARM acknowledgement";
        }
        private void LateUpdate()
        {
            bool valid=FreshCandidate();
            UpdatePreparation();
            if(Armed && !valid) { Hold(MappingError); return; }
            UpdateTimedRun();
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
            for(int i=0;i<6;i++) if(!int.TryParse(f[at+i],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out values[i]) || values[i]<-MaximumAllowanceUs || values[i]>MaximumAllowanceUs ||
                ApproximateIkPulseMap.NeutralUs[i]+values[i]<1000 || ApproximateIkPulseMap.NeutralUs[i]+values[i]>2000)return false;
            return true;
        }
        public void OnMessageArrived(string message)
        {
            if(string.IsNullOrEmpty(message))return;
            var f=message.Trim().Split(',');
            if(IsExtended && f.Length>=2 && (f[0]=="OI3" || f[0]=="OI4")) { Status="Older firmware: upload updated 09_IK_RANGE_PROBE (OI5, ±500us) for expanded pulse allowances";return; }
            if(f.Length<3 || f[0]!=Protocol || !uint.TryParse(f[2],out uint id) || id!=token)return;
            if(f[1]=="READY" && !Ready && !Armed && !pendingArm && !pendingHome && !homing && ReadSix(f,3,out int[] board)) {
                AllowanceUs=25; MovementSpeedUsPerSecond=50; BoardOffsets=board; Ready=true; Status=Zero(board) ? "Firmware READY; explicit Arm required" : "Firmware READY; retained offsets need Gradual neutral";
            }
            else if(f[1]=="ARMED" && f.Length==(IsExtended ? 5 : 4) && pendingArm && int.TryParse(f[3],out int echo) && echo==AllowanceUs &&
                (!IsExtended || (int.TryParse(f[4],out int speedEcho) && speedEcho==MovementSpeedUsPerSecond))) {
                pendingArm=false; Armed=true; seq=lastAck=0; haveAck=false; ackAt=Now; nextSend=0; LastStop=null; Status="ARMED • six independent provisional IK commands";
                MotionStarted=false;motionStoppedAt=-1;RunDurationSeconds=boatSource && IsExtended ? TestDurationSeconds : 0;
                if(SimulationMode)Status="Simulation running • platform armed";
                else GetComponent<IkSessionLogger>()?.BeginRun();
            }
            else if(f[1]=="ACK" && Armed && f.Length==10 && uint.TryParse(f[3],out uint ack) && ack>0 && ack<=seq && (!haveAck || ack>lastAck) && ReadSix(f,4,out int[] actual)) {
                BoardOffsets=actual; if(Array.Exists(actual,x=>Math.Abs(x)>AllowanceUs)) { Hold("Board exceeded selected allowance"); return; } lastAck=ack; haveAck=true; ackAt=Now;
            }
            else if(f[1]=="HOMING" && f.Length==3 && pendingHome) { pendingHome=false; homing=true; deadline=Now+(IsExtended ? 12 : 4); nextSend=0; Status="Ramping to neutral"; }
            else if(f[1]=="NEUTRAL" && f.Length==3 && homing) { homing=false; BoardOffsets=new int[6]; Status="Board commanded neutral; observe before Arm"; }
            else if((f[1]=="ERR" || f[1]=="WATCHDOG" || f[1]=="HOLD" || f[1]=="OFF")) {
                CancelTimer();
                Preparing=SimulationMode=false;
                Armed=pendingArm=pendingHome=homing=false;
                if(f[1]=="OFF")BoardOffsets=new int[6];
                if(f[1]=="ERR") { Ready=false; nextHello=Now+1; }
                if(f[1]=="ERR" || f[1]=="WATCHDOG")LastStop=message;
                Status=message+"; manual rearm only";
                GetComponent<IkSessionLogger>()?.EndRun(message);
            }
        }
        private void DrawPreparationSettings()
        {
            GUI.enabled=!Preparing && !pendingArm && !pendingHome && !homing;
            GUILayout.Label("Next test");
            if(GUILayout.Button("Pulse allowance: ±"+NextPulseAllowanceUs+" µs ▾")) { pulseDropdown=!pulseDropdown;angleDropdown=speedDropdown=false; }
            if(pulseDropdown) {
                string[] labels=new string[20];for(int i=0;i<20;i++)labels[i]="±"+((i+1)*25);
                int index=GUILayout.SelectionGrid(NextPulseAllowanceUs/25-1,labels,5);
                if(index!=NextPulseAllowanceUs/25-1) { SetPreparationSettings((index+1)*25,NextPoseAllowanceDegrees,NextRotationGain,NextHeaveGain);pulseDropdown=false; }
            }
            if(GUILayout.Button("Pose angle allowance: ±"+NextPoseAllowanceDegrees+"° ▾")) { angleDropdown=!angleDropdown;pulseDropdown=speedDropdown=false; }
            if(angleDropdown) {
                string[] labels=new string[10];for(int i=0;i<10;i++)labels[i]=(i+1)+"°";
                int index=GUILayout.SelectionGrid(NextPoseAllowanceDegrees-1,labels,5);
                if(index!=NextPoseAllowanceDegrees-1) { SetPreparationSettings(NextPulseAllowanceUs,index+1,NextRotationGain,NextHeaveGain);angleDropdown=false; }
            }
            GUILayout.Label("Rotation gain: "+NextRotationGain.ToString("F2"));
            float r=Mathf.Round(GUILayout.HorizontalSlider(NextRotationGain,0,1)*100)/100;
            GUILayout.Label("Heave gain: "+NextHeaveGain.ToString("F4"));
            float h=Mathf.Round(GUILayout.HorizontalSlider(NextHeaveGain,0,.02f)*10000)/10000;
            SetPreparationSettings(NextPulseAllowanceUs,NextPoseAllowanceDegrees,r,h);
            if(GUILayout.Button("Movement speed: "+NextMovementSpeedUsPerSecond+" µs/s ▾")) { speedDropdown=!speedDropdown;pulseDropdown=angleDropdown=false; }
            if(speedDropdown) {
                string[] labels=new string[10];for(int i=0;i<10;i++)labels[i]=((i+1)*50).ToString();
                int index=GUILayout.SelectionGrid(NextMovementSpeedUsPerSecond/50-1,labels,5);
                if(index!=NextMovementSpeedUsPerSecond/50-1) { SetNextMovementSpeed((index+1)*50);speedDropdown=false; }
            }
            GUILayout.Label("Neutral return 50 µs/s");
            GUILayout.Label("Water speed: "+NextWaterSpeedMultiplier.ToString("F2")+"×");
            float waterSpeed=Mathf.Round(GUILayout.HorizontalSlider(NextWaterSpeedMultiplier,0,4)*4)/4;
            if(waterSpeed!=NextWaterSpeedMultiplier)SetNextWaterSpeed(waterSpeed);
            GUILayout.Label("Wave transition: "+NextWaveTransitionSeconds+" seconds");
            int transition=GUILayout.Toolbar(NextWaveTransitionSeconds-1,new[]{"1s","2s","3s"})+1;
            if(transition!=NextWaveTransitionSeconds)SetNextWaveTransition(transition);
            GUI.enabled=!Armed && !Preparing && !pendingArm && !pendingHome && !homing;
            GUILayout.Label("Duration (s) • 0 for manual stop");
            string text=GUILayout.TextField(durationText);
            if(text!=durationText)SetTestDuration(text);
            if(!DurationInputValid)GUILayout.Label("Enter 1–3600 seconds, or 0 for manual. Decimals use a dot.");
            GUI.enabled=!SimulationMode && DurationInputValid && Ready && !pendingArm && !pendingHome && !homing && !Preparing;
            if(GUILayout.Button("Prepare test"))PrepareBoatTest();GUI.enabled=true;
        }
        internal bool ShowOutputControls => connected || (serial && serial.enabled);

        internal void DrawBoatConnection()
        {
            GUI.enabled=!ShowOutputControls && !SimulationMode;
            portName=GUILayout.TextField(portName);
            if(GUILayout.Button("Connect USB (PWM off)")) Connect();
            GUI.enabled=true;
            if(ShowOutputControls && GUILayout.Button("Disconnect USB")) Disconnect();
            DrawPreparationSettings();
            GUI.enabled=!SimulationMode && DurationInputValid && Ready && !Armed && !pendingArm && !pendingHome && !homing && !Preparing;
            if(GUILayout.Button("Arm at Calm + neutral")) Arm();
            GUI.enabled=true;
            GUILayout.Label("Target µs   "+Vector(CandidateOffsets));
            GUILayout.Label("Board µs    "+Vector(BoardOffsets));
            var logger=GetComponent<IkSessionLogger>();
            if(logger && !string.IsNullOrEmpty(logger.FilePath))
                GUILayout.Label(System.IO.Path.GetFileName(logger.FilePath));
        }

        internal void StopSimulation()
        {
            if(!SimulationMode) return;
            if(ShowOutputControls) StopTest("Simulation stopped");
            else {
                boatSource.PauseAtNeutral();
                Status="Simulation stopped";
            }
        }

        internal void DrawBoatOutput()
        {
            if(!ShowOutputControls) return;
            if(TimerRunning) GUILayout.Label($"Auto-stop in {Math.Max(0,RunDurationSeconds-MotionElapsedSeconds):F1} s");
            if(!SimulationMode && GUILayout.Button("Stop + save / gradual neutral")) StopTest();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Hold")) Hold();
            if(GUILayout.Button("Neutral")) Home();
            GUILayout.EndHorizontal();
            if(GUILayout.Button("PWM off • support plate")) Off();
        }

        private void OnGUI()
        {
            if (Preview && Preview.HasPresentationOverlay) return;
            GUILayout.BeginArea(new Rect(Screen.width-395,12,383,Mathf.Min(Screen.height-24,900)),GUI.skin.box);
            GUILayout.Label(boatSource ? "BOAT → INDIVIDUAL IK" : "INDIVIDUAL IK • STATIC SWEEP");
            panelScroll=GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("Offset −5mm estimate; 11us/degree estimate. Selected ±"+AllowanceUs+"us; motion "+MovementSpeedUsPerSecond+"us/s; home50us/s.");
            GUILayout.Label(Status); if(!string.IsNullOrEmpty(LastStop))GUILayout.Label("Last stop: "+LastStop);
            GUILayout.Label("Candidate offsets 0..5 (us): "+Vector(CandidateOffsets));
            GUILayout.Label("Board offsets 0..5 (us): "+Vector(BoardOffsets));
            if(MappingError!=null)GUILayout.Label(MappingError);
            GUI.enabled=!connected; portName=GUILayout.TextField(portName);
            if(GUILayout.Button("Connect USB — no PWM"))Connect(); GUI.enabled=true;
            if(GUILayout.Button("Disconnect link"))Disconnect();
            if(boatSource && IsExtended) {
                DrawPreparationSettings();
            }
            GUI.enabled=!SimulationMode && DurationInputValid && Ready && !Armed && !pendingArm && !pendingHome && !homing && !Preparing;
            if(GUILayout.Button(boatSource ? "ARM at Calm + board neutral" : "ARM at cube + board neutral"))Arm(); GUI.enabled=true;
            if(!boatSource || !IsExtended) {
            GUI.enabled=CanConfigure && !Preparing;
            GUILayout.BeginHorizontal();
            int[] allowances=IsExtended ? new[]{25,50,75,100,125,150,175,200,225,250,275,300,325,350,375,400,425,450,475,500} : new[]{25,50,100};
            for(int i=0;i<allowances.Length;i++) {
                if(i>0 && i%5==0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if(GUILayout.Button("±"+allowances[i]+"us"))SetAllowance(allowances[i]);
            }
            GUILayout.EndHorizontal(); GUI.enabled=true;
            }
            var sweep=GetComponent<IkAxisSweep>(); if(sweep)sweep.DrawControls();
            GUILayout.EndScrollView();
            if(boatSource && Armed)GUILayout.Label(TimerRunning ? "Auto-stop in "+Math.Max(0,RunDurationSeconds-MotionElapsedSeconds).ToString("F1")+"s" :
                RunDurationSeconds==0 ? "Manual stop selected" : "Timer waiting: choose a wave preset");
            if(boatSource && GUILayout.Button("Stop test + save • gradual neutral"))StopTest();
            var logger=GetComponent<IkSessionLogger>();
            if(logger && !string.IsNullOrEmpty(logger.FilePath))GUILayout.Label("Log: "+System.IO.Path.GetFileName(logger.FilePath));
            if(GUILayout.Button("HOLD current PWM"))Hold();
            if(GUILayout.Button("Gradual neutral"))Home();
            if(GUILayout.Button("PWM OFF — support plate"))Off();
            GUILayout.Label("IK with estimated geometry/calibration; commanded offsets are not measured pose feedback.");
            GUILayout.EndArea();
        }
        private void OnDisable() { Hold("Component disabled / Play stopped"); Ready=false; }
    }
}
