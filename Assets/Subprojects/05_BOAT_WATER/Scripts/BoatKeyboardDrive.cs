using UnityEngine;
using UnityEngine.InputSystem;

namespace OceanMotion.Subproject05
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatKeyboardDrive : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference driveAction;

        [Header("Tuning")]
        [SerializeField] private float forwardForce = 1200f;
        [SerializeField] private float turnTorque = 450f;

        private Rigidbody rb;
        private Vector2 driveInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (driveAction != null)
            {
                driveAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (driveAction != null)
            {
                driveAction.action.Disable();
            }
        }

        private void Update()
        {
            if (driveAction != null)
            {
                driveInput = driveAction.action.ReadValue<Vector2>();
            }
            else
            {
                driveInput = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            // Y input: forward / reverse.
            rb.AddForce(transform.forward * driveInput.y * forwardForce);

            // X input: left / right steering around the boat's up axis.
            rb.AddTorque(transform.up * driveInput.x * turnTorque);
        }
    }
}