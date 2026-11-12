using OceanMotion.Subproject05;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration
{
    [RequireComponent(typeof(IndividualIkSweepProbe))]
    public sealed class BoatPresentationOverlay : MonoBehaviour
    {
        private IndividualIkSweepProbe probe;
        private BoatIndividualIkSource boat;
        private Camera solveCamera;
        private RenderTexture solveImage;
        private Rect originalViewport;
        private Color originalBackground;
        private RenderTexture originalTarget;
        private GUISkin skin;
        private GUIStyle heading;
        private bool showSettings;
        private Vector2 settingsScroll, telemetryScroll;
        private static readonly WavePreset[] Presets = { WavePreset.Calm, WavePreset.Bow, WavePreset.Side, WavePreset.Crossed };

        private void OnEnable()
        {
            probe = GetComponent<IndividualIkSweepProbe>();
            boat = probe.boatSource;
            solveCamera = boat.preview.viewCamera;
            originalViewport = solveCamera.rect;
            originalBackground = solveCamera.backgroundColor;
            originalTarget = solveCamera.targetTexture;
            solveImage = new RenderTexture(640, 400, 24, RenderTextureFormat.ARGB32);
            solveCamera.targetTexture = solveImage;
            solveCamera.rect = new Rect(0, 0, 1, 1);
            solveCamera.backgroundColor = Color.clear;
            boat.preview.HasPresentationOverlay = true;
        }

        private void OnDisable()
        {
            if (boat && boat.preview) boat.preview.HasPresentationOverlay = false;
            if (solveCamera)
            {
                solveCamera.targetTexture = originalTarget;
                solveCamera.rect = originalViewport;
                solveCamera.backgroundColor = originalBackground;
            }
            solveImage.Release();
            Destroy(solveImage);
            if (skin) Destroy(skin);
        }

        private void OnGUI()
        {
            var previousSkin = GUI.skin;
            if (!skin)
            {
                skin = Instantiate(previousSkin);
                skin.label.fontSize = skin.button.fontSize = skin.textField.fontSize = 13;
                skin.label.wordWrap = true;
                heading = new GUIStyle(skin.label) { fontStyle = FontStyle.Bold };
            }
            GUI.skin = skin;
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Max(1, Screen.height / 900f);
            GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1));
            const float margin = 12, gap = 6;
            float width = Mathf.Min(310, Screen.width / scale - margin * 2);
            float available = Screen.height / scale - margin * 2 - gap * 2;
            float solveHeight = Mathf.Min(205, available * .30f);
            float telemetryHeight = Mathf.Min(124, available * .24f);
            float parameterHeight = Mathf.Min(showSettings ? 550 : probe.ShowOutputControls ? 270 : 156, available - telemetryHeight - solveHeight);
            var rect = new Rect(margin, margin, width, parameterHeight);
            BeginPanel(rect, "PARAMETERS");
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            GUI.enabled = !probe.WaitingForSimulationArm;
            GUILayout.BeginHorizontal();
            foreach (var preset in Presets)
                if (GUILayout.Button(preset.ToString())) boat.SelectWavePreset(preset);
            GUILayout.EndHorizontal();
            GUI.enabled = probe.CanConfigure && !probe.Preparing;
            if (GUILayout.Button("Calibrate boat neutral")) boat.CalibrateBoat();
            GUI.enabled = probe.SimulationMode || probe.CanStartSimulation;
            string startLabel = probe.ShowOutputControls ? "Start simulation + arm platform" : "Start simulation";
            if (GUILayout.Button(probe.SimulationMode ? "Stop simulation" : startLabel))
            {
                if (probe.SimulationMode) probe.StopSimulation();
                else probe.StartSimulation();
            }
            GUI.enabled = true;
            if (GUILayout.Button(showSettings ? "Hide additional settings" : "Additional settings"))
                showSettings = !showSettings;
            if (showSettings) probe.DrawBoatConnection();
            GUILayout.EndScrollView();
            probe.DrawBoatOutput();
            GUILayout.EndArea();

            rect.y = rect.yMax + gap;
            rect.height = telemetryHeight;
            BeginPanel(rect, "TELEMETRY");
            telemetryScroll = GUILayout.BeginScrollView(telemetryScroll);
            var t = boat.telemetry;
            if (t.IsCalibrated)
            {
                GUILayout.Label($"Pitch {t.Pitch:F1}°    Roll {t.Roll:F1}°");
                GUILayout.Label($"Yaw {t.Yaw:F1}°    Heave {t.Heave:F2} m");
            }
            else GUILayout.Label("Awaiting neutral calibration");
            GUILayout.Label($"{boat.waves.ActivePresetName} · {probe.WaterSpeedMultiplier:F2}×  •  {probe.Status}");
            if (!string.IsNullOrEmpty(probe.LastStop)) GUILayout.Label(probe.LastStop);
            if (!string.IsNullOrEmpty(probe.MappingError)) GUILayout.Label(probe.MappingError);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            rect.y = rect.yMax + gap;
            rect.height = solveHeight;
            BeginPanel(rect, "IK SOLVE DISPLAY");
            if (boat.preview.CurrentResult?.KinematicSolutionFound != true)
                GUILayout.Label(boat.preview.CurrentError ?? boat.SourceStatus);
            var image = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.DrawTexture(image, solveImage, ScaleMode.ScaleToFit, true);
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
            GUI.skin = previousSkin;
        }

        private void BeginPanel(Rect rect, string title)
        {
            var tint = GUI.color;
            GUI.color = new Color(.035f, .055f, .075f, .75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8));
            GUILayout.Label(title, heading);
        }
    }
}
