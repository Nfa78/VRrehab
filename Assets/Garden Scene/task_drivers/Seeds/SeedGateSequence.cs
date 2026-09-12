using System;
using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class SeedGateSequence : MonoBehaviour
{
    public event Action<int, int> GateSequenceProgressed;
    public event Action<int> ThrowSequenceSucceeded;
    public event Action ThrowSequenceWrongGateRejected;
    public event Action<string, int, int> ThrowSequenceReset;

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
    [SerializeField][Min(0)] private int activeGateCountOverride;

    private float committedThrowProgress;
    private readonly List<Renderer> gateRenderers = new List<Renderer>();
    private readonly List<Collider> gateColliders = new List<Collider>();
    private int nextGateIndex;
    private int activeThrowId = -1;
    private float lastGatePassTime = -999f;
    private bool gatesVisible = true;
    private bool UseTsGates => orderedTsGates != null && orderedTsGates.Count > 0;

    public int ActiveGateCount => GetActiveGateCount();

    private void OnEnable()
    {
        ResolveReferences();
        CacheGateObjects();
        Subscribe();
        ResetSequence("enable");
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
            ResetSequence("timeout", syncTaskProgress: true);
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
            ThrowSequenceWrongGateRejected?.Invoke();
            if (resetSequenceOnWrongGate)
            {
                ResetSequence("wrong_gate", syncTaskProgress: true);
            }

            return;
        }

        nextGateIndex++;
        lastGatePassTime = Time.time;
        GateSequenceProgressed?.Invoke(nextGateIndex, GetActiveGateCount());

        if (nextGateIndex < GetActiveGateCount())
        {
            SyncThrowObjectiveProgress();
            return;
        }

        ReportSuccess();
        ResetSequence("success", syncTaskProgress: false);
    }

    public void ApplyDifficulty(int activeGateCount, bool shouldResetSequenceOnWrongGate, float gateRadiusScale)
    {
        activeGateCountOverride = Mathf.Max(0, activeGateCount);
        resetSequenceOnWrongGate = shouldResetSequenceOnWrongGate;
        ApplyGateRadiusScale(gateRadiusScale);
        ResetSequence("difficulty", syncTaskProgress: true);
        RefreshGateVisibility(true);
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
            committedThrowProgress += Mathf.Max(0f, successProgressDelta);
            taskDriver.SetThrowProgress(committedThrowProgress);
            committedThrowProgress = ResolveCommittedThrowProgress(taskDriver.GetThrowProgressValue());
        }

        ThrowSequenceSucceeded?.Invoke(GetActiveGateCount());
    }

    private void ResetSequence(string reason, bool syncTaskProgress = false)
    {
        int previousGateDepth = nextGateIndex;
        int activeGateCount = GetActiveGateCount();
        nextGateIndex = 0;
        activeThrowId = -1;
        lastGatePassTime = -999f;

        if (syncTaskProgress)
        {
            SyncThrowObjectiveProgress();
        }

        ThrowSequenceReset?.Invoke(reason ?? string.Empty, previousGateDepth, activeGateCount);
    }

    private void RefreshGateVisibility(bool force)
    {
        bool shouldShow = !showGatesOnlyWhenObjectiveIsActive || IsTargetObjectiveActive();
        if (!force && shouldShow == gatesVisible)
        {
            return;
        }

        gatesVisible = shouldShow;
        SetGateObjectsEnabled(shouldShow);

        if (shouldShow)
        {
            SyncCommittedThrowProgressFromTask();
        }

        if (!shouldShow)
        {
            ResetSequence("objective_hidden", syncTaskProgress: false);
            committedThrowProgress = 0f;
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
        int totalGateCount = UseTsGates ? orderedTsGates.Count : orderedGates.Count;
        if (activeGateCountOverride <= 0)
        {
            return totalGateCount;
        }

        return Mathf.Min(activeGateCountOverride, totalGateCount);
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

    private void SetGateObjectsEnabled(bool visible)
    {
        int activeGateCount = GetActiveGateCount();

        if (UseTsGates)
        {
            for (int i = 0; i < orderedTsGates.Count; i++)
            {
                SetGateComponentObjectsEnabled(orderedTsGates[i], visible && i < activeGateCount);
            }
            return;
        }

        for (int i = 0; i < orderedGates.Count; i++)
        {
            SetGateComponentObjectsEnabled(orderedGates[i], visible && i < activeGateCount);
        }
    }

    private void SetGateComponentObjectsEnabled(Component gate, bool enabled)
    {
        if (gate == null)
        {
            return;
        }

        Renderer[] renderers = gate.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabled;
            }
        }

        if (!disableGateCollidersWhenHidden)
        {
            return;
        }

        Collider[] colliders = gate.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enabled;
            }
        }
    }

    private void ApplyGateRadiusScale(float gateRadiusScale)
    {
        float scale = Mathf.Max(0.01f, gateRadiusScale);

        for (int i = 0; i < orderedTsGates.Count; i++)
        {
            if (orderedTsGates[i] != null)
            {
                orderedTsGates[i].ApplyDifficultyRadiusScale(scale);
            }
        }

        for (int i = 0; i < orderedGates.Count; i++)
        {
            if (orderedGates[i] != null)
            {
                orderedGates[i].ApplyDifficultyRadiusScale(scale);
            }
        }
    }

    private void SyncCommittedThrowProgressFromTask()
    {
        if (taskDriver == null)
        {
            committedThrowProgress = 0f;
            return;
        }

        committedThrowProgress = ResolveCommittedThrowProgress(taskDriver.GetThrowProgressValue());
    }

    private void SyncThrowObjectiveProgress()
    {
        if (taskDriver == null || !taskDriver.IsThrowStepActive() || !addProgressOnSuccess)
        {
            return;
        }

        int activeGateCount = GetActiveGateCount();
        if (activeGateCount <= 0)
        {
            taskDriver.SetThrowProgress(committedThrowProgress);
            return;
        }

        float normalizedSequenceProgress = Mathf.Clamp01((float)nextGateIndex / activeGateCount);
        float totalProgress = committedThrowProgress + normalizedSequenceProgress * Mathf.Max(0f, successProgressDelta);
        taskDriver.SetThrowProgress(totalProgress);
    }

    private float ResolveCommittedThrowProgress(float currentProgress)
    {
        float progressDelta = Mathf.Max(0.0001f, successProgressDelta);
        return Mathf.Floor(Mathf.Max(0f, currentProgress) / progressDelta) * progressDelta;
    }
}
