using UnityEngine;

namespace OceanMotion.Subproject05
{
[RequireComponent(typeof(Rigidbody))]
public class BoatBuoyancy : MonoBehaviour
{
    public Transform[] points;          // drag the 4 sample transforms in
    public float waterLevel = 0f;
    public float floatHeight = 1.0f;    // spring target depth
    public float springStrength = 0.1f;  // tune per stage below
    public float damping = 0.1f;

    [Header("Simple water resistance")]
[SerializeField] private float forwardWaterDrag = 100f;
[SerializeField] private float lateralWaterDrag = 300f;

    [SerializeField] private WaveField waveField;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        foreach (var p in points)
        {

        float surfaceY = waveField != null
        ? waveField.SampleHeight(p.position, Time.time)
        : waterLevel;
            float depth = surfaceY - p.position.y - floatHeight;
            if (depth <= 0) continue; // not submerged

            Vector3 pointVel = rb.GetPointVelocity(p.position);
            float force = depth * springStrength - pointVel.y * damping;
            rb.AddForceAtPosition(Vector3.up * force, p.position);

            // Added as a test for water drag
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            float forwardSpeed = Vector3.Dot(pointVel, forward);
            float lateralSpeed = Vector3.Dot(pointVel, right);

            Vector3 waterDragForce =
                -forward * forwardSpeed * forwardWaterDrag
                -right * lateralSpeed * lateralWaterDrag;

            rb.AddForceAtPosition(waterDragForce, p.position);
        }
    }
}
}
