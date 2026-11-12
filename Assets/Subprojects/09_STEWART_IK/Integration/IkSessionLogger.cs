using System;
using System.IO;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    // Diagnostic observer only: never opens USB or changes motion commands.
    [DefaultExecutionOrder(600)]
    public sealed class IkSessionLogger : MonoBehaviour
    {
        private IndividualIkSweepProbe probe;
        private StreamWriter writer;
        private double nextSample, nextFlush;
        public string FilePath { get; private set; }
        private int runNumber;
        private bool runActive;

        [Serializable] private sealed class Snapshot
        {
            public string utc, reason, scene, status, lastStop, mappingError, sourceStatus, flags, waves;
            public double elapsedSeconds, sourceTimeSeconds,testDurationSeconds,motionElapsedSeconds;
            public float frameSeconds, heaveGain, rotationGain, poseAllowanceDegrees,waveTransitionProgress,waterSpeedMultiplier,waveTimeSeconds;
            public bool focused, ready, armed, sourceValid, batchMode,motionStarted,timerRunning,waveTransitioning;
            public int pulseAllowanceUs,run,movementSpeedUsPerSecond,neutralReturnSpeedUsPerSecond,waveTransitionSeconds;
            public ulong sourceSequence;
            public Vector3 mappedTranslationMm, mappedPitchYawRollDegrees;
            public int[] candidateOffsetsUs, boardOffsetsUs;
        }

        private void Awake()
        {
            probe=GetComponent<IndividualIkSweepProbe>();
            if(probe && !GetComponent<BoatPresentationOverlay>()) StartRecording();
        }

        public void StartRecording()
        {
            if(!probe || writer!=null)return;
            OpenFile("session");
            if(writer==null)return;
            Application.logMessageReceived+=OnLog;
            Capture("session started");
        }
        private void OpenFile(string label)
        {
            try {
                string folder=Path.Combine(Application.dataPath,"..","TestLogs","Stewart");
                Directory.CreateDirectory(folder);
                FilePath=Path.GetFullPath(Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+label+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".txt"));
                writer=new StreamWriter(FilePath,false);
                writer.WriteLine("# Ocean Motion IK session v1; UTC; JSON snapshot per line; sampled 5Hz plus HOLD/warnings/errors.");
                writer.WriteLine("# Offsets are microseconds from saved neutrals [1500,1600,1550,1600,1500,1500]. Board values are acknowledged commands, not measured positions.");
                writer.WriteLine("# Provisional calibration: 11us/degree, -5mm horn offset. Null candidate = unavailable/rejected, not zero.");
                Debug.Log("IK session recording: "+FilePath,this);
            } catch(Exception e) { StopLogging(e); }
        }
        public void BeginRun()
        {
            if(writer==null)return;
            EndRun("Next run started");
            Capture("recording continued in next run file");
            try { writer.Dispose();writer=null; } catch(Exception e) { StopLogging(e);return; }
            runNumber++;OpenFile("run-"+runNumber.ToString("D3"));
            runActive=writer!=null;Capture("TEST STARTED: ARM acknowledged");
        }
        public void EndRun(string reason)
        {
            if(!runActive)return;
            runActive=false;Capture("TEST STOPPED: "+reason+"; recording neutral return/status until next run");
        }

        private void LateUpdate()
        {
            if(Time.realtimeSinceStartupAsDouble<nextSample)return;
            nextSample=Time.realtimeSinceStartupAsDouble+.2;
            Capture("sample");
        }

        public void Capture(string reason)
        {
            if(writer==null || !probe)return;
            try {
                var boat=probe.boatSource;
                var c=boat ? boat.LatestCommand : probe.source ? probe.source.LatestCommand : default;
                var s=new Snapshot {
                    utc=DateTime.UtcNow.ToString("O"),reason=reason,scene=gameObject.scene.name,
                    elapsedSeconds=Time.realtimeSinceStartupAsDouble,frameSeconds=Time.unscaledDeltaTime,
                    testDurationSeconds=probe.RunDurationSeconds,motionElapsedSeconds=probe.MotionElapsedSeconds,
                    motionStarted=probe.MotionStarted,timerRunning=probe.TimerRunning,
                    focused=Application.isFocused,batchMode=Application.isBatchMode,ready=probe.Ready,armed=probe.Armed,
                    waves=boat && boat.waves ? boat.waves.ActivePresetName : "none",
                    waveTransitionSeconds=boat ? probe.WaveTransitionSeconds : 0,
                    waveTransitionProgress=boat && boat.waves ? boat.waves.TransitionProgress : 1,
                    waveTransitioning=boat && boat.waves && boat.waves.IsTransitioning,
                    waterSpeedMultiplier=boat && boat.waves ? boat.waves.SpeedMultiplier : 1,
                    waveTimeSeconds=boat && boat.waves ? boat.waves.WaveTimeAt(Time.time) : 0,
                    status=probe.Status,lastStop=probe.LastStop,mappingError=probe.MappingError,
                    run=runNumber,movementSpeedUsPerSecond=probe.MovementSpeedUsPerSecond,neutralReturnSpeedUsPerSecond=50,
                    pulseAllowanceUs=probe.AllowanceUs,heaveGain=boat ? boat.TranslationGain : 0,
                    rotationGain=boat ? boat.RotationGain : 0,poseAllowanceDegrees=boat ? boat.PoseAngleAllowance : probe.source ? probe.source.smallAngleExtent : 0,
                    sourceStatus=boat ? boat.SourceStatus : "manual cube",sourceValid=c.IsValid,
                    sourceSequence=c.SourceSequence,sourceTimeSeconds=c.SourceTimeSeconds,flags=c.LimitFlags.ToString(),
                    mappedTranslationMm=c.TranslationMeters*1000,mappedPitchYawRollDegrees=c.RotationDegrees,
                    candidateOffsetsUs=probe.CandidateOffsets,boardOffsetsUs=probe.BoardOffsets
                };
                writer.WriteLine(JsonUtility.ToJson(s));
                if(reason!="sample" || s.elapsedSeconds>=nextFlush) {
                    writer.Flush();nextFlush=s.elapsedSeconds+1;
                    // Bound a long unattended session without deleting earlier evidence.
                    if(writer.BaseStream.Position>50*1024*1024)StopLogging(new IOException("Session reached the 50 MiB recording cap; start a new Play session to record more."));
                }
            } catch(Exception e) { StopLogging(e); }
        }

        private void OnLog(string message,string trace,LogType type)
        {
            if(type==LogType.Warning || type==LogType.Error || type==LogType.Exception)
                Capture(type+": "+message+"\n"+trace);
        }
        private void StopLogging(Exception error)
        {
            Application.logMessageReceived-=OnLog;
            var old=writer;writer=null;
            try { old?.Dispose(); } catch { /* Logging failures must not interrupt motion handling. */ }
            Debug.LogWarning("IK session recording stopped: "+error.Message,this);
        }
        public void CloseRecording()
        {
            EndRun("Recording closed");
            Capture("session ended");
            Application.logMessageReceived-=OnLog;
            try { writer?.Dispose(); } catch { }
            writer=null;
            FilePath=null;
        }
        private void OnDestroy() => CloseRecording();
    }
}
