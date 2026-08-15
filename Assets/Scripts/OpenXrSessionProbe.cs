using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

[DefaultExecutionOrder(-10000)]
public sealed class OpenXrSessionProbe : MonoBehaviour
{
    [SerializeField] private float probeDurationSeconds = 10f;
    [SerializeField] private int logEveryNFrames = 30;
    [SerializeField] private bool logOnlyOnStateChange = true;

    private readonly List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();
    private static readonly MethodInfo OpenXrInstanceProcAddrMethod = typeof(OVRPlugin).GetMethod(
        "GetOpenXRInstanceProcAddrFunc",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    private static bool _loggedProcAddrUnavailable;
    private string _lastSnapshot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var existing = FindObjectOfType<OpenXrSessionProbe>();
        if (existing != null)
        {
            return;
        }

        var gameObject = new GameObject(nameof(OpenXrSessionProbe))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<OpenXrSessionProbe>();
    }

    private IEnumerator Start()
    {
        Debug.Log("[OpenXrSessionProbe] Probe started.");

        float endTime = Time.realtimeSinceStartup + probeDurationSeconds;
        int frame = 0;

        while (Time.realtimeSinceStartup < endTime)
        {
            frame++;
            string snapshot = BuildSnapshot(frame);
            bool shouldLog = !logOnlyOnStateChange || !string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal);

            if (!shouldLog && frame % Mathf.Max(1, logEveryNFrames) == 0)
            {
                shouldLog = true;
            }

            if (shouldLog)
            {
                Debug.Log(snapshot, this);
                _lastSnapshot = snapshot;
            }

            yield return null;
        }

        Debug.Log("[OpenXrSessionProbe] Probe finished.", this);
    }

    private string BuildSnapshot(int frame)
    {
        var snapshot = new Snapshot
        {
            Frame = frame,
            TimeSinceStartup = Time.realtimeSinceStartup,
            RuntimeEnv = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON"),
            SelectedRuntimeEnv = Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON"),
            ActiveLoader = GetActiveLoaderName(),
            AssignedLoaders = GetAssignedLoaderNames(),
            RunningDisplayCount = GetRunningDisplayCount(),
            OvrInitialized = SafeGetOvrInitialized(),
            XrInstance = SafeGetNativeOpenXrInstance(),
            XrSession = SafeGetNativeOpenXrSession(),
            ProcAddr = SafeGetOpenXrProcAddr()
        };

        return snapshot.ToLogString();
    }

    private int GetRunningDisplayCount()
    {
        SubsystemManager.GetSubsystems(_displaySubsystems);
        int count = 0;
        for (int i = 0; i < _displaySubsystems.Count; i++)
        {
            if (_displaySubsystems[i] != null && _displaySubsystems[i].running)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetActiveLoaderName()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        return manager != null && manager.activeLoader != null ? manager.activeLoader.name : "<none>";
    }

    private static string GetAssignedLoaderNames()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null || manager.loaders == null || manager.loaders.Count == 0)
        {
            return "<none>";
        }

        var names = new StringBuilder();
        for (int i = 0; i < manager.loaders.Count; i++)
        {
            if (i > 0)
            {
                names.Append(", ");
            }

            names.Append(manager.loaders[i] != null ? manager.loaders[i].name : "<null>");
        }

        return names.ToString();
    }

    private static bool SafeGetOvrInitialized()
    {
        try
        {
            return OVRPlugin.initialized;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenXrSessionProbe] Failed to query OVRPlugin.initialized: {e.Message}");
            return false;
        }
    }

    private static ulong SafeGetNativeOpenXrInstance()
    {
        try
        {
            return OVRPlugin.GetNativeOpenXRInstance();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenXrSessionProbe] Failed to query OpenXR instance: {e.Message}");
            return 0;
        }
    }

    private static ulong SafeGetNativeOpenXrSession()
    {
        try
        {
            return OVRPlugin.GetNativeOpenXRSession();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenXrSessionProbe] Failed to query OpenXR session: {e.Message}");
            return 0;
        }
    }

    private static IntPtr SafeGetOpenXrProcAddr()
    {
        try
        {
            if (OpenXrInstanceProcAddrMethod == null)
            {
                if (!_loggedProcAddrUnavailable)
                {
                    Debug.LogWarning("[OpenXrSessionProbe] OVRPlugin.GetOpenXRInstanceProcAddrFunc is unavailable in this SDK wrapper.");
                    _loggedProcAddrUnavailable = true;
                }

                return IntPtr.Zero;
            }

            object value = OpenXrInstanceProcAddrMethod.Invoke(null, null);
            return value is IntPtr procAddr ? procAddr : IntPtr.Zero;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenXrSessionProbe] Failed to query OpenXR proc addr: {e.Message}");
            return IntPtr.Zero;
        }
    }

    private struct Snapshot
    {
        public int Frame;
        public float TimeSinceStartup;
        public string RuntimeEnv;
        public string SelectedRuntimeEnv;
        public string ActiveLoader;
        public string AssignedLoaders;
        public int RunningDisplayCount;
        public bool OvrInitialized;
        public ulong XrInstance;
        public ulong XrSession;
        public IntPtr ProcAddr;

        public string ToLogString()
        {
            return $"[OpenXrSessionProbe] frame={Frame} t={TimeSinceStartup:F2}s " +
                   $"runtime={Format(RuntimeEnv)} selected={Format(SelectedRuntimeEnv)} " +
                   $"activeLoader={ActiveLoader} assignedLoaders={AssignedLoaders} " +
                   $"runningDisplays={RunningDisplayCount} ovrInitialized={OvrInitialized} " +
                   $"xrInstance={XrInstance} xrSession={XrSession} procAddr={ProcAddr}";
        }

        private static string Format(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unset>" : value;
        }
    }
}
