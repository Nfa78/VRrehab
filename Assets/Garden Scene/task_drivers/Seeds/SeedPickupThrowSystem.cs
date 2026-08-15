using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class SeedPickupThrowSystem : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private SeedPickupState pickupState;
    [SerializeField] private SeedThrowSpawner throwSpawner;
    [SerializeField] private SeedThrowArrow throwArrow;
    [SerializeField] private SeedsTaskDriver taskDriver;

    [Header("Pickup")]
    [SerializeField] private bool completePickObjectiveOnPickup = true;

    [Header("Release")]
    [SerializeField][Min(0f)] private float releaseCooldownSeconds = 0.08f;
    [SerializeField][Min(2)] private int releaseVelocitySampleCount = 6;
    [SerializeField][Min(0.02f)] private float releaseVelocityWindowSeconds = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool subscribedToPickupState;
    private Transform sampledHand;
    private readonly List<HandMotionSample> _releaseSamples = new List<HandMotionSample>(8);
    private bool? lastInteractionAllowed;
    private bool? lastPickupStepActive;
    private bool? lastThrowStepActive;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToPickupState();
    }

    private void OnDisable()
    {
        UnsubscribeFromPickupState();
        ClearReleaseSamples();

        if (throwArrow != null)
        {
            throwArrow.Render(null, false, true);
        }
    }

    private void Update()
    {
        ResolveReferences();
        SubscribeToPickupState();

        Transform handBeforeTick = pickupState != null ? pickupState.ActiveHand : null;
        bool interactionAllowed = IsSeedInteractionAllowed();
        LogInteractionGateState(interactionAllowed);

        if (!interactionAllowed)
        {
            ClearReleaseSamples();

            if (throwArrow != null)
            {
                throwArrow.Render(null, false, true);
            }

            return;
        }

        if (pickupState != null)
        {
            pickupState.Tick(releaseCooldownSeconds);
        }

        Transform activeHand = pickupState != null ? pickupState.ActiveHand : handBeforeTick;
        if (throwArrow != null && pickupState != null)
        {
            throwArrow.Render(activeHand, pickupState.SeedsLoaded, pickupState.IsRightHandLikely);
        }

        if (pickupState != null && pickupState.SeedsLoaded)
        {
            SampleHand(activeHand);
        }
        else
        {
            ClearReleaseSamples();
        }
    }

    public void HandleRadiusEnter(GameObject targetObject)
    {
        ResolveReferences();
        if (pickupState != null && IsSeedInteractionAllowed())
        {
            pickupState.RegisterHandCandidate(targetObject);
        }
    }

    public void HandleRadiusExit(GameObject targetObject)
    {
        ResolveReferences();
        if (pickupState != null)
        {
            pickupState.UnregisterHandCandidate(targetObject);
        }
    }

    private void HandlePickedUp()
    {
        Transform hand = pickupState != null ? pickupState.ActiveHand : null;
        SampleHand(hand, true);

        bool completed = !completePickObjectiveOnPickup || CompletePickObjective();
        if (logDebug)
        {
            Debug.Log(
                $"[SeedPickupThrowSystem] Seeds picked up by {(hand != null ? hand.name : "unknown hand")}. Pick objective {(completed ? "updated" : "not updated")}.",
                this);
        }
    }

    private void HandleReleased()
    {
        Transform hand = pickupState != null ? pickupState.ActiveHand : null;
        if (hand == null || throwSpawner == null)
        {
            return;
        }

        if (!IsSeedInteractionAllowed())
        {
            return;
        }

        SampleHand(hand);
        Vector3 releaseVelocity = ResolveBufferedReleaseVelocity(hand);

        int spawned = throwSpawner.SpawnBurst(hand, releaseVelocity);
        if (logDebug)
        {
            Debug.Log(
                $"[SeedPickupThrowSystem] Released seeds from {hand.name}. Spawned={spawned}. BufferedSpeed={releaseVelocity.magnitude:0.###}.",
                this);
        }

        ClearReleaseSamples();
    }

    private bool CompletePickObjective()
    {
        return taskDriver != null && taskDriver.HandleSeedsPickedUp();
    }

    private void ResolveReferences()
    {
        if (pickupState == null)
        {
            pickupState = GetComponent<SeedPickupState>() ?? GetComponentInChildren<SeedPickupState>(true);
        }

        if (throwSpawner == null)
        {
            throwSpawner = GetComponent<SeedThrowSpawner>() ?? GetComponentInChildren<SeedThrowSpawner>(true);
        }

        if (throwArrow == null)
        {
            throwArrow = GetComponent<SeedThrowArrow>() ?? GetComponentInChildren<SeedThrowArrow>(true);
        }

        if (taskDriver == null)
        {
            taskDriver = FindFirstObjectByType<SeedsTaskDriver>();
        }
    }

    private bool IsSeedInteractionAllowed()
    {
        return taskDriver != null && taskDriver.AllowsSeedInteraction();
    }

    private void LogInteractionGateState(bool interactionAllowed)
    {
        if (!logDebug)
        {
            return;
        }

        bool pickupStepActive = taskDriver != null && taskDriver.IsPickupStepActive();
        bool throwStepActive = taskDriver != null && taskDriver.IsThrowStepActive();

        if (lastInteractionAllowed.HasValue &&
            lastInteractionAllowed.Value == interactionAllowed &&
            lastPickupStepActive == pickupStepActive &&
            lastThrowStepActive == throwStepActive)
        {
            return;
        }

        lastInteractionAllowed = interactionAllowed;
        lastPickupStepActive = pickupStepActive;
        lastThrowStepActive = throwStepActive;

        Debug.Log(
            $"[SeedPickupThrowSystem] Interaction gate allowed={interactionAllowed}, pickupStepActive={pickupStepActive}, throwStepActive={throwStepActive}, taskDriver={(taskDriver != null ? taskDriver.name : "null")}.",
            this);
    }

    private void SubscribeToPickupState()
    {
        if (subscribedToPickupState || pickupState == null)
        {
            return;
        }

        pickupState.PickedUp += HandlePickedUp;
        pickupState.Released += HandleReleased;
        subscribedToPickupState = true;
    }

    private void UnsubscribeFromPickupState()
    {
        if (!subscribedToPickupState || pickupState == null)
        {
            return;
        }

        pickupState.PickedUp -= HandlePickedUp;
        pickupState.Released -= HandleReleased;
        subscribedToPickupState = false;
    }

    private void SampleHand(Transform hand, bool resetBuffer = false)
    {
        if (hand == null)
        {
            return;
        }

        if (resetBuffer || sampledHand != hand)
        {
            _releaseSamples.Clear();
            sampledHand = hand;
        }

        float now = Time.time;
        Vector3 position = hand.position;

        if (_releaseSamples.Count > 0)
        {
            HandMotionSample lastSample = _releaseSamples[_releaseSamples.Count - 1];
            if (now - lastSample.Time <= 0.0001f &&
                (position - lastSample.Position).sqrMagnitude <= 0.000001f)
            {
                TrimReleaseSamples(now);
                return;
            }
        }

        _releaseSamples.Add(new HandMotionSample(position, now));
        TrimReleaseSamples(now);
    }

    private Vector3 ResolveBufferedReleaseVelocity(Transform hand)
    {
        if (hand == null || _releaseSamples.Count < 2)
        {
            return Vector3.zero;
        }

        Vector3 weightedVelocity = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 1; i < _releaseSamples.Count; i++)
        {
            HandMotionSample previousSample = _releaseSamples[i - 1];
            HandMotionSample currentSample = _releaseSamples[i];
            float deltaTime = currentSample.Time - previousSample.Time;
            if (deltaTime <= 0.0001f)
            {
                continue;
            }

            float recencyWeight = (float)i / (_releaseSamples.Count - 1);
            Vector3 segmentVelocity = (currentSample.Position - previousSample.Position) / deltaTime;
            weightedVelocity += segmentVelocity * recencyWeight;
            totalWeight += recencyWeight;
        }

        if (totalWeight > 0.0001f)
        {
            return weightedVelocity / totalWeight;
        }

        HandMotionSample oldestSample = _releaseSamples[0];
        HandMotionSample newestSample = _releaseSamples[_releaseSamples.Count - 1];
        float totalTime = newestSample.Time - oldestSample.Time;
        if (totalTime <= 0.0001f)
        {
            return Vector3.zero;
        }

        return (newestSample.Position - oldestSample.Position) / totalTime;
    }

    private void TrimReleaseSamples(float now)
    {
        int sampleLimit = Mathf.Max(2, releaseVelocitySampleCount);
        while (_releaseSamples.Count > sampleLimit)
        {
            _releaseSamples.RemoveAt(0);
        }

        float sampleWindow = Mathf.Max(0.02f, releaseVelocityWindowSeconds);
        while (_releaseSamples.Count > 1 && now - _releaseSamples[0].Time > sampleWindow)
        {
            _releaseSamples.RemoveAt(0);
        }
    }

    private void ClearReleaseSamples()
    {
        _releaseSamples.Clear();
        sampledHand = null;
    }

    private readonly struct HandMotionSample
    {
        public HandMotionSample(Vector3 position, float time)
        {
            Position = position;
            Time = time;
        }

        public Vector3 Position { get; }
        public float Time { get; }
    }
}
