using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// A desired platform pose after architecture-neutral conditioning.
    /// It is not an actuator command and contains no inverse kinematics.
    /// </summary>
    public readonly struct MotionPoseCommand
    {
        public Vector3 TranslationMeters { get; }
        public Vector3 RotationDegrees { get; }
        public ulong SourceSequence { get; }
        public double SourceTimeSeconds { get; }
        public bool IsValid { get; }
        public MotionLimitFlags LimitFlags { get; }

        public MotionPoseCommand(
            Vector3 translationMeters,
            Vector3 rotationDegrees,
            ulong sourceSequence,
            double sourceTimeSeconds,
            bool isValid,
            MotionLimitFlags limitFlags = MotionLimitFlags.None)
        {
            TranslationMeters = translationMeters;
            RotationDegrees = rotationDegrees;
            SourceSequence = sourceSequence;
            SourceTimeSeconds = sourceTimeSeconds;
            IsValid = isValid;
            LimitFlags = limitFlags;
        }
    }
}
