using UnityEngine;

public class UserPolling_ReadWrite_RGB : MonoBehaviour
{
    [SerializeField] private SerialController serialController;
    [SerializeField] private Material material;

    [SerializeField] private float cycleDuration = 5f;
    [SerializeField] private float serialUpdatesPerSecond = 5f;

    private float nextSerialUpdate;
    private bool connected;

    private void Awake()
    {
        if (serialController != null)
        {
            serialController.SetTearDownFunction(
                TurnOffArduinoLed
            );
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
        float safeCycleDuration =
            Mathf.Max(0.01f, cycleDuration);

        float hue = Mathf.Repeat(
            Time.time / safeCycleDuration,
            1f
        );

        Color color = Color.HSVToRGB(hue, 1f, 1f);

        if (material != null)
        {
            material.SetColor("_BaseColor", color);
        }

        // Unscaled time keeps the Arduino heartbeat running
        // even when Time.timeScale is zero.
        float serialTime = Time.unscaledTime;

        if (
            connected &&
            serialTime >= nextSerialUpdate
        )
        {
            SendColorToArduino(color);

            float safeUpdateRate =
                Mathf.Max(1f, serialUpdatesPerSecond);

            nextSerialUpdate =
                serialTime + (1f / safeUpdateRate);
        }
    }

    private void SendColorToArduino(Color color)
    {
        int red = Mathf.RoundToInt(color.r * 255f);
        int green = Mathf.RoundToInt(color.g * 255f);
        int blue = Mathf.RoundToInt(color.b * 255f);

        string message =
            $"RGB,{red},{green},{blue}";

        serialController.SendSerialMessage(message);
    }

    private void TurnOffArduinoLed()
    {
        if (serialController == null)
        {
            return;
        }

        Debug.Log("Unity → Arduino: OFF");
        serialController.SendSerialMessage("OFF");
    }

    private void OnApplicationQuit()
    {
        TurnOffArduinoLed();
    }

    public void OnMessageArrived(string message)
    {
        Debug.Log("Arduino → Unity: " + message);
    }

    public void OnConnectionEvent(bool isConnected)
    {
        connected = isConnected;
        nextSerialUpdate = 0f;

        Debug.Log(
            isConnected
                ? "Arduino connected"
                : "Arduino disconnected"
        );
    }
}