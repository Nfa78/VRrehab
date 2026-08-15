using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
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
    [SerializeField] private SimManager simManager;
    [SerializeField] private Transform leavesRoot;

    [Header("Task")]
    [SerializeField] private string requiredTaskId = "catch_leafs";
    [SerializeField] private string pickupObjectiveId = "pick_bucket";
    [SerializeField] private string catchObjectiveId = "catch_leafs";
    [SerializeField] private string legacyLeafTaskId = "rake_leafs";

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
    private bool? bucketGrabComponentsEnabled;
    private string lastLoggedTaskId;

    private void Awake()
    {
        ResolveReferences();
        CacheLeaves();
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheLeaves();
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
        wasGrabbedLastFrame = false;
    }

    private void OnDisable()
    {
        SetBucketGrabComponentsEnabled(false);
        SetLeavesActive(false, true);
        wasGrabbedLastFrame = false;
    }

    private void Update()
    {
        ResolveReferences();
        CacheLeaves();

        bool bucketCatchTaskActive = IsCurrentTask(requiredTaskId);
        bool pickupStepActive = bucketCatchTaskActive && IsCurrentObjective(pickupObjectiveId);
        bool catchStepActive = bucketCatchTaskActive && IsCurrentObjective(catchObjectiveId);
        bool legacyLeafTaskActive = IsCurrentTask(legacyLeafTaskId);

        SetBucketGrabComponentsEnabled(bucketCatchTaskActive);
        LogTaskGateState(bucketCatchTaskActive);

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
        if (pickupStepActive && isGrabbed && !wasGrabbedLastFrame)
        {
            bool completed = taskDriver != null && taskDriver.CompletePickupStep();
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

        if (simManager == null)
        {
            simManager = FindFirstObjectByType<SimManager>();
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
                $"[BucketLeafCatchSystem] Bucket grab components {(enabled ? "enabled" : "disabled")} for task '{requiredTaskId}'.",
                this);
        }
    }

    private void LogTaskGateState(bool bucketCatchTaskActive)
    {
        if (!logDebug)
        {
            return;
        }

        string taskId = simManager != null && simManager.CurrentTask != null
            ? simManager.CurrentTask.TaskId
            : "<none>";
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
        }
    }

    private void ProcessLeafCatches()
    {
        if (taskDriver == null)
        {
            return;
        }

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

            bool counted = taskDriver.CatchLeaf();
            leaf.Effect.SetSimulationActive(false, true);
            leaf.ResumeAtTime = Time.time + Mathf.Max(0f, leafRespawnDelaySeconds);

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

    private bool IsCurrentTask(string taskId)
    {
        return simManager != null &&
               simManager.CurrentTask != null &&
               !string.IsNullOrWhiteSpace(taskId) &&
               string.Equals(simManager.CurrentTask.TaskId, taskId, System.StringComparison.Ordinal);
    }

    private bool IsCurrentObjective(string objectiveId)
    {
        return simManager != null &&
               simManager.CurrentObjective != null &&
               !string.IsNullOrWhiteSpace(objectiveId) &&
               string.Equals(simManager.CurrentObjective.ObjectiveId, objectiveId, System.StringComparison.Ordinal);
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
