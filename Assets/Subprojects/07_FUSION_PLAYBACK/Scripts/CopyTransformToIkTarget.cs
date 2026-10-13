using OceanMotion.Subproject05;
using OceanMotion.Subproject06;
using UnityEngine;

namespace OceanMotion.FusionPlayback
{
    /// <summary>Maps a raw transform or conditioned pipeline pose onto an IK target.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class CopyTransformToIkTarget : MonoBehaviour
    {
        [SerializeField] private Transform source;
        [SerializeField] private Transform target;
        [SerializeField] private bool useProcessedCommand;
        [SerializeField] private MotionPipelineController controller;
        [SerializeField] private bool autoStartAfterCalibration;

        private BoatMotionTelemetry telemetry;
        private bool awaitingCalibratedCommand;
        private ulong calibrationSourceSequence;
        private Vector3 sourceNeutralPosition;
        private Quaternion sourceNeutralRotation;
        private Vector3 targetNeutralPosition;
        private Quaternion targetNeutralRotation;

        public bool IsCopying { get; private set; }

        private void OnEnable()
        {
            // The controller and its source telemetry live on the same Boat.
            telemetry = controller != null ? controller.GetComponent<BoatMotionTelemetry>() : null;
            if (telemetry != null)
                telemetry.NeutralCalibrated += OnNeutralCalibrated;
        }

        private void OnNeutralCalibrated()
        {
            if (!autoStartAfterCalibration)
                return;

            // Never copy an old command across a new neutral calibration.
            IsCopying = false;
            calibrationSourceSequence = controller != null
                ? controller.LatestCommand.SourceSequence : 0;
            awaitingCalibratedCommand = true;
        }

        [ContextMenu("Calibrate Neutral And Start Copying")]
        public void CalibrateAndStartCopying()
        {
            awaitingCalibratedCommand = false;
            IsCopying = false;
            if (target == null || (useProcessedCommand
                ? controller == null || !controller.isActiveAndEnabled ||
                  !controller.LatestCommand.IsValid
                : source == null || source == target))
            {
                Debug.LogWarning("Assign a target and a valid, active source or processed controller before starting.", this);
                return;
            }

            if (!useProcessedCommand)
            {
                sourceNeutralPosition = source.position;
                sourceNeutralRotation = source.rotation;
            }
            targetNeutralPosition = target.position;
            targetNeutralRotation = target.rotation;
            IsCopying = true;
        }

        [ContextMenu("Stop Copying")]
        public void StopCopying()
        {
            awaitingCalibratedCommand = false;
            IsCopying = false;
        }

        private void OnDisable()
        {
            if (telemetry != null)
                telemetry.NeutralCalibrated -= OnNeutralCalibrated;
            telemetry = null;
            StopCopying();
        }

        private void LateUpdate()
        {
            if (awaitingCalibratedCommand && autoStartAfterCalibration && controller != null &&
                controller.isActiveAndEnabled && controller.LatestCommand.IsValid &&
                controller.LatestCommand.SourceSequence > calibrationSourceSequence)
                CalibrateAndStartCopying();

            if (!IsCopying || target == null)
                return;

            if (useProcessedCommand)
            {
                if (controller == null || !controller.isActiveAndEnabled)
                    return;

                MotionPoseCommand command = controller.LatestCommand;
                if (!command.IsValid)
                    return;

                // Pipeline values are already neutral-relative and bounded in pose space.
                target.SetPositionAndRotation(
                    targetNeutralPosition + targetNeutralRotation * command.TranslationMeters,
                    targetNeutralRotation * Quaternion.Euler(command.RotationDegrees));
                return;
            }

            if (source == null || source == target)
                return;

            Quaternion relativeRotation = Quaternion.Inverse(sourceNeutralRotation) * source.rotation;
            Vector3 relativePosition = Quaternion.Inverse(sourceNeutralRotation) *
                (source.position - sourceNeutralPosition);
            target.SetPositionAndRotation(
                targetNeutralPosition + targetNeutralRotation * relativePosition,
                targetNeutralRotation * relativeRotation);
        }
    }
}
