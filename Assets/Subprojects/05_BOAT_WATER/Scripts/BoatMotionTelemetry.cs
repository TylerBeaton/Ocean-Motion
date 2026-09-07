using UnityEngine;

namespace OceanMotion.Subproject05
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatMotionTelemetry : MonoBehaviour
    {
        [Header("Calibration")]
        [SerializeField] private bool isCalibrated;
        [SerializeField] private Vector3 neutralPosition;
        [SerializeField] private Quaternion neutralRotation;

        [Header("Calibrated Motion")]
        [SerializeField] private float pitch;
        [SerializeField] private float roll;
        [SerializeField] private float heave;
        [SerializeField] private float yaw;

        [Header("Diagnostics")]
        [SerializeField] private int sequence;
        [SerializeField] private float simulationTime;
        [SerializeField] private Vector3 rawWorldPosition;
        [SerializeField] private Vector3 rawWorldEulerAngles;
        [SerializeField] private float speed;

        private Rigidbody boatRigidbody;

        public bool IsCalibrated => isCalibrated;
        public float Pitch => pitch;
        public float Roll => roll;
        public float Heave => heave;
        public float Yaw => yaw;
        public float Speed => speed;
        public int Sequence => sequence;
        public float SimulationTime => simulationTime;

        private void Awake()
        {
            boatRigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            rawWorldPosition = boatRigidbody.position;
            rawWorldEulerAngles = boatRigidbody.rotation.eulerAngles;
            speed = boatRigidbody.linearVelocity.magnitude;

            if (!isCalibrated)
            {
                return;
            }

            Quaternion relativeRotation =
                Quaternion.Inverse(neutralRotation) * boatRigidbody.rotation;

            Vector3 relativeEulerAngles = relativeRotation.eulerAngles;

            pitch = Mathf.DeltaAngle(0f, relativeEulerAngles.x);
            yaw = Mathf.DeltaAngle(0f, relativeEulerAngles.y);
            roll = Mathf.DeltaAngle(0f, relativeEulerAngles.z);
            heave = boatRigidbody.position.y - neutralPosition.y;

            simulationTime = Time.time;
            sequence++;
        }

        [ContextMenu("Calibrate Neutral")]
        public void CalibrateNeutral()
        {
            neutralPosition = boatRigidbody.position;
            neutralRotation = boatRigidbody.rotation;

            pitch = 0f;
            roll = 0f;
            heave = 0f;
            yaw = 0f;
            sequence = 0;
            simulationTime = Time.time;
            isCalibrated = true;
        }
    }
}