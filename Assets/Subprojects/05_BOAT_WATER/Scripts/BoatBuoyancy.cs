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

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        foreach (var p in points)
        {
            float depth = waterLevel - p.position.y;
            if (depth <= 0) continue; // not submerged

            Vector3 pointVel = rb.GetPointVelocity(p.position);
            float force = depth * springStrength - pointVel.y * damping;
            rb.AddForceAtPosition(Vector3.up * force, p.position);
        }
    }
}
}
