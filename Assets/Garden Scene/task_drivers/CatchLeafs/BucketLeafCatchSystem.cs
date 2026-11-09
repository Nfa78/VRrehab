using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using SoundSystem;
using TaskSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BucketLeafCatchSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grabbable bucketGrabbable;
    [SerializeField] private GrabInteractable bucketGrabInteractable;
    [SerializeField] private HandGrabInteractable handGrabInteractable;
    [SerializeField] private CatchLeafsTaskDriver taskDriver;
    [SerializeField, Min(0f)] private float returnInteractionBufferSeconds = 0.5f;
    [SerializeField] private Transform leavesRoot;
    public GameObject DetectionSphere;
    [SerializeField] private string legacyLeafTaskId = "rake_leaves";

    [Header("Catch Volume")]
    [SerializeField] private Vector3 catchVolumeCenterLocal = new Vector3(0f, 0.17f, 0f);
    [SerializeField] private Vector3 catchVolumeSizeLocal = new Vector3(0.42f, 0.2f, 0.42f);

    [Header("Leaves")]
    [SerializeField][Min(0f)] private float leafRespawnDelaySeconds = 0.35f;
    [SerializeField] private bool autoFindLeavesRootByName = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly List<LeafRuntime> leaves = new List<LeafRuntime>();
    private bool wereLeavesVisible;
    private bool wasGrabbedLastFrame;
    private float returnInteractionLockUntil;
    private bool? bucketGrabComponentsEnabled;
    private string lastLoggedTaskId;
    private Renderer[] detectionSphereRenderers;
    private Color[] detectionSphereDefaultColors;
    private float catchFeedbackRemaining;
    private const float CatchFeedbackDurationSeconds = 1f;
    private static readonly Color CatchFeedbackColor = new Color(0.1f, 1f, 0.2f, 1f);

    public event Action<bool> BucketGrabStateChanged;
    public event Action<LeafsFallingEffect, bool> LeafCatchProcessed;
    public event Action<LeafsFallingEffect> LeafMissed;
    public event Action<LeafsFallingEffect> LeafRespawned;

    public Transform BucketTransform => transform;
    public bool IsBucketGrabbed => IsCurrentlyGrabbed();
    public int ConfiguredLeafCount => leaves.Count;

    public void ApplyDifficulty(
        float newLeafRespawnDelaySeconds,
        float newLeafFallSpeed,
        float newLeafSwayAmount,
        float newLeafDepthAmount)
    {
        leafRespawnDelaySeconds = Mathf.Max(0f, newLeafRespawnDelaySeconds);
        CacheLeaves();

        for (int i = 0; i < leaves.Count; i++)
        {
            LeafRuntime leaf = leaves[i];
            if (leaf != null && leaf.Effect != null)
            {
                leaf.Effect.ApplyCatchDifficulty(newLeafFallSpeed, newLeafSwayAmount, newLeafDepthAmount);
            }
        }
    }

    private void Awake()
    {
        ResolveReferences();
        CacheLeaves();
        ResolveDetectionSphere();
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
        SetDetectionSphereActive(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheLeaves();
        ResolveDetectionSphere();
        SubscribeToLeafEvents();
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
        SetDetectionSphereActive(false);
        wasGrabbedLastFrame = false;
        wereLeavesVisible = false;
    }

    private void OnDisable()
    {
        UnsubscribeFromLeafEvents();
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
        wasGrabbedLastFrame = false;
    }

    private void Update()
    {
        ResolveReferences();
        ResolveDetectionSphere();
        TickCatchFeedback();

        if (taskDriver != null && taskDriver.IsReturnStepCompleted())
        {
            if (returnInteractionLockUntil <= 0f)
            {
                returnInteractionLockUntil = Time.time + Mathf.Max(0f, returnInteractionBufferSeconds);
            }

            SetBucketGrabComponentsEnabled(false);
            wasGrabbedLastFrame = false;
            return;
        }

        if (taskDriver == null || !taskDriver.IsTaskActive(taskDriver.TaskId))
        {
            returnInteractionLockUntil = 0f;
        }

        if (returnInteractionLockUntil > 0f && Time.time < returnInteractionLockUntil)
        {
            SetBucketGrabComponentsEnabled(false);
            wasGrabbedLastFrame = false;
            return;
        }

        returnInteractionLockUntil = 0f;
        bool bucketCatchTaskActive = taskDriver != null && taskDriver.AllowsBucketCatchFlow();
        bool pickupStepActive = taskDriver != null && taskDriver.IsPickupStepActive();
        bool catchStepActive = taskDriver != null && taskDriver.IsCatchStepActive();
        bool legacyLeafTaskActive = taskDriver != null && taskDriver.IsTaskActive(legacyLeafTaskId);
        SetBucketGrabComponentsEnabled(bucketCatchTaskActive);
        LogTaskGateState(bucketCatchTaskActive);
        SetDetectionSphereActive(catchStepActive);

        bool leavesShouldBeVisible = catchStepActive || legacyLeafTaskActive;
        if (leavesShouldBeVisible != wereLeavesVisible)
        {
            SetLeavesActive(leavesShouldBeVisible, true);
            wereLeavesVisible = leavesShouldBeVisible;
        }

        if (!bucketCatchTaskActive)
        {
            wasGrabbedLastFrame = false;
            return;
        }

        bool isGrabbed = IsCurrentlyGrabbed();
        if (isGrabbed != wasGrabbedLastFrame)
        {
            BucketGrabStateChanged?.Invoke(isGrabbed);
            if (isGrabbed)
            {
                EventSoundManager.TryPlayAt("garden.bucket.grabbed", transform.position);
            }
        }

        if (pickupStepActive && isGrabbed && !wasGrabbedLastFrame)
        {
            bool completed = taskDriver.CompletePickupStep();
            if (logDebug)
            {
                Debug.Log(
                    $"[BucketLeafCatchSystem] Bucket grab detected. Pick step completion {(completed ? "succeeded" : "was rejected")}.",
                    this);
            }
        }

        wasGrabbedLastFrame = isGrabbed;
        if (!catchStepActive)
        {
            return;
        }

        UpdateLeafRespawns();
        ProcessLeafCatches();
    }

    private void ResolveReferences()
    {
        if (bucketGrabbable == null)
        {
            bucketGrabbable = GetComponent<Grabbable>();
        }

        if (bucketGrabInteractable == null)
        {
            bucketGrabInteractable = GetComponent<GrabInteractable>();
        }

        if (handGrabInteractable == null)
        {
            handGrabInteractable = GetComponent<HandGrabInteractable>() ??
                                  GetComponentInChildren<HandGrabInteractable>(true);
        }

        if (taskDriver == null)
        {
            taskDriver = FindFirstObjectByType<CatchLeafsTaskDriver>();
        }

        if (leavesRoot == null && autoFindLeavesRootByName)
        {
            GameObject leavesRootObject = GameObject.Find("Leafs");
            if (leavesRootObject != null)
            {
                leavesRoot = leavesRootObject.transform;
            }
        }
    }

    private void SetBucketGrabComponentsEnabled(bool enabled)
    {
        if (bucketGrabComponentsEnabled.HasValue && bucketGrabComponentsEnabled.Value == enabled)
        {
            return;
        }

        bucketGrabComponentsEnabled = enabled;

        if (bucketGrabbable != null)
        {
            bucketGrabbable.enabled = enabled;
        }

        if (bucketGrabInteractable != null)
        {
            bucketGrabInteractable.enabled = enabled;
        }

        if (handGrabInteractable != null)
        {
            handGrabInteractable.enabled = enabled;
        }

        if (!enabled)
        {
            wasGrabbedLastFrame = false;
        }

        if (logDebug)
        {
            Debug.Log(
                $"[BucketLeafCatchSystem] Bucket grab components {(enabled ? "enabled" : "disabled")} for task '{(taskDriver != null ? taskDriver.TaskId : "<none>")}'.",
                this);
        }
    }

    private void ResolveDetectionSphere()
    {
        if (DetectionSphere == null)
        {
            Transform found = transform.Find("DetectionRadius");
            if (found == null)
            {
                GameObject namedObject = GameObject.Find("DetectionRadius");
                found = namedObject != null ? namedObject.transform : null;
            }

            DetectionSphere = found != null ? found.gameObject : null;
        }

        if (DetectionSphere == null || detectionSphereRenderers != null)
        {
            return;
        }

        detectionSphereRenderers = DetectionSphere.GetComponentsInChildren<Renderer>(true);
        detectionSphereDefaultColors = new Color[detectionSphereRenderers.Length];
        for (int i = 0; i < detectionSphereRenderers.Length; i++)
        {
            detectionSphereDefaultColors[i] = detectionSphereRenderers[i] != null
                ? detectionSphereRenderers[i].material.color
                : Color.white;
        }
    }

    private void SetDetectionSphereActive(bool active)
    {
        if (DetectionSphere == null)
        {
            return;
        }

        if (DetectionSphere.activeSelf != active)
        {
            DetectionSphere.SetActive(active);
        }

        if (!active)
        {
            catchFeedbackRemaining = 0f;
            RestoreDetectionSphereColors();
        }
    }

    private void StartCatchFeedback()
    {
        catchFeedbackRemaining = CatchFeedbackDurationSeconds;
    }

    private void TickCatchFeedback()
    {
        if (catchFeedbackRemaining <= 0f || DetectionSphere == null || !DetectionSphere.activeSelf)
        {
            return;
        }

        catchFeedbackRemaining = Mathf.Max(0f, catchFeedbackRemaining - Time.deltaTime);
        float pulse = Mathf.PingPong((CatchFeedbackDurationSeconds - catchFeedbackRemaining) * 8f, 1f);
        for (int i = 0; i < detectionSphereRenderers.Length; i++)
        {
            Renderer renderer = detectionSphereRenderers[i];
            if (renderer != null)
            {
                renderer.material.color = Color.Lerp(detectionSphereDefaultColors[i], CatchFeedbackColor, pulse);
            }
        }

        if (catchFeedbackRemaining <= 0f)
        {
            RestoreDetectionSphereColors();
        }
    }

    private void RestoreDetectionSphereColors()
    {
        if (detectionSphereRenderers == null || detectionSphereDefaultColors == null)
        {
            return;
        }

        for (int i = 0; i < detectionSphereRenderers.Length; i++)
        {
            if (detectionSphereRenderers[i] != null)
            {
                detectionSphereRenderers[i].material.color = detectionSphereDefaultColors[i];
            }
        }
    }

    private void LogTaskGateState(bool bucketCatchTaskActive)
    {
        if (!logDebug)
        {
            return;
        }

        string taskId = taskDriver != null ? taskDriver.TaskId : "<none>";
        if (taskId == lastLoggedTaskId)
        {
            return;
        }

        lastLoggedTaskId = taskId;
        Debug.Log(
            $"[BucketLeafCatchSystem] Task={taskId}, catchTaskActive={bucketCatchTaskActive}, " +
            $"Grabbable={(bucketGrabbable != null && bucketGrabbable.enabled)}, " +
            $"GrabInteractable={(bucketGrabInteractable != null && bucketGrabInteractable.enabled)}, " +
            $"HandGrabInteractable={(handGrabInteractable != null && handGrabInteractable.enabled)}.",
            this);
    }

    private void CacheLeaves()
    {
        if (leaves.Count > 0 || leavesRoot == null)
        {
            return;
        }

        LeafsFallingEffect[] leafEffects = leavesRoot.GetComponentsInChildren<LeafsFallingEffect>(true);
        for (int i = 0; i < leafEffects.Length; i++)
        {
            if (leafEffects[i] != null)
            {
                leaves.Add(new LeafRuntime(leafEffects[i]));
                leafEffects[i].FellPastResetThreshold -= HandleLeafFellPastThreshold;
                leafEffects[i].FellPastResetThreshold += HandleLeafFellPastThreshold;
            }
        }
    }

    private void SetLeavesActive(bool active, bool resetPose)
    {
        for (int i = 0; i < leaves.Count; i++)
        {
            LeafRuntime leaf = leaves[i];
            if (leaf.Effect == null)
            {
                continue;
            }

            leaf.ResumeAtTime = 0f;
            leaf.Effect.SetSimulationActive(active, resetPose);
        }
    }

    private void UpdateLeafRespawns()
    {
        for (int i = 0; i < leaves.Count; i++)
        {
            LeafRuntime leaf = leaves[i];
            if (leaf.Effect == null || leaf.Effect.IsSimulationActive || leaf.ResumeAtTime <= 0f)
            {
                continue;
            }

            if (Time.time < leaf.ResumeAtTime)
            {
                continue;
            }

            leaf.ResumeAtTime = 0f;
            leaf.Effect.SetSimulationActive(true, true);
            LeafRespawned?.Invoke(leaf.Effect);
        }
    }

    private void ProcessLeafCatches()
    {
        Vector3 halfExtents = catchVolumeSizeLocal * 0.5f;
        for (int i = 0; i < leaves.Count; i++)
        {
            LeafRuntime leaf = leaves[i];
            if (leaf.Effect == null || !leaf.Effect.IsSimulationActive)
            {
                continue;
            }

            Vector3 localPoint = transform.InverseTransformPoint(leaf.Effect.transform.position);
            Vector3 relativePoint = localPoint - catchVolumeCenterLocal;
            if (Mathf.Abs(relativePoint.x) > halfExtents.x ||
                Mathf.Abs(relativePoint.y) > halfExtents.y ||
                Mathf.Abs(relativePoint.z) > halfExtents.z)
            {
                continue;
            }

            bool counted = taskDriver != null && taskDriver.CatchLeaf();
            LeafCatchProcessed?.Invoke(leaf.Effect, counted);
        if (counted)
        {
            EventSoundManager.TryPlayAt("garden.leaf.caught", leaf.Effect.transform.position);
        }
            leaf.Effect.SetSimulationActive(false, true);
            leaf.ResumeAtTime = Time.time + Mathf.Max(0f, leafRespawnDelaySeconds);

            if (counted)
            {
                StartCatchFeedback();
            }

            if (logDebug)
            {
                Debug.Log(
                    $"[BucketLeafCatchSystem] Leaf caught. LocalPoint={localPoint}, progressUpdate={(counted ? "accepted" : "rejected")}.",
                    this);
            }
        }
    }

    private bool IsCurrentlyGrabbed()
    {
        if (handGrabInteractable == null || handGrabInteractable.Interactors == null)
        {
            return false;
        }

        foreach (object candidate in handGrabInteractable.Interactors)
        {
            HandGrabInteractor interactor = candidate as HandGrabInteractor;
            if (interactor != null && interactor.IsGrabbing)
            {
                return true;
            }
        }

        return false;
    }

    private void SubscribeToLeafEvents()
    {
        for (int i = 0; i < leaves.Count; i++)
        {
            LeafsFallingEffect effect = leaves[i].Effect;
            if (effect == null)
            {
                continue;
            }

            effect.FellPastResetThreshold -= HandleLeafFellPastThreshold;
            effect.FellPastResetThreshold += HandleLeafFellPastThreshold;
        }
    }

    private void UnsubscribeFromLeafEvents()
    {
        for (int i = 0; i < leaves.Count; i++)
        {
            LeafsFallingEffect effect = leaves[i].Effect;
            if (effect != null)
            {
                effect.FellPastResetThreshold -= HandleLeafFellPastThreshold;
            }
        }
    }

    private void HandleLeafFellPastThreshold(LeafsFallingEffect effect)
    {
        if (taskDriver == null || !taskDriver.IsCatchStepActive())
        {
            return;
        }

        LeafMissed?.Invoke(effect);
        if (effect != null && effect.CanResetAfterFall)
        {
            LeafRespawned?.Invoke(effect);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.35f);
        Gizmos.DrawWireCube(catchVolumeCenterLocal, catchVolumeSizeLocal);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private sealed class LeafRuntime
    {
        public LeafRuntime(LeafsFallingEffect effect)
        {
            Effect = effect;
        }

        public LeafsFallingEffect Effect { get; }
        public float ResumeAtTime { get; set; }
    }
}
