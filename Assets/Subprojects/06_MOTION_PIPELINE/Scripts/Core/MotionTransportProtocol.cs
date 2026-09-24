using System.Globalization;

namespace OceanMotion.Subproject06
{
    public static class MotionTransportProtocol
    {
        public const string Version = "OM1";
        public const string HelloMessage = Version + ",HELLO";
        public const string ReadyMessage = Version + ",READY";
        public const string StopMessage = Version + ",STOP";
        public const string StoppedMessage = Version + ",STOPPED";
        public const string WatchdogMessage = Version + ",WATCHDOG";
        public const string ErrorPrefix = Version + ",ERR,";
        public const float MaximumHeaveMeters = 0.25f;
        public const float MaximumPitchDegrees = 70f;
        public const float MaximumYawDegrees = 5f;
        public const float MaximumRollDegrees = 70f;

        public static bool TryFormatPose(
            MotionPoseCommand command,
            uint transportSequence,
            out string packet)
        {
            if (transportSequence == 0 || command.SourceSequence == 0 ||
                !command.IsValid ||
                !IsFinite(command.TranslationMeters.y) ||
                !IsFinite(command.RotationDegrees.x) ||
                !IsFinite(command.RotationDegrees.y) ||
                !IsFinite(command.RotationDegrees.z) ||
                System.Math.Abs(command.TranslationMeters.y) > MaximumHeaveMeters ||
                System.Math.Abs(command.RotationDegrees.x) > MaximumPitchDegrees ||
                System.Math.Abs(command.RotationDegrees.y) > MaximumYawDegrees ||
                System.Math.Abs(command.RotationDegrees.z) > MaximumRollDegrees)
            {
                packet = null;
                return false;
            }

            packet = string.Format(
                CultureInfo.InvariantCulture,
                "{0},POSE,{1},{2},{3:F5},{4:F5},{5:F5},{6:F5}",
                Version,
                transportSequence,
                command.SourceSequence,
                command.TranslationMeters.y,
                command.RotationDegrees.x,
                command.RotationDegrees.y,
                command.RotationDegrees.z);
            return true;
        }

        public static bool TryParseAcknowledgement(
            string message,
            out uint transportSequence)
        {
            transportSequence = 0;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            string[] fields = message.Trim().Split(',');
            return fields.Length == 3 &&
                fields[0] == Version &&
                fields[1] == "ACK" &&
                uint.TryParse(
                    fields[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out transportSequence);
        }


        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
