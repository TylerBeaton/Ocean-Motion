using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace OceanMotion.Subproject06.Tests
{
    public class MotionTransportSessionTests
    {
        [Test]
        public void TryCreateNextMessage_DoesNotSendBeforeFirmwareReady()
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, 0d);

            bool created = session.TryCreateNextMessage(
                ValidCommand(),
                0d,
                out string message);

            Assert.That(created, Is.False);
            Assert.That(message, Is.Null);
        }

        [Test]
        public void HandleIncomingMessage_IgnoresReadyBeforeHelloWasSent()
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, 0d);

            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 0d);

            Assert.That(session.IsFirmwareReady, Is.False);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0d, out _),
                Is.False);
        }

        [Test]
        public void HandleIncomingMessage_IgnoresReadyUntilHelloReachedWire()
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, 0d);
            session.TryCreateHandshakeMessage(0d, out _);

            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 0d);
            Assert.That(session.IsFirmwareReady, Is.False);

            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 0.01d);
            Assert.That(session.IsFirmwareReady, Is.True);
        }

        [Test]
        public void TryCreateHandshakeMessage_RetriesHelloUntilReady()
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, 0d);

            Assert.That(
                session.TryCreateHandshakeMessage(0d, out string first),
                Is.True);
            Assert.That(first, Is.EqualTo("OM1,HELLO"));
            Assert.That(
                session.TryCreateHandshakeMessage(0.999d, out _),
                Is.False);
            Assert.That(
                session.TryCreateHandshakeMessage(1d, out string retry),
                Is.True);
            Assert.That(retry, Is.EqualTo("OM1,HELLO"));

            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage("OM1,READY", 1.1d);
            Assert.That(
                session.TryCreateHandshakeMessage(2d, out _),
                Is.False);
        }

        [Test]
        public void TryCreateNextMessage_SendsFirstPoseAfterReady()
        {
            var session = ReadySession();

            bool created = session.TryCreateNextMessage(
                ValidCommand(9),
                0d,
                out string message);

            Assert.That(created, Is.True);
            Assert.That(
                message,
                Is.EqualTo("OM1,POSE,1,9,0.10000,1.00000,2.00000,3.00000"));
        }

        [Test]
        public void TryCreateNextMessage_LimitsPosePacketsToTwentyHertz()
        {
            var session = ReadySession();

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0d, out _),
                Is.True);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.049d, out _),
                Is.False);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(2), 0.05d, out string message),
                Is.True);
            Assert.That(message, Does.StartWith("OM1,POSE,2,2,"));
        }

        [Test]
        public void TryCreateNextMessage_StopsWhenSourceSequenceStallsInRealtime()
        {
            var session = ReadySession();
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0d, out _),
                Is.True);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.251d, out string message),
                Is.True);
            Assert.That(message, Is.EqualTo(MotionTransportProtocol.StopMessage));
        }

        [Test]
        public void TryCreateNextMessage_FreshSourceSequenceResetsRealtimeAge()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(1), 0d, out _);
            session.TryCreateNextMessage(ValidCommand(2), 0.24d, out _);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(2), 0.48d, out string message),
                Is.True);
            Assert.That(message, Does.StartWith("OM1,POSE,"));
        }

        [Test]
        public void Reconnect_DoesNotMakeAnOldSourceSequenceFreshAgain()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(), 0d, out _);
            session.SetConnected(false, 0.1d);
            session.SetConnected(true, 1d);
            session.TryCreateHandshakeMessage(1d, out _);
            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 1d);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 1d, out string message),
                Is.True);
            Assert.That(message, Is.EqualTo(MotionTransportProtocol.StopMessage));
        }

        [Test]
        public void Reconnect_ContinuesTransportSequenceAndIgnoresOldAcknowledgement()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(1), 0d, out string first);
            Assert.That(first, Does.StartWith("OM1,POSE,1,1,"));

            session.SetConnected(false, 0.01d);
            session.SetConnected(true, 0.02d);
            session.TryCreateHandshakeMessage(0.02d, out _);
            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 0.02d);
            session.TryCreateNextMessage(ValidCommand(2), 0.02d, out string second);

            Assert.That(second, Does.StartWith("OM1,POSE,2,2,"));
            session.HandleIncomingMessage("OM1,ACK,1", 0.03d);
            Assert.That(session.AcknowledgedPoseCount, Is.Zero);
            session.HandleIncomingMessage("OM1,ACK,2", 0.04d);
            Assert.That(session.LastAcknowledgedSequence, Is.EqualTo(2u));
        }

        [Test]
        public void TryCreateNextMessage_SendsOneStopForInvalidState()
        {
            var session = ReadySession();

            Assert.That(
                session.TryCreateNextMessage(default, 0d, out string message),
                Is.True);
            Assert.That(message, Is.EqualTo("OM1,STOP"));
            Assert.That(
                session.TryCreateNextMessage(default, 0.05d, out _),
                Is.False);
        }

        [Test]
        public void TryCreateNextMessage_WaitsForStoppedBeforeResumingPose()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(default, 0d, out _);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.05d, out _),
                Is.False);

            session.HandleIncomingMessage("OM1,STOPPED", 0.06d);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.06d, out string message),
                Is.True);
            Assert.That(message, Does.StartWith("OM1,POSE,1,"));
        }

        [Test]
        public void TryCreateNextMessage_RetriesStopUntilStopped()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(default, 0d, out _);

            Assert.That(
                session.TryCreateNextMessage(default, 0.249d, out _),
                Is.False);
            Assert.That(
                session.TryCreateNextMessage(default, 0.25d, out string retry),
                Is.True);
            Assert.That(retry, Is.EqualTo(MotionTransportProtocol.StopMessage));
            Assert.That(session.AwaitingStopConfirmation, Is.True);
        }

        [TestCase("OM1,READY")]
        [TestCase("OM1,WATCHDOG")]
        public void HandleIncomingMessage_DoesNotReleaseStopWithoutStopped(
            string deviceMessage)
        {
            var session = ReadySession();
            session.TryCreateNextMessage(default, 0d, out _);

            session.HandleIncomingMessage(deviceMessage, 0.1d);

            Assert.That(session.AwaitingStopConfirmation, Is.True);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.1d, out _),
                Is.False);
        }

        [Test]
        public void HandleIncomingMessage_RecordsApplicationLatencyWithoutWriteBookkeeping()
        {
            var session = ReadySession(10d);
            session.TryCreateNextMessage(ValidCommand(), 10d, out _);

            session.HandleIncomingMessage("OM1,ACK,1", 10.012d);

            Assert.That(session.AcknowledgedPoseCount, Is.EqualTo(1u));
            Assert.That(session.LastAcknowledgedSequence, Is.EqualTo(1u));
            Assert.That(session.LastAcknowledgementLatencyMilliseconds,
                Is.EqualTo(12d).Within(0.0001d));
        }

        [Test]
        public void HandleIncomingMessage_CountsOlderAckWithoutInventingItsLatency()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(1), 0d, out _);
            session.TryCreateNextMessage(ValidCommand(2), 0.05d, out _);
            session.HandleIncomingMessage("OM1,ACK,1", 0.06d);
            Assert.That(session.AcknowledgedPoseCount, Is.EqualTo(1u));
            Assert.That(session.LastAcknowledgementLatencyMilliseconds, Is.Zero);
            session.HandleIncomingMessage("OM1,ACK,2", 0.07d);
            Assert.That(session.AcknowledgedPoseCount, Is.EqualTo(2u));
            Assert.That(session.LastAcknowledgementLatencyMilliseconds,
                Is.EqualTo(20d).Within(0.0001d));
        }

        [TestCase("OM1,ACK,0")]
        [TestCase("OM1,ACK,2")]
        [TestCase("OM1,ACK,-1")]
        [TestCase("OM1,ACK,1,extra")]
        public void HandleIncomingMessage_IgnoresUnissuedOrMalformedAck(string message)
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(), 0d, out _);
            session.HandleIncomingMessage(message, 0.01d);
            Assert.That(session.AcknowledgedPoseCount, Is.Zero);
        }

        [Test]
        public void HandleIncomingMessage_DoesNotCountDuplicateOrDisconnectedAck()
        {
            var session = ReadySession();
            session.TryCreateNextMessage(ValidCommand(), 0d, out _);
            session.HandleIncomingMessage("OM1,ACK,1", 0.01d);
            session.HandleIncomingMessage("OM1,ACK,1", 0.02d);
            Assert.That(session.AcknowledgedPoseCount, Is.EqualTo(1u));
            session.TryCreateNextMessage(ValidCommand(2), 0.05d, out _);
            session.SetConnected(false, 0.06d);
            session.HandleIncomingMessage("OM1,ACK,2", 0.07d);
            Assert.That(session.AcknowledgedPoseCount, Is.EqualTo(1u));
            Assert.That(session.LastAcknowledgedSequence, Is.Zero);
        }

        [Test]
        public void HandleIncomingMessage_ProtocolErrorRequiresFreshHandshake()
        {
            var session = ReadySession();

            session.HandleIncomingMessage("OM1,ERR,RANGE", 0.1d);

            Assert.That(session.IsFirmwareReady, Is.False);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0.1d, out _),
                Is.False);
            Assert.That(
                session.TryCreateHandshakeMessage(0.1d, out string hello),
                Is.True);
            Assert.That(hello, Is.EqualTo(MotionTransportProtocol.HelloMessage));
        }

        [Test]
        public void TryCreateNextMessage_RehandshakesBeforeSequenceWouldWrap()
        {
            var session = ReadySession();
            typeof(MotionTransportSession).GetField(
                "nextTransportSequence",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, uint.MaxValue);

            Assert.That(
                session.TryCreateNextMessage(ValidCommand(), 0d, out _),
                Is.False);
            Assert.That(session.IsFirmwareReady, Is.False);
            Assert.That(
                session.TryCreateHandshakeMessage(0d, out string hello),
                Is.True);
            Assert.That(hello, Is.EqualTo(MotionTransportProtocol.HelloMessage));

            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage(MotionTransportProtocol.ReadyMessage, 0.01d);
            Assert.That(
                session.TryCreateNextMessage(ValidCommand(2), 0.01d, out string pose),
                Is.True);
            Assert.That(pose, Does.StartWith("OM1,POSE,1,2,"));
        }

        [Test]
        public void HandleIncomingMessage_TracksFirmwareStatus()
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, 0d);
            Assert.That(session.IsConnected, Is.True);
            Assert.That(session.IsFirmwareReady, Is.False);

            session.TryCreateHandshakeMessage(0d, out _);
            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage("OM1,READY", 0d);
            Assert.That(session.IsFirmwareReady, Is.True);

            session.HandleIncomingMessage("OM1,WATCHDOG", 1d);
            session.HandleIncomingMessage("OM1,ERR,PACKET", 2d);

            Assert.That(session.WatchdogTripCount, Is.EqualTo(1u));
            Assert.That(session.ProtocolErrorCount, Is.EqualTo(1u));
            Assert.That(session.LastDeviceMessage, Is.EqualTo("OM1,ERR,PACKET"));
        }

        private static MotionPoseCommand ValidCommand(ulong sourceSequence = 1)
        {
            return new MotionPoseCommand(
                new Vector3(0f, 0.1f, 0f),
                new Vector3(1f, 2f, 3f),
                sourceSequence,
                1d,
                true);
        }

        private static MotionTransportSession ReadySession(double nowSeconds = 0d)
        {
            var session = new MotionTransportSession();
            session.SetConnected(true, nowSeconds);
            session.TryCreateHandshakeMessage(nowSeconds, out _);
            session.NotifyHandshakeWritten();
            session.HandleIncomingMessage(
                MotionTransportProtocol.ReadyMessage,
                nowSeconds);
            return session;
        }
    }
}
