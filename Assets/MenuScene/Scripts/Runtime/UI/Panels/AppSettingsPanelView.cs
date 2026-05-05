using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class AppSettingsPanelView : MonoBehaviour
    {
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeValueLabel;

        public event Action<float> VolumeChanged;

        private void Awake()
        {
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            }
        }

        private void OnDestroy()
        {
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
            }
        }

        public void SetVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(clamped);
            }

            UpdateValueLabel(clamped);
        }

        public bool HasRequiredReferences(out string message)
        {
            if (volumeSlider == null)
            {
                message = "Volume slider is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleVolumeChanged(float volume)
        {
            UpdateValueLabel(volume);
            if (VolumeChanged != null)
            {
                VolumeChanged.Invoke(volume);
            }
        }

        private void UpdateValueLabel(float volume)
        {
            if (volumeValueLabel != null)
            {
                volumeValueLabel.text = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
            }
        }
    }
}
