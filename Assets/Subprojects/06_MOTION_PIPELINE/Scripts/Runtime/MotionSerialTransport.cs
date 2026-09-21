using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// Streams the latest bounded platform pose to the SP06 Arduino receiver.
    /// The receiver acknowledges packets but does not drive actuators.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class MotionSerialTransport : MonoBehaviour
    {
        public const int BaudRate = 115200;

        [SerializeField] private MotionPipelineController controller;
        [SerializeField] private SerialController serialController;

        private MotionTransportSession session;
        private bool portConnected;

        public int RequiredBaudRate => BaudRate;
        public bool IsConfigurationValid =>
            controller != null && serialController != null &&
            serialController.baudRate == BaudRate;
        public bool IsConnected => session != null && session.IsConnected;
        public bool IsFirmwareReady => session != null && session.IsFirmwareReady;
        public bool AwaitingStopConfirmation =>
            session != null && session.AwaitingStopConfirmation;
        public uint AcknowledgedPoseCount =>
            session == null ? 0u : session.AcknowledgedPoseCount;
        public uint LastAcknowledgedSequence =>
            session == null ? 0u : session.LastAcknowledgedSequence;
        public double LastAcknowledgementLatencyMilliseconds =>
            session == null ? 0d : session.LastAcknowledgementLatencyMilliseconds;
        public uint WatchdogTripCount =>
            session == null ? 0u : session.WatchdogTripCount;
        public uint ProtocolErrorCount =>
            session == null ? 0u : session.ProtocolErrorCount;
        public string LastDeviceMessage =>
            session == null ? null : session.LastDeviceMessage;

        private void Reset()
        {
            controller = GetComponent<MotionPipelineController>();
            serialController = GetComponent<SerialController>();
        }

        private void Awake()
        {
            session = new MotionTransportSession();

            if (controller == null)
                controller = GetComponent<MotionPipelineController>();
            if (serialController == null)
                serialController = GetComponent<SerialController>();

            if (controller == null || serialController == null)
            {
                Debug.LogError(
                    "MotionSerialTransport requires MotionPipelineController " +
                    "and SerialController references.",
                    this);
                return;
            }

            if (serialController.baudRate != BaudRate)
            {
                Debug.LogError(
                    $"SP06 serial baud must be {BaudRate}, not " +
                    $"{serialController.baudRate}.",
                    this);
            }

            serialController.SetTearDownFunction(QueueStopMessage);
        }

        private void OnEnable()
        {
            EnsureSession();
            DrainWriteOutcomes();
            session.SetConnected(portConnected && IsConfigurationValid, NowSeconds);
        }

        private void Update()
        {
            if (controller == null || serialController == null || session == null)
                return;

            double nowSeconds = NowSeconds;
            DrainWriteOutcomes();
            if (!IsConfigurationValid || !serialController.isActiveAndEnabled)
            {
                if (session.IsConnected)
                {
                    QueueStopMessage();
                    session.SetConnected(false, nowSeconds);
                }
                return;
            }

            if (session.TryCreateHandshakeMessage(nowSeconds, out string handshake))
            {
                QueueLatestMessage(handshake);
                return;
            }

            if (session.TryCreateNextMessage(
                controller.LatestCommand,
                nowSeconds,
                out string message))
            {
                // All SP06 outbound records represent supersedable state. A STOP
                // replaces any older pose and the session waits for STOPPED before
                // allowing motion packets to resume.
                QueueLatestMessage(message);
            }
        }

        public void OnConnectionEvent(bool isConnected)
        {
            portConnected = isConnected;
            EnsureSession();
            DrainWriteOutcomes();
            isConnected = isConnected && IsConfigurationValid;
            session.SetConnected(isConnected, NowSeconds);
            Debug.Log(
                isConnected
                    ? "SP06 Arduino serial port connected; waiting for OM1,READY."
                    : "SP06 Arduino serial port disconnected.",
                this);
        }

        public void OnMessageArrived(string message)
        {
            EnsureSession();
            DrainWriteOutcomes();
            session.HandleIncomingMessage(
                message,
                NowSeconds);

            if (!MotionTransportProtocol.TryParseAcknowledgement(message, out _))
                Debug.Log("Arduino → Unity: " + message.Trim(), this);
        }

        private void QueueStopMessage()
        {
            if (serialController == null || session == null || !session.IsConnected)
                return;

            QueueLatestMessage(MotionTransportProtocol.StopMessage);
        }

        private void QueueLatestMessage(string message)
        {
            serialController.SendLatestSerialMessage(message);
        }

        private void OnDisable()
        {
            if (session != null && session.IsConnected)
            {
                QueueStopMessage();
                session.SetConnected(
                    false,
                    NowSeconds);
            }
        }

        private void DrainWriteOutcomes()
        {
            if (serialController == null || session == null)
                return;

            for (int i = 0; i < 64; i++)
            {
                string writtenMessage = serialController.ReadSentSerialMessage();
                if (writtenMessage == null)
                    break;

                if (writtenMessage == MotionTransportProtocol.HelloMessage)
                    session.NotifyHandshakeWritten();
            }
        }

        private static double NowSeconds =>
            (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private void EnsureSession()
        {
            if (session == null)
                session = new MotionTransportSession();
        }
    }
}
