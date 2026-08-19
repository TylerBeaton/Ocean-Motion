using UnityEngine;

public class SP03_04_PitchYaw : MonoBehaviour
{
    [SerializeField] private SerialController serialController;
    [SerializeField] private Transform pitchJoint;
    [SerializeField] private Transform yawJoint;
    [SerializeField] private float serialUpdatesPerSecond = 15f;

    [Header("Pitch calibration (local X)")]
    [SerializeField] private float pitchNeutralLocalDegrees;
    [SerializeField] private bool invertPitch;
    [SerializeField] private Vector2 pitchMechanicalLimits =
        new Vector2(-90f, 90f);
    [SerializeField] private Vector3 pitchServoCommands =
        new Vector3(0f, 90f, 180f);

    [Header("Yaw calibration (local Y)")]
    [SerializeField] private float yawNeutralLocalDegrees;
    [SerializeField] private bool invertYaw = true;
    [SerializeField] private Vector2 yawMechanicalLimits =
        new Vector2(-90f, 90f);
    [SerializeField] private Vector3 yawServoCommands =
        new Vector3(0f, 90f, 180f);

    private float nextSerialUpdate;
    private bool connected;
    private bool loggedFirstTelemetryMessage;

    private void Awake()
    {
        if (serialController != null)
        {
            serialController.SetTearDownFunction(StopArduinoData);
        }
        else
        {
            Debug.LogError(
                "SerialController has not been assigned.",
                this
            );
        }

        if (pitchJoint == null || yawJoint == null)
        {
            Debug.LogError(
                "PitchJoint and YawJoint must both be assigned.",
                this
            );
        }
        else if (!yawJoint.IsChildOf(pitchJoint))
        {
            Debug.LogError(
                "YawJoint must be a descendant of PitchJoint.",
                this
            );
        }
    }

    private void Update()
    {
        if (
            !connected ||
            pitchJoint == null ||
            yawJoint == null
        )
        {
            return;
        }

        // Unscaled time keeps serial updates running even when
        // Time.timeScale is zero.
        float serialTime = Time.unscaledTime;

        if (serialTime < nextSerialUpdate)
        {
            return;
        }

        SendJointCommandsToArduino();

        float safeUpdateRate =
            Mathf.Max(1f, serialUpdatesPerSecond);

        nextSerialUpdate =
            serialTime + (1f / safeUpdateRate);
    }

    private void SendJointCommandsToArduino()
    {
        int pitchCommand = GetServoCommand(
            pitchJoint.localEulerAngles.x,
            pitchNeutralLocalDegrees,
            invertPitch,
            pitchMechanicalLimits,
            pitchServoCommands
        );

        int yawCommand = GetServoCommand(
            yawJoint.localEulerAngles.y,
            yawNeutralLocalDegrees,
            invertYaw,
            yawMechanicalLimits,
            yawServoCommands
        );

        string message = $"SERVO,{pitchCommand},{yawCommand}";

        if (!loggedFirstTelemetryMessage)
        {
            Debug.Log("Unity → Arduino: " + message);
            loggedFirstTelemetryMessage = true;
        }

        serialController.SendLatestSerialMessage(message);
    }

    private static int GetServoCommand(
        float currentLocalDegrees,
        float neutralLocalDegrees,
        bool invert,
        Vector2 mechanicalLimits,
        Vector3 servoCommands
    )
    {
        float jointAngle = Mathf.DeltaAngle(
            neutralLocalDegrees,
            currentLocalDegrees
        );

        if (invert)
        {
            jointAngle = -jointAngle;
        }

        float minimumMechanicalAngle =
            Mathf.Min(mechanicalLimits.x, 0f);
        float maximumMechanicalAngle =
            Mathf.Max(mechanicalLimits.y, 0f);

        jointAngle = Mathf.Clamp(
            jointAngle,
            minimumMechanicalAngle,
            maximumMechanicalAngle
        );

        return ScaleJointToServoCommand(
            jointAngle,
            minimumMechanicalAngle,
            maximumMechanicalAngle,
            servoCommands
        );
    }

    private static int ScaleJointToServoCommand(
        float jointAngle,
        float minimumMechanicalAngle,
        float maximumMechanicalAngle,
        Vector3 servoCommands
    )
    {
        float command;

        if (jointAngle < 0f)
        {
            float negativeAmount = Mathf.InverseLerp(
                0f,
                minimumMechanicalAngle,
                jointAngle
            );
            command = Mathf.Lerp(
                servoCommands.y,
                servoCommands.x,
                negativeAmount
            );
        }
        else
        {
            float positiveAmount = Mathf.InverseLerp(
                0f,
                maximumMechanicalAngle,
                jointAngle
            );
            command = Mathf.Lerp(
                servoCommands.y,
                servoCommands.z,
                positiveAmount
            );
        }

        return Mathf.RoundToInt(Mathf.Clamp(command, 0f, 180f));
    }

    private void StopArduinoData()
    {
        if (serialController == null)
        {
            return;
        }

        Debug.Log("Unity → Arduino: STOP");
        serialController.SendSerialMessage("STOP");
    }

    private void OnApplicationQuit()
    {
        StopArduinoData();
    }

    public void OnMessageArrived(string message)
    {
        Debug.Log("Arduino → Unity: " + message);
    }

    public void OnConnectionEvent(bool isConnected)
    {
        connected = isConnected;
        nextSerialUpdate = 0f;
        loggedFirstTelemetryMessage = false;

        Debug.Log(
            isConnected
                ? "Arduino connected"
                : "Arduino disconnected"
        );
    }
}
