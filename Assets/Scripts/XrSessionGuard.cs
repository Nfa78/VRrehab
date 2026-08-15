using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

[DisallowMultipleComponent]
public class XrSessionGuard : MonoBehaviour
{
    [Header("Recovery")]
    [SerializeField] private bool autoRecoverOnInvalidSession = true;
    [SerializeField] private int maxRecoveryAttempts = 2;
    [SerializeField] private float waitBeforeCheckSeconds = 1.5f;
    [SerializeField] private float waitBetweenAttemptsSeconds = 1.5f;

    [Header("Gate While Invalid")]
    [SerializeField] private List<Behaviour> disableUntilReady = new List<Behaviour>();
    [SerializeField] private bool logDebug = true;

    private readonly List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();

    private IEnumerator Start()
    {
        SetTargetsEnabled(false);
        yield return new WaitForSeconds(waitBeforeCheckSeconds);

        int attempt = 0;
        while (!IsSessionValid() && attempt <= maxRecoveryAttempts)
        {
            if (logDebug)
            {
                Debug.LogWarning($"[XrSessionGuard] XR session invalid. Recovery attempt {attempt + 1}/{maxRecoveryAttempts + 1}.", this);
            }

            if (autoRecoverOnInvalidSession)
            {
                yield return RestartXrSubsystems();
                yield return new WaitForSeconds(waitBetweenAttemptsSeconds);
            }
            else
            {
                break;
            }

            attempt++;
        }

        bool ready = IsSessionValid();
        SetTargetsEnabled(ready);

        if (logDebug)
        {
            Debug.Log(ready
                ? "[XrSessionGuard] XR session valid. Interaction scripts enabled."
                : "[XrSessionGuard] XR session still invalid. Keep simulator/link active and retry Play.", this);
        }
    }

    private bool IsSessionValid()
    {
        SubsystemManager.GetSubsystems(_displaySubsystems);
        for (int i = 0; i < _displaySubsystems.Count; i++)
        {
            XRDisplaySubsystem display = _displaySubsystems[i];
            if (display != null && display.running)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator RestartXrSubsystems()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null)
        {
            yield break;
        }

        manager.StopSubsystems();
        manager.DeinitializeLoader();
        yield return null;

        yield return manager.InitializeLoader();
        if (manager.activeLoader != null)
        {
            manager.StartSubsystems();
        }
    }

    private void SetTargetsEnabled(bool enabled)
    {
        for (int i = 0; i < disableUntilReady.Count; i++)
        {
            Behaviour b = disableUntilReady[i];
            if (b != null)
            {
                b.enabled = enabled;
            }
        }
    }
}
