// Auto-create Localization Settings if missing to prevent runtime exceptions.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;

[InitializeOnLoad]
static class LocalizationSettingsAutoCreate
{
    const string k_SettingsFolder = "Assets/Localization";
    const string k_SettingsPath = "Assets/Localization/LocalizationSettings.asset";

    static LocalizationSettingsAutoCreate()
    {
        EditorApplication.delayCall += EnsureLocalizationSettings;
    }

    static void EnsureLocalizationSettings()
    {
        if (LocalizationSettings.HasSettings)
            return;

        if (!AssetDatabase.IsValidFolder(k_SettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Localization");
        }

        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(k_SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "Default Localization Settings";
            AssetDatabase.CreateAsset(settings, k_SettingsPath);
            AssetDatabase.SaveAssets();
        }

        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
    }
}
#endif
