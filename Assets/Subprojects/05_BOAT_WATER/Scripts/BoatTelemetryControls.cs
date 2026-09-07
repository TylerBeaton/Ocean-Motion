using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OceanMotion.Subproject05
{
    public class BoatTelemetryControls : MonoBehaviour
    {
        [Header("Controlled Systems")]
        [SerializeField] private BoatMotionTelemetry telemetry;
        [SerializeField] private WaveField waveField;

        [Header("UI Controls")]
        [SerializeField] private Button calibrateButton;
        [SerializeField] private TMP_Dropdown presetDropdown;

        private void OnEnable()
        {
            if (calibrateButton != null)
            {
                calibrateButton.onClick.AddListener(CalibrateNeutral);
            }

            if (presetDropdown != null)
            {
                presetDropdown.onValueChanged.AddListener(SelectPreset);
            }

            SynchronizePresetDropdown();
        }

        private void OnDisable()
        {
            if (calibrateButton != null)
            {
                calibrateButton.onClick.RemoveListener(CalibrateNeutral);
            }

            if (presetDropdown != null)
            {
                presetDropdown.onValueChanged.RemoveListener(SelectPreset);
            }
        }

        private void CalibrateNeutral()
        {
            if (telemetry != null)
            {
                telemetry.CalibrateNeutral();
            }
        }

        private void SelectPreset(int optionIndex)
        {
            if (waveField == null ||
                !System.Enum.IsDefined(typeof(WavePreset), optionIndex))
            {
                return;
            }

            waveField.SetPreset((WavePreset)optionIndex);
        }

        private void SynchronizePresetDropdown()
        {
            if (presetDropdown == null || waveField == null)
            {
                return;
            }

            presetDropdown.SetValueWithoutNotify(
                (int)waveField.ActivePreset);

            presetDropdown.RefreshShownValue();
        }
    }
}