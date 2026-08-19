using System.Globalization;
using UnityEngine;

public class SP03_04_PitchYaw : MonoBehaviour
{
    [SerializeField] private SerialController serialController;
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private float serialUpdatesPerSecond = 15f;

    private float nextSerialUpdate;
    private bool connected;
    private bool loggedFirstTelemetryMessage;

    private void Awake()
    {
        if (sourceTransform == null)
        {
            sourceTransform = transform;
        }

        if (serialController != null)
        {
            serialController.SetTearDownFunction(StopArduinoData);
        }
        else
        {
            Debug.LogError(
                "SerialController has not been assigned."
            );
        }
    }

    private void Update()
    {
        // Unscaled time keeps serial updates running even when
        // Time.timeScale is zero.
        float serialTime = Time.unscaledTime;

        if (
            connected &&
            serialTime >= nextSerialUpdate
        )
        {
            SendPitchYawToArduino(sourceTransform.eulerAngles);

            float safeUpdateRate =
                Mathf.Max(1f, serialUpdatesPerSecond);

            nextSerialUpdate =
                serialTime + (1f / safeUpdateRate);
        }
    }

    private void SendPitchYawToArduino(Vector3 eulerAngles)
    {
        string pitch = eulerAngles.x.ToString(
            "F3",
            CultureInfo.InvariantCulture
        );
        string yaw = eulerAngles.y.ToString(
            "F3",
            CultureInfo.InvariantCulture
        );
        string roll = eulerAngles.z.ToString(
            "F3",
            CultureInfo.InvariantCulture
        );

        // Keep the established ROT packet for compatibility. Pitch is X,
        // yaw is Y, and roll remains available as Z for diagnostics.
        string message = $"ROT,{pitch},{yaw},{roll}";

        if (!loggedFirstTelemetryMessage)
        {
            Debug.Log("Unity → Arduino: " + message);
            loggedFirstTelemetryMessage = true;
        }

        serialController.SendLatestSerialMessage(message);
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
