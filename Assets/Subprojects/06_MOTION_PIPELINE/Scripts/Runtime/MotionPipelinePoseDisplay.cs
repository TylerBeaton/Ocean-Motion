using UnityEngine;

namespace OceanMotion.Subproject06
{
    /// <summary>
    /// Drives two display-only transforms from raw and processed motion so the
    /// pipeline can be compared visually. It never changes the source boat.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class MotionPipelinePoseDisplay : MonoBehaviour
    {
        [Header("Pipeline")]
        [SerializeField] private BoatTelemetryPoseSource source;
        [SerializeField] private MotionPipelineController controller;

        [Header("Display-Only Proxies")]
        [SerializeField] private Transform rawPose;
        [SerializeField] private Transform processedPose;

        [Header("Shared Visual Exaggeration")]
        [SerializeField, Min(0f)] private float translationDisplayScale = 1f;
        [SerializeField, Min(0f)] private float rotationDisplayScale = 1f;

        private Vector3 rawNeutralPosition;
        private Quaternion rawNeutralRotation;
        private Vector3 processedNeutralPosition;
        private Quaternion processedNeutralRotation;
        private bool isInitialized;

        private void Reset()
        {
            source = FindFirstObjectByType<BoatTelemetryPoseSource>();
            controller = FindFirstObjectByType<MotionPipelineController>();
        }

        private void Awake()
        {
            CaptureNeutralPose();
        }

        [ContextMenu("Capture Display Neutral")]
        public void CaptureNeutralPose()
        {
            isInitialized = HasSafeReferences();
            if (!isInitialized)
            {
                Debug.LogError(
                    "Pose display requires two distinct proxy transforms that " +
                    "are outside the source boat hierarchy.",
                    this);
                return;
            }

            rawNeutralPosition = rawPose.localPosition;
            rawNeutralRotation = rawPose.localRotation;
            processedNeutralPosition = processedPose.localPosition;
            processedNeutralRotation = processedPose.localRotation;
        }

        private void LateUpdate()
        {
            if (!isInitialized)
                return;

            MotionPoseSample raw = source.LatestSample;
            if (raw.IsValid)
            {
                ApplyPose(
                    rawPose,
                    rawNeutralPosition,
                    rawNeutralRotation,
                    raw.TranslationMeters,
                    raw.RotationDegrees);
            }
            else
            {
                ResetPose(rawPose, rawNeutralPosition, rawNeutralRotation);
            }

            MotionPoseCommand processed = controller.LatestCommand;
            if (processed.IsValid)
            {
                ApplyPose(
                    processedPose,
                    processedNeutralPosition,
                    processedNeutralRotation,
                    processed.TranslationMeters,
                    processed.RotationDegrees);
            }
            else
            {
                ResetPose(
                    processedPose,
                    processedNeutralPosition,
                    processedNeutralRotation);
            }
        }

        private void OnDisable()
        {
            if (!isInitialized)
                return;

            ResetPose(rawPose, rawNeutralPosition, rawNeutralRotation);
            ResetPose(processedPose, processedNeutralPosition, processedNeutralRotation);
        }

        private bool HasSafeReferences()
        {
            if (source == null || controller == null ||
                rawPose == null || processedPose == null)
                return false;

            Transform boat = source.transform;
            return !AreHierarchyRelated(rawPose, processedPose) &&
                !AreHierarchyRelated(rawPose, boat) &&
                !AreHierarchyRelated(processedPose, boat);
        }

        private static bool AreHierarchyRelated(Transform first, Transform second)
        {
            return first == second || first.IsChildOf(second) || second.IsChildOf(first);
        }

        private void ApplyPose(
            Transform target,
            Vector3 neutralPosition,
            Quaternion neutralRotation,
            Vector3 translationMeters,
            Vector3 rotationDegrees)
        {
            float translationScale = SafeScale(translationDisplayScale);
            float rotationScale = SafeScale(rotationDisplayScale);
            target.localPosition = neutralPosition + translationMeters * translationScale;
            target.localRotation = neutralRotation * Quaternion.Euler(
                rotationDegrees * rotationScale);
        }

        private static void ResetPose(
            Transform target,
            Vector3 neutralPosition,
            Quaternion neutralRotation)
        {
            target.SetLocalPositionAndRotation(neutralPosition, neutralRotation);
        }

        private static float SafeScale(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
