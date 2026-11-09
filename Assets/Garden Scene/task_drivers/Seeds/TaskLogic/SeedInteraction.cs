using System;
using System.Collections.Generic;
using SoundSystem;
using UnityEngine;
using TriggerSystem;

[DisallowMultipleComponent]
public sealed class SeedInteraction : MonoBehaviour
{
    private enum State { WaitingForSeeds, SeedsLoaded }

    [SerializeField] private TSParasite TS;
    [SerializeField] private TaskSystem.SeedsTaskDriver taskDriver;
    [SerializeField] private bool logDebug;

    private readonly HashSet<GameObject> targetsInside = new HashSet<GameObject>();
    private State state = State.WaitingForSeeds;
    private GameObject activeTarget;
    private Hand activeHand;

    public bool SeedsLoaded => state == State.SeedsLoaded;
    public Hand GrabbingHand => SeedsLoaded ? activeHand : null;
    public Transform ActiveHand => activeHand?.TrackedAnchor;
    public bool IsRightHandLikely => activeHand == HandsResolver.hands?.right;
    public event Action PickedUp;
    public event Action<int, float> ThrowBurstSpawned;

    private void Awake()
    {
        ResolveReferences();
        if (TS == null)
        {
            Debug.LogError(
                "SeedInteraction requires a manually assigned TSParasite on Seeds.",
                this);
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (TS == null) return;
        TS.RadiusEntered += OnRadiusEntered;
        TS.RadiusExited += OnRadiusExited;
        foreach (GameObject target in TS.TargetsInsideRadius) OnRadiusEntered(target);
    }

    private void OnDisable()
    {
        if (TS != null)
        {
            TS.RadiusEntered -= OnRadiusEntered;
            TS.RadiusExited -= OnRadiusExited;
        }
        targetsInside.Clear(); activeTarget = null; activeHand = null; state = State.WaitingForSeeds;
    }

    private void Update()
    {
        ResolveReferences();
        if (taskDriver == null || !taskDriver.AllowsSeedInteraction()) return;
        if (state == State.WaitingForSeeds && (TS == null || !TS.HasTargetInsideRadius))
        {
            activeTarget = null;
            activeHand = null;
            return;
        }
        if (activeTarget == null) SelectTarget();
        if (activeTarget == null) return;

        activeHand = ResolveHand(activeTarget.transform);
        if (activeHand == null) return;
        Hand.Pose pose = activeHand.getPose();

        bool holding = pose == Hand.Pose.Grab || pose == Hand.Pose.Pinch;

        if (state == State.WaitingForSeeds && holding)
        {
            state = State.SeedsLoaded;
            PickedUp?.Invoke();
            EventSoundManager.TryPlayAt("garden.seed.grabbed", activeTarget.transform.position);
            taskDriver.HandleSeedsPickedUp();
            Log("Seeds picked up.");

        }
        else if (state == State.SeedsLoaded && !holding)
        {
            state = State.WaitingForSeeds;
            activeTarget = null;
            activeHand = null;
        }
    }

    public void ApplyDifficulty(float pickupRadius)
    {
        if (TS != null) TS.DetectionRadius = pickupRadius;
    }

    private void OnRadiusEntered(GameObject target)
    {
        if (target == null) return;
        targetsInside.Add(target);
        SelectTarget();
    }

    private void OnRadiusExited(GameObject target)
    {
        if (target == null) return;
        targetsInside.Remove(target);
        if (target == activeTarget && state == State.WaitingForSeeds)
        {
            activeTarget = null;
            SelectTarget();
        }
    }

    private void SelectTarget()
    {
        foreach (GameObject target in targetsInside)
        {
            if (target != null) { activeTarget = target; return; }
        }
        activeTarget = null;
    }

    private static Hand ResolveHand(Transform target)
    {
        bool left = false;
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0) { left = true; break; }
            if (current.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0) break;
        }
        return left ? HandsResolver.hands?.left : HandsResolver.hands?.right;
    }

    private void ResolveReferences()
    {
        if (taskDriver == null) taskDriver = FindFirstObjectByType<TaskSystem.SeedsTaskDriver>();
    }

    private void Log(string message)
    {
        if (logDebug) Debug.Log($"[SeedInteraction] {message}", this);
    }
}
