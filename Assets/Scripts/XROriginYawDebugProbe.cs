using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class XROriginYawDebugProbe : MonoBehaviour
{
    const float MinYawDeltaDegrees = 0.05f;
    const float LogCooldownSeconds = 0.2f;

    Transform target;
    float lastYaw;
    float lastLogTime = -999f;
    bool hasLastYaw;
    bool loggedSetup;
    bool loggedNoTargetYet;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var existing = FindObjectsOfType<XROriginYawDebugProbe>(true);
        if (existing != null && existing.Length > 0)
            return;

        var go = new GameObject(nameof(XROriginYawDebugProbe));
        DontDestroyOnLoad(go);
        go.AddComponent<XROriginYawDebugProbe>();
    }

    void Start()
    {
        StartCoroutine(FindTargetRoutine());
    }

    IEnumerator FindTargetRoutine()
    {
        while (target == null)
        {
            target = FindLikelyXrOriginTransform(transform);
            if (target != null)
            {
                hasLastYaw = false;
                loggedSetup = false;
                loggedNoTargetYet = false;
                yield break;
            }

            if (!loggedNoTargetYet)
            {
                loggedNoTargetYet = true;
                Debug.LogWarning("[XROriginYawDebugProbe] No XR Origin found yet. Waiting for scene objects to initialize.");
            }

            yield return null;
        }
    }

    void FixedUpdate()
    {
        Probe("FixedUpdate");
    }

    void Update()
    {
        Probe("Update");
    }

    void LateUpdate()
    {
        Probe("LateUpdate");
    }

    void Probe(string phase)
    {
        if (target == null)
            return;

        if (!loggedSetup)
        {
            loggedSetup = true;
            Debug.Log($"[XROriginYawDebugProbe] Tracking target: {GetPath(target)}", target);
            var suspects = GetRotationSuspects(target);
            if (suspects.Count > 0)
                Debug.Log($"[XROriginYawDebugProbe] Rotation-related components on target: {string.Join(", ", suspects)}", target);
        }

        var currentYaw = NormalizeYaw(target.eulerAngles.y);
        if (!hasLastYaw)
        {
            lastYaw = currentYaw;
            hasLastYaw = true;
            return;
        }

        var delta = Mathf.DeltaAngle(lastYaw, currentYaw);
        var absDelta = Mathf.Abs(delta);

        if (absDelta >= MinYawDeltaDegrees && (Time.unscaledTime - lastLogTime) >= LogCooldownSeconds)
        {
            lastLogTime = Time.unscaledTime;
            Debug.LogWarning(
                $"[XROriginYawDebugProbe] Yaw delta {delta:F3} deg detected in {phase}. " +
                $"Prev={lastYaw:F3}, Curr={currentYaw:F3}, Frame={Time.frameCount}",
                target);
        }

        lastYaw = currentYaw;
    }

    static Transform FindLikelyXrOriginTransform(Transform self)
    {
        // Best effort: pick the actual Unity XR Origin component first.
        var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in allBehaviours)
        {
            if (mb == null || mb.transform == null || mb.transform == self)
                continue;

            var fullName = mb.GetType().FullName ?? string.Empty;
            if (string.Equals(fullName, "Unity.XR.CoreUtils.XROrigin", StringComparison.Ordinal))
                return mb.transform;
        }

        // Fallback by component type name while explicitly excluding this debug probe.
        foreach (var mb in allBehaviours)
        {
            if (mb == null || mb.transform == null || mb.transform == self)
                continue;

            var typeName = mb.GetType().Name;
            if (string.Equals(typeName, nameof(XROriginYawDebugProbe), StringComparison.Ordinal))
                continue;

            if (typeName.IndexOf("XROrigin", StringComparison.OrdinalIgnoreCase) >= 0)
                return mb.transform;
        }

        // Final fallback by object name.
        var allTransforms = FindObjectsOfType<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == null || t == self)
                continue;

            if (string.Equals(t.name, nameof(XROriginYawDebugProbe), StringComparison.Ordinal))
                continue;

            if (t.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("XR Rig", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return t;
            }
        }

        return null;
    }

    static List<string> GetRotationSuspects(Transform root)
    {
        var result = new List<string>();
        var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in allBehaviours)
        {
            if (mb == null || mb.transform != root)
                continue;

            var n = mb.GetType().Name;
            if (n.IndexOf("Turn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Locomotion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Simulator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Add(n);
            }
        }

        return result.Distinct().ToList();
    }

    static string GetPath(Transform t)
    {
        if (t == null)
            return "<null>";

        var path = t.name;
        var current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw < 0f)
            yaw += 360f;
        return yaw;
    }
}
