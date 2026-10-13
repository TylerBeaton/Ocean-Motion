using System;
using TMPro;
using UnityEngine;

namespace OceanMotion.FusionPlayback
{
    /// <summary>Shows each solved rotary pivot angle on a fixed screen-space label.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class StewartServoAngleLabels : MonoBehaviour
    {
        [Serializable]
        private sealed class ServoLabel
        {
            public Transform pivot;
            public TMP_Text label;
            [Tooltip("Hinge axis in the pivot's neutral local space. The SP07 prefab uses local -Y.")]
            public Vector3 localHingeAxis = Vector3.down;

            [NonSerialized] public Quaternion neutralLocalRotation;
        }

        [SerializeField] private ServoLabel[] servos = new ServoLabel[6];
        [SerializeField] private bool showRotationDiagnostic;

        private void Start()
        {
            foreach (ServoLabel servo in servos)
            {
                if (servo != null && servo.pivot != null)
                    servo.neutralLocalRotation = servo.pivot.localRotation;
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < servos.Length; i++)
            {
                ServoLabel servo = servos[i];
                if (servo == null || servo.label == null)
                    continue;

                if (servo.pivot == null || servo.localHingeAxis.sqrMagnitude < 0.0001f)
                {
                    servo.label.text = $"S{i + 1}: —";
                    continue;
                }

                Vector3 axis = servo.localHingeAxis.normalized;
                Vector3 reference = Vector3.ProjectOnPlane(Vector3.right, axis);
                if (reference.sqrMagnitude < 0.0001f)
                    reference = Vector3.ProjectOnPlane(Vector3.forward, axis);

                Quaternion delta = Quaternion.Inverse(servo.neutralLocalRotation) * servo.pivot.localRotation;
                float angle = Vector3.SignedAngle(reference, delta * reference, axis);
                servo.label.text = $"S{i + 1}: {angle:+0.0;-0.0;0.0}°";
                if (showRotationDiagnostic)
                {
                    Quaternion current = servo.pivot.localRotation;
                    Quaternion neutral = servo.neutralLocalRotation;
                    servo.label.text += $"  (joint Δ {Quaternion.Angle(neutral, current):0.0}°; " +
                        $"N {neutral.x:F3},{neutral.y:F3},{neutral.z:F3},{neutral.w:F3}; " +
                        $"C {current.x:F3},{current.y:F3},{current.z:F3},{current.w:F3})";
                }
            }
        }
    }
}
