using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// An immutable snapshot of calibrated source motion, before conditioning.
    /// This is a data type, not an attachable Unity component.
    /// </summary>
    public readonly struct MotionPoseSample
    {
        /// <summary>
        /// Neutral-relative translation in metres in Unity's left-handed frame
        /// (+X right, +Y up, +Z forward). Currently only Y (heave) is populated.
        /// </summary>
        public Vector3 TranslationMeters { get; }

        /// <summary>
        /// Signed neutral-relative angles in degrees: X=pitch, Y=yaw, Z=roll.
        /// Preserves BoatMotionTelemetry's quaternion-relative Unity Euler
        /// decomposition (Z-X-Y order); these are not raw world Euler angles.
        /// </summary>
        public Vector3 RotationDegrees { get; }

        /// <summary>
        /// Increasing sample ID within a source session. The adapter must own
        /// this counter because SP05 resets its counter on recalibration.
        /// </summary>
        public ulong Sequence { get; }

        /// <summary>
        /// Source simulation time in seconds, not wall-clock or controller time.
        /// </summary>
        public double SourceTimeSeconds { get; }

        /// <summary>
        /// Producer-confirmed calibration and finite values. This record only
        /// stores the flag; consumers must still validate freshness and ordering.
        /// A default-constructed sample is invalid.
        /// </summary>
        public bool IsValid { get; }

        public MotionPoseSample(
            Vector3 translationMeters,
            Vector3 rotationDegrees,
            ulong sequence,
            double sourceTimeSeconds,
            bool isValid)
        {
            TranslationMeters = translationMeters;
            RotationDegrees = rotationDegrees;
            Sequence = sequence;
            SourceTimeSeconds = sourceTimeSeconds;
            IsValid = isValid;
        }
    }
}
