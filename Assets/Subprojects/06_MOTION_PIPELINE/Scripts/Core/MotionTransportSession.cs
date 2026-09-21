namespace OceanMotion.Subproject06
{
    public sealed class MotionTransportSession
    {
        public const double SendIntervalSeconds = 0.05d;

        public const double HandshakeIntervalSeconds = 1d;
        public const double StopRetryIntervalSeconds = 0.25d;
        public const double SourceFreshnessTimeoutSeconds = 0.25d;

        private bool connected;
        private bool firmwareReady;
        private bool stopSentForInvalidState;
        private bool awaitingStopConfirmation;
        private bool awaitingReadyAfterHello;
        private uint nextTransportSequence = 1;
        private double nextSendTimeSeconds;
        private double nextHandshakeTimeSeconds;
        private double nextStopRetryTimeSeconds;
        private bool hasObservedSourceSequence;
        private ulong lastObservedSourceSequence;
        private double lastSourceAdvanceTimeSeconds;
        private uint firstSequenceInSession = 1;
        private uint latestPoseSequence;
        private double latestPoseTimeSeconds;

        public uint AcknowledgedPoseCount { get; private set; }
        public uint LastAcknowledgedSequence { get; private set; }
        public double LastAcknowledgementLatencyMilliseconds { get; private set; }
        public bool AwaitingStopConfirmation => awaitingStopConfirmation;
        public bool IsConnected => connected;
        public bool IsFirmwareReady => firmwareReady;
        public uint WatchdogTripCount { get; private set; }
        public uint ProtocolErrorCount { get; private set; }
        public string LastDeviceMessage { get; private set; }

        public void SetConnected(bool connected, double nowSeconds)
        {
            ResetAcknowledgements();
            this.connected = connected;
            firmwareReady = false;
            stopSentForInvalidState = false;
            awaitingStopConfirmation = false;
            awaitingReadyAfterHello = false;
            nextSendTimeSeconds = nowSeconds;
            nextHandshakeTimeSeconds = nowSeconds;
        }

        public void HandleIncomingMessage(string message, double nowSeconds)
        {
            string trimmedMessage = message == null ? null : message.Trim();
            LastDeviceMessage = trimmedMessage;

            if (connected && firmwareReady &&
                MotionTransportProtocol.TryParseAcknowledgement(
                trimmedMessage,
                out uint acknowledgedSequence) &&
                acknowledgedSequence >= firstSequenceInSession &&
                acknowledgedSequence < nextTransportSequence &&
                acknowledgedSequence > LastAcknowledgedSequence)
            {
                LastAcknowledgedSequence = acknowledgedSequence;
                // Sample only the latest pose; older ACKs still count, but their
                // timestamps are not retained. This includes host queue/frame delay.
                if (acknowledgedSequence == latestPoseSequence)
                    LastAcknowledgementLatencyMilliseconds =
                        (nowSeconds - latestPoseTimeSeconds) * 1000d;
                AcknowledgedPoseCount++;
                return;
            }

            if (connected && trimmedMessage == MotionTransportProtocol.StoppedMessage)
            {
                awaitingStopConfirmation = false;
                nextSendTimeSeconds = nowSeconds;
                return;
            }

            if (connected && trimmedMessage == MotionTransportProtocol.WatchdogMessage)
            {
                WatchdogTripCount++;
                return;
            }

            if (connected && trimmedMessage != null &&
                trimmedMessage.StartsWith(MotionTransportProtocol.ErrorPrefix))
            {
                ProtocolErrorCount++;
                RequireFreshHandshake(nowSeconds, false);
                return;
            }

            if (connected && awaitingReadyAfterHello && !firmwareReady &&
                !awaitingStopConfirmation &&
                trimmedMessage == MotionTransportProtocol.ReadyMessage)
            {
                firmwareReady = true;
                awaitingReadyAfterHello = false;
                stopSentForInvalidState = false;
                awaitingStopConfirmation = false;
                nextSendTimeSeconds = nowSeconds;
            }
        }

        public bool TryCreateHandshakeMessage(
            double nowSeconds,
            out string message)
        {
            if (!connected || firmwareReady || nowSeconds < nextHandshakeTimeSeconds)
            {
                message = null;
                return false;
            }

            message = MotionTransportProtocol.HelloMessage;
            nextHandshakeTimeSeconds = nowSeconds + HandshakeIntervalSeconds;
            return true;
        }

        public void NotifyHandshakeWritten()
        {
            if (connected && !firmwareReady)
                awaitingReadyAfterHello = true;
        }

        public bool TryCreateNextMessage(
            MotionPoseCommand command,
            double nowSeconds,
            out string message)
        {
            if (!connected || !firmwareReady)
            {
                message = null;
                return false;
            }

            if (awaitingStopConfirmation)
            {
                if (nowSeconds < nextStopRetryTimeSeconds)
                {
                    message = null;
                    return false;
                }

                message = MotionTransportProtocol.StopMessage;
                nextStopRetryTimeSeconds = nowSeconds + StopRetryIntervalSeconds;
                return true;
            }

            if (nowSeconds < nextSendTimeSeconds)
            {
                message = null;
                return false;
            }

            if (nextTransportSequence == uint.MaxValue)
            {
                RequireFreshHandshake(nowSeconds, true);
                message = null;
                return false;
            }

            uint sequence = nextTransportSequence;
            if (!MotionTransportProtocol.TryFormatPose(command, sequence, out message) ||
                !IsSourceFresh(command, nowSeconds))
            {
                if (stopSentForInvalidState)
                    return false;

                message = MotionTransportProtocol.StopMessage;
                stopSentForInvalidState = true;
                awaitingStopConfirmation = true;
                nextStopRetryTimeSeconds = nowSeconds + StopRetryIntervalSeconds;
                nextSendTimeSeconds = nowSeconds + SendIntervalSeconds;
                return true;
            }

            nextTransportSequence++;
            stopSentForInvalidState = false;
            nextSendTimeSeconds = nowSeconds + SendIntervalSeconds;
            latestPoseSequence = sequence;
            latestPoseTimeSeconds = nowSeconds;
            return true;
        }


        private bool IsSourceFresh(
            MotionPoseCommand command,
            double nowSeconds)
        {
            if (!hasObservedSourceSequence)
            {
                hasObservedSourceSequence = true;
                lastObservedSourceSequence = command.SourceSequence;
                lastSourceAdvanceTimeSeconds = nowSeconds;
                return true;
            }

            if (command.SourceSequence > lastObservedSourceSequence)
            {
                lastObservedSourceSequence = command.SourceSequence;
                lastSourceAdvanceTimeSeconds = nowSeconds;
                return true;
            }

            return command.SourceSequence == lastObservedSourceSequence &&
                nowSeconds - lastSourceAdvanceTimeSeconds <=
                    SourceFreshnessTimeoutSeconds;
        }


        private void RequireFreshHandshake(
            double nowSeconds,
            bool resetTransportSequence)
        {
            firmwareReady = false;
            awaitingReadyAfterHello = false;
            awaitingStopConfirmation = false;
            stopSentForInvalidState = false;
            if (resetTransportSequence)
                nextTransportSequence = 1;
            ResetAcknowledgements();
            nextHandshakeTimeSeconds = nowSeconds;
        }

        private void ResetAcknowledgements()
        {
            firstSequenceInSession = nextTransportSequence;
            latestPoseSequence = 0;
            LastAcknowledgedSequence = 0;
            LastAcknowledgementLatencyMilliseconds = 0d;
        }
    }
}
