using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine;

public class TextUpdater : MonoBehaviour
{
    public TMP_Text messageText; // Assign this in Inspector!

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        if (LocalizationSettings.InitializationOperation.IsDone)
        {
            OnLocaleChanged(LocalizationSettings.SelectedLocale);
        }
        else
        {
            LocalizationSettings.InitializationOperation.Completed += (op) => OnLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged += UpdateText;
        }
    }

    private void OnLocaleChanged(Locale locale)
    {
        UpdateText();
    }

    public void UpdateText()
    {
        if (messageText == null)
        {
            return;
        }
        if (PointsManager.Instance == null)
        {
            Debug.LogError("PointsManager.Instance is null. Ensure PointsManager is set up properly.");
            return;
        }

        if (LocalizationSettings.SelectedLocale == LocalizationSettings.AvailableLocales.GetLocale("it"))
        {
            messageText.SetText("Punti: " + PointsManager.Instance.Points.ToString());
        }
        else
        {
            messageText.SetText("Points: " + PointsManager.Instance.Points.ToString());
        }
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged -= UpdateText;
        }
    }
}
