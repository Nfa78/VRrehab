// Editor-only diagnostics for Meta XR Simulator + basic hand interaction setup.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MetaXRDiagnostics
{
    private const string MenuPath = "Tools/Meta XR/Run Diagnostics";
    private const string XrGeneralSettingsAssetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string XrRuntimeJsonKey = "XR_RUNTIME_JSON";
    private const string XrSelectedRuntimeJsonKey = "XR_SELECTED_RUNTIME_JSON";

    [MenuItem(MenuPath)]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        Debug.Log($"[MetaXRDiagnostics] Scene: {scene.name}");
        Debug.Log($"[MetaXRDiagnostics] Active build target: {EditorUserBuildSettings.activeBuildTarget}");

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
            var settingsType = Type.GetType("UnityEngine.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management");
            if (settingsType == null)
            {
                Debug.LogWarning("[MetaXRDiagnostics] XR Management types not found. Is XR Management installed?");
                return;
            }

            var settings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            string source = "XRGeneralSettingsPerBuildTarget.Instance";

            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath(XrGeneralSettingsAssetPath, settingsType);
                source = XrGeneralSettingsAssetPath;
            }

            if (settings == null)
            {
                Debug.LogWarning("[MetaXRDiagnostics] XRGeneralSettingsPerBuildTarget could not be loaded from singleton or asset.");
                return;
            }

            Debug.Log($"[MetaXRDiagnostics] XR settings source: {source}");

            LogLoaderState(settings, BuildTargetGroup.Standalone);
            LogLoaderState(settings, BuildTargetGroup.Android);

            var activeGeneralSettings = GetGeneralSettingsForGroup(settings, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            if (activeGeneralSettings != null)
            {
                var assignedManager = GetAssignedSettings(activeGeneralSettings);
                if (assignedManager != null)
                {
                    var activeLoader = assignedManager.GetType().GetProperty("activeLoader")?.GetValue(assignedManager) as UnityEngine.Object;
                    Debug.Log($"[MetaXRDiagnostics] Active loader (play mode only): {(activeLoader != null ? activeLoader.name : "<none>")}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MetaXRDiagnostics] Failed to read OpenXR settings: {ex.Message}");
        }
    }

    private static void LogLoaderState(object settings, BuildTargetGroup group)
    {
        var generalSettings = GetGeneralSettingsForGroup(settings, group);
        if (generalSettings == null)
        {
            Debug.LogWarning($"[MetaXRDiagnostics] No XR general settings found for {group}.");
            return;
        }

        var manager = GetAssignedSettings(generalSettings);
        if (manager == null)
        {
            Debug.LogWarning($"[MetaXRDiagnostics] No XR manager assigned for {group}.");
            return;
        }

        var loadersProp = manager.GetType().GetProperty("loaders");
        var loaders = loadersProp?.GetValue(manager) as IEnumerable<UnityEngine.Object>;
        if (loaders == null)
        {
            Debug.LogWarning($"[MetaXRDiagnostics] Loader list unavailable for {group}.");
            return;
        }

        var loaderNames = loaders.Where(l => l != null).Select(l => l.name).ToArray();
        bool hasOpenXr = loaderNames.Any(name => name.IndexOf("OpenXR", StringComparison.OrdinalIgnoreCase) >= 0);
        bool hasSimulation = loaderNames.Any(name => name.IndexOf("Simulation", StringComparison.OrdinalIgnoreCase) >= 0);

        Debug.Log($"[MetaXRDiagnostics] {group} loaders: {(loaderNames.Length > 0 ? string.Join(", ", loaderNames) : "<none>")}");
        Debug.Log($"[MetaXRDiagnostics] {group} OpenXR loader: {(hasOpenXr ? "ON" : "OFF")}");

        if (group == BuildTargetGroup.Standalone && hasOpenXr && hasSimulation)
        {
            Debug.LogWarning("[MetaXRDiagnostics] Standalone has both OpenXR and Simulation loaders enabled. This can destabilize Meta XR Simulator startup.");
        }
    }

    private static object GetGeneralSettingsForGroup(object settings, BuildTargetGroup group)
    {
        var type = settings.GetType();
        var instanceMethod = type.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Instance);
        if (instanceMethod != null)
        {
            return instanceMethod.Invoke(settings, new object[] { group });
        }

        var staticMethod = type.GetMethod("XRGeneralSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Static);
        if (staticMethod != null)
        {
            return staticMethod.Invoke(null, new object[] { group });
        }

        return null;
    }

    private static object GetAssignedSettings(object generalSettings)
    {
        var type = generalSettings.GetType();
        return type.GetProperty("AssignedSettings")?.GetValue(generalSettings)
               ?? type.GetProperty("Manager")?.GetValue(generalSettings);
    }

    private static void LogSimulatorStatus()
    {
        var runtime = Environment.GetEnvironmentVariable(XrRuntimeJsonKey);
        var selectedRuntime = Environment.GetEnvironmentVariable(XrSelectedRuntimeJsonKey);

        Debug.Log($"[MetaXRDiagnostics] {XrRuntimeJsonKey}: {FormatEnvValue(runtime)}");
        Debug.Log($"[MetaXRDiagnostics] {XrSelectedRuntimeJsonKey}: {FormatEnvValue(selectedRuntime)}");

        if (string.IsNullOrEmpty(runtime))
        {
            Debug.LogWarning("[MetaXRDiagnostics] XR runtime env var is not set. Simulator likely not active.");
            return;
        }

        if (runtime.IndexOf("meta_openxr_simulator.json", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Debug.Log("[MetaXRDiagnostics] Meta XR Simulator runtime is selected.");
        }
        else
        {
            Debug.LogWarning("[MetaXRDiagnostics] XR runtime is not Meta XR Simulator.");
        }

        if (!string.Equals(runtime, selectedRuntime, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[MetaXRDiagnostics] XR runtime and selected runtime differ. Simulator activation may be stale.");
        }
    }

    private static string FormatEnvValue(string value)
        => string.IsNullOrEmpty(value) ? "<unset>" : value;

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
