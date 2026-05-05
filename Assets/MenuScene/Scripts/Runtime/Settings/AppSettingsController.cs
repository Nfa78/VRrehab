using System;
using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    public class AppSettingsController : MonoBehaviour
    {
        [SerializeField] private AppSettingsPanelView view;
        [SerializeField] private string volumePlayerPrefsKey = "MenuScene.AppSettings.MasterVolume";
        [SerializeField] [Range(0f, 1f)] private float defaultVolume = 1f;

        private AppSettingsModel settings = new AppSettingsModel();

        public event Action<AppSettingsModel> SettingsChanged;

        public AppSettingsPanelView View
        {
            get { return view; }
        }

        public AppSettingsModel CurrentSettings
        {
            get { return settings; }
        }

        private void Awake()
        {
            if (view != null)
            {
                view.VolumeChanged += HandleVolumeChanged;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (view != null)
            {
                view.VolumeChanged -= HandleVolumeChanged;
            }
        }

        public void Initialize()
        {
            settings.masterVolume = PlayerPrefs.GetFloat(volumePlayerPrefsKey, defaultVolume);
            ApplySettings();
        }

        public AppSettingsModel CreateSnapshot()
        {
            return settings != null ? settings.Clone() : new AppSettingsModel();
        }

        public bool HasRequiredReferences(out string message)
        {
            if (view == null)
            {
                message = "App settings view is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleVolumeChanged(float volume)
        {
            settings.masterVolume = Mathf.Clamp01(volume);
            ApplySettings();
            PlayerPrefs.SetFloat(volumePlayerPrefsKey, settings.masterVolume);
            PlayerPrefs.Save();

            if (SettingsChanged != null)
            {
                SettingsChanged.Invoke(CreateSnapshot());
            }
        }

        private void ApplySettings()
        {
            settings.masterVolume = Mathf.Clamp01(settings.masterVolume);
            AudioListener.volume = settings.masterVolume;

            if (view != null)
            {
                view.SetVolume(settings.masterVolume);
            }
        }
    }
}
