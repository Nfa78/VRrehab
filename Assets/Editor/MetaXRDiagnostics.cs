// Editor-only diagnostics for Meta XR Simulator + basic hand interaction setup.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MetaXRDiagnostics
{
    private const string MenuPath = "Tools/Meta XR/Run Diagnostics";

    [MenuItem(MenuPath)]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        Debug.Log($"[MetaXRDiagnostics] Scene: {scene.name}");

        LogPackage("com.meta.xr.sdk.core", "Meta XR Core SDK");
        LogPackage("com.meta.xr.sdk.interaction", "Meta XR Interaction SDK");
        LogPackage("com.meta.xr.sdk.interaction.ovr", "Meta XR Interaction SDK (OVR)");

        LogOpenXRSettings();
        LogSimulatorStatus();
        LogRigObjects();
        LogInteractorGroupNulls();
        LogXROriginProbe();
    }

    private static void LogPackage(string packageName, string label)
    {
        var pkg = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
            .FirstOrDefault(p => p.name == packageName);
        if (pkg != null)
            Debug.Log($"[MetaXRDiagnostics] OK: {label} installed ({pkg.version})");
        else
            Debug.LogWarning($"[MetaXRDiagnostics] MISSING: {label} package ({packageName})");
    }

    private static void LogOpenXRSettings()
    {
        try
        {
            // XR Management settings via reflection to avoid hard dependency
            var settingsType = Type.GetType(
                "UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
            if (settingsType == null)
            {
                Debug.LogWarning("[MetaXRDiagnostics] XR Management editor types not found. Is XR Management installed?");
                return;
            }

            var settings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (settings == null)
            {
                Debug.LogWarning("[MetaXRDiagnostics] XRGeneralSettingsPerBuildTarget.Instance is null.");
                return;
            }

            bool standaloneOpenXR = IsOpenXRLoaderEnabled(settings, BuildTargetGroup.Standalone);
            bool androidOpenXR = IsOpenXRLoaderEnabled(settings, BuildTargetGroup.Android);

            Debug.Log($"[MetaXRDiagnostics] OpenXR loader Standalone: {(standaloneOpenXR ? "ON" : "OFF")}");
            Debug.Log($"[MetaXRDiagnostics] OpenXR loader Android: {(androidOpenXR ? "ON" : "OFF")}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MetaXRDiagnostics] Failed to read OpenXR settings: {ex.Message}");
        }
    }

    private static bool IsOpenXRLoaderEnabled(object settings, BuildTargetGroup group)
    {
        // XRGeneralSettingsPerBuildTarget has GetSettingsForBuildTargetGroup(BuildTargetGroup)
        var getSettings = settings.GetType().GetMethod("GetSettingsForBuildTargetGroup");
        var generalSettings = getSettings?.Invoke(settings, new object[] { group });
        if (generalSettings == null)
            return false;

        var managerProp = generalSettings.GetType().GetProperty("AssignedSettings");
        var manager = managerProp?.GetValue(generalSettings);
        if (manager == null)
            return false;

        var loadersProp = manager.GetType().GetProperty("loaders");
        var loaders = loadersProp?.GetValue(manager) as IEnumerable<UnityEngine.Object>;
        if (loaders == null)
            return false;

        return loaders.Any(l => l != null && l.GetType().Name.Contains("OpenXR"));
    }

    private static void LogSimulatorStatus()
    {
        // Check XR runtime env vars set by Meta XR Simulator
        var runtime = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
        if (string.IsNullOrEmpty(runtime))
        {
            Debug.LogWarning("[MetaXRDiagnostics] XR_RUNTIME_JSON not set. Simulator likely not active.");
        }
        else
        {
            Debug.Log($"[MetaXRDiagnostics] XR_RUNTIME_JSON: {runtime}");
            Debug.Log(runtime.Contains("meta_openxr_simulator.json")
                ? "[MetaXRDiagnostics] OK: Meta XR Simulator runtime is selected."
                : "[MetaXRDiagnostics] WARNING: XR runtime is not Meta XR Simulator.");
        }
    }

    private static void LogRigObjects()
    {
        var all = UnityEngine.Object.FindObjectsOfType<GameObject>(true);

        bool hasOVRManager = all.Any(go => go.GetComponent("OVRManager") != null);
        bool hasOVRCameraRig = all.Any(go => go.GetComponent("OVRCameraRig") != null || go.name.Contains("OVRCameraRig"));
        bool hasInteractionRig = all.Any(go => go.name.Contains("OVRInteractionComprehensive") || go.name.Contains("Interaction"));

        Debug.Log($"[MetaXRDiagnostics] OVRManager present: {(hasOVRManager ? "YES" : "NO")}");
        Debug.Log($"[MetaXRDiagnostics] OVRCameraRig present: {(hasOVRCameraRig ? "YES" : "NO")}");
        Debug.Log($"[MetaXRDiagnostics] Interaction rig present (name contains 'Interaction'): {(hasInteractionRig ? "YES" : "NO")}");
    }

    private static void LogInteractorGroupNulls()
    {
        // Detect Meta Interaction InteractorGroup with nulls via reflection.
        var interactorGroupType = Type.GetType("Oculus.Interaction.InteractorGroup, Oculus.Interaction");
        if (interactorGroupType == null)
        {
            Debug.Log("[MetaXRDiagnostics] InteractorGroup type not found. Skipping interactor null check.");
            return;
        }

        var groups = UnityEngine.Object.FindObjectsOfType(interactorGroupType, true);
        int totalNulls = 0;

        foreach (var group in groups)
        {
            var field = interactorGroupType.GetField("_interactors", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = field?.GetValue(group) as System.Collections.IList;
            if (list == null)
                continue;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    totalNulls++;
                    var go = (group as Component)?.gameObject;
                    Debug.LogWarning($"[MetaXRDiagnostics] InteractorGroup '{go?.name}' has null at index {i}.");
                }
            }
        }

        if (totalNulls == 0)
            Debug.Log("[MetaXRDiagnostics] OK: No null entries in InteractorGroup lists.");
    }

    private static void LogXROriginProbe()
    {
        var probe = UnityEngine.Object.FindObjectOfType(Type.GetType("XROriginYawDebugProbe, Assembly-CSharp"), true);
        if (probe != null)
        {
            Debug.LogWarning("[MetaXRDiagnostics] XROriginYawDebugProbe exists. If you are using Meta rig only, consider disabling it.");
        }
    }
}
#endif
