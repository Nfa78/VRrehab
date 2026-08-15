using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class SeedGateSequence : MonoBehaviour
{
    [Header("Rings")]
    [SerializeField] private List<SeedGate> orderedGates = new List<SeedGate>();
    [SerializeField] private List<TSSeedGate> orderedTsGates = new List<TSSeedGate>();
    [SerializeField] private bool showGatesOnlyWhenObjectiveIsActive = true;
    [SerializeField] private bool disableGateCollidersWhenHidden = true;

    [Header("Objective")]
    [SerializeField] private SeedsTaskDriver taskDriver;
    [SerializeField] private bool completeObjectiveOnSuccess;
    [SerializeField] private bool addProgressOnSuccess = true;
    [SerializeField] private float successProgressDelta = 1f;

    [Header("Sequence")]
    [SerializeField] private float maxSecondsBetweenGates = 2f;
    [SerializeField] private bool resetSequenceOnWrongGate = true;
    [SerializeField] private bool uniqueGatePerThrow = true;

    private readonly List<Renderer> gateRenderers = new List<Renderer>();
    private readonly List<Collider> gateColliders = new List<Collider>();
    private int nextGateIndex;
    private int activeThrowId = -1;
    private float lastGatePassTime = -999f;
    private bool gatesVisible = true;
    private bool UseTsGates => orderedTsGates != null && orderedTsGates.Count > 0;

    private void OnEnable()
    {
        ResolveReferences();
        CacheGateObjects();
        Subscribe();
        ResetSequence();
        RefreshGateVisibility(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        RefreshGateVisibility(false);

        if (gatesVisible &&
            nextGateIndex > 0 &&
            Time.time - lastGatePassTime > maxSecondsBetweenGates)
        {
            ResetSequence();
        }
    }

    private void ResolveReferences()
    {
        if (taskDriver == null)
        {
            taskDriver = FindFirstObjectByType<SeedsTaskDriver>();
        }
    }

    private void Subscribe()
    {
        if (UseTsGates)
        {
            for (int i = 0; i < orderedTsGates.Count; i++)
            {
                TSSeedGate gate = orderedTsGates[i];
                if (gate == null)
                {
                    continue;
                }

                gate.SeedPassed -= HandleTsGatePassed;
                gate.SeedPassed += HandleTsGatePassed;
            }
            return;
        }

        for (int i = 0; i < orderedGates.Count; i++)
        {
            SeedGate gate = orderedGates[i];
            if (gate == null)
            {
                continue;
            }

            gate.SeedPassed -= HandleGatePassed;
            gate.SeedPassed += HandleGatePassed;
        }
    }

    private void Unsubscribe()
    {
        if (UseTsGates)
        {
            for (int i = 0; i < orderedTsGates.Count; i++)
            {
                TSSeedGate gate = orderedTsGates[i];
                if (gate != null)
                {
                    gate.SeedPassed -= HandleTsGatePassed;
                }
            }
            return;
        }

        for (int i = 0; i < orderedGates.Count; i++)
        {
            SeedGate gate = orderedGates[i];
            if (gate != null)
            {
                gate.SeedPassed -= HandleGatePassed;
            }
        }
    }

    private void HandleGatePassed(SeedGate gate, SeedProjectileMarker marker)
    {
        HandleGatePassedInternal(gate, marker);
    }

    private void HandleTsGatePassed(TSSeedGate gate, SeedProjectileMarker marker)
    {
        HandleGatePassedInternal(gate, marker);
    }

    private void HandleGatePassedInternal(Component gate, SeedProjectileMarker marker)
    {
        if (!gatesVisible || gate == null || marker == null || GetActiveGateCount() == 0)
        {
            return;
        }

        if (nextGateIndex == 0)
        {
            activeThrowId = marker.ThrowId;
        }
        else if (uniqueGatePerThrow && marker.ThrowId != activeThrowId)
        {
            return;
        }

        if (!IsExpectedGate(gate))
        {
            if (resetSequenceOnWrongGate)
            {
                ResetSequence();
            }

            return;
        }

        nextGateIndex++;
        lastGatePassTime = Time.time;

        if (nextGateIndex < GetActiveGateCount())
        {
            return;
        }

        ReportSuccess();
        ResetSequence();
    }

    private void ReportSuccess()
    {
        if (taskDriver == null)
        {
            return;
        }

        if (completeObjectiveOnSuccess)
        {
            taskDriver.CompleteThrowStep();
        }
        else if (addProgressOnSuccess)
        {
            taskDriver.HandleThrowSuccess(successProgressDelta);
        }
    }

    private void ResetSequence()
    {
        nextGateIndex = 0;
        activeThrowId = -1;
        lastGatePassTime = -999f;
    }

    private void RefreshGateVisibility(bool force)
    {
        bool shouldShow = !showGatesOnlyWhenObjectiveIsActive || IsTargetObjectiveActive();
        if (!force && shouldShow == gatesVisible)
        {
            return;
        }

        gatesVisible = shouldShow;
        SetRenderersEnabled(shouldShow);

        if (disableGateCollidersWhenHidden)
        {
            SetCollidersEnabled(shouldShow);
        }

        if (!shouldShow)
        {
            ResetSequence();
        }
    }

    private bool IsTargetObjectiveActive()
    {
        if (taskDriver == null)
        {
            return false;
        }

        return taskDriver.IsThrowStepActive();
    }

    private void CacheGateObjects()
    {
        gateRenderers.Clear();
        gateColliders.Clear();

        if (UseTsGates)
        {
            for (int i = 0; i < orderedTsGates.Count; i++)
            {
                TSSeedGate gate = orderedTsGates[i];
                if (gate == null)
                {
                    continue;
                }

                gateRenderers.AddRange(gate.GetComponentsInChildren<Renderer>(true));
                gateColliders.AddRange(gate.GetComponentsInChildren<Collider>(true));
            }
            return;
        }

        for (int i = 0; i < orderedGates.Count; i++)
        {
            SeedGate gate = orderedGates[i];
            if (gate == null)
            {
                continue;
            }

            gateRenderers.AddRange(gate.GetComponentsInChildren<Renderer>(true));
            gateColliders.AddRange(gate.GetComponentsInChildren<Collider>(true));
        }
    }

    private int GetActiveGateCount()
    {
        return UseTsGates ? orderedTsGates.Count : orderedGates.Count;
    }

    private bool IsExpectedGate(Component gate)
    {
        if (UseTsGates)
        {
            return nextGateIndex >= 0 &&
                   nextGateIndex < orderedTsGates.Count &&
                   orderedTsGates[nextGateIndex] == gate as TSSeedGate;
        }

        return nextGateIndex >= 0 &&
               nextGateIndex < orderedGates.Count &&
               orderedGates[nextGateIndex] == gate as SeedGate;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < gateRenderers.Count; i++)
        {
            if (gateRenderers[i] != null)
            {
                gateRenderers[i].enabled = enabled;
            }
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < gateColliders.Count; i++)
        {
            if (gateColliders[i] != null)
            {
                gateColliders[i].enabled = enabled;
            }
        }
    }
}
