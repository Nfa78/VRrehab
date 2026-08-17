using System;
using System.Collections.Generic;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using TriggerSystem;
using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class SeedPickupState : MonoBehaviour
{
    private const string DefaultHandAnchorTag = "HandAnchor";
    private const string LegacyHandVisualTag = "HandVisual";

    public enum PickupMode
    {
        None,
        Grab,
        Pinch
    }

    [Header("Detection")]
    [SerializeField] private bool useHandTagFallback = true;
    [SerializeField] private bool allowGrabAsPickup = true;
    [SerializeField] private bool allowPinchAsPickup = true;
    [SerializeField] private bool useHandsResolver = true;
    [SerializeField] private HandsResolver handsResolver;
    [SerializeField] private bool logDebug;
    [SerializeField] private TSParasite triggerSystemRadius;
    [SerializeField] private Transform seedRadiusSource;
    [SerializeField][Min(0f)] private float seedPickupRadius = 0.35f;
    [SerializeField][Min(0.05f)] private float radiusTargetRefreshInterval = 0.25f;
    [SerializeField] private string radiusHandTag = DefaultHandAnchorTag;
    [SerializeField] private bool createRadiusParasiteIfMissing = true;
    [SerializeField][Min(0f)] private float closestInteractorMaxDistance = 0.5f;
    [SerializeField][Min(0f)] private float controllerGripStrengthThreshold = 0.45f;

    private readonly HashSet<Transform> handsInside = new HashSet<Transform>();
    private Transform activeHand;
    private HandGrabInteractor activeGrabInteractor;
    private OVRHand activeOvrHand;
    private bool hadActionActiveLastFrame;
    private float lastReleaseTime = -999f;
    private bool subscribedToRadius;
    private bool hasGestureDebugSnapshot;
    private string lastGestureDebugHandName;
    private bool lastDebugInteractorGrab;
    private bool lastDebugPalmGrab;
    private bool lastDebugResolverGrab;
    private bool lastDebugResolverPinch;
    private bool lastDebugResolverTracked;
    private bool lastDebugResolverValid;
    private HandsResolver.HandInputSource lastDebugResolverSource;
    private bool lastDebugUnityXrGrab;
    private bool lastDebugControllerGrip;
    private bool lastDebugIndexPinch;
    private bool lastDebugMiddlePinch;
    private bool lastDebugThumbPinch;
    private bool lastDebugActionActive;
    private TSParasite configuredRadiusParasite;
    private float configuredRadiusValue = -1f;
    private float configuredRefreshInterval = -1f;
    private string configuredRadiusTagSignature;
    private readonly List<InputDevice> unityXrInputDevices = new List<InputDevice>();

    public Transform ActiveHand => activeHand;
    public OVRHand ActiveOvrHand => activeOvrHand;
    public PickupMode CurrentPickupMode { get; private set; } = PickupMode.None;
    public bool SeedsLoaded { get; private set; }
    public bool IsRightHandLikely { get; private set; } = true;

    public event Action PickedUp;
    public event Action Released;

    public void ApplyDifficulty(float pickupRadius)
    {
        seedPickupRadius = Mathf.Max(0f, pickupRadius);
        EnsureRadius();
    }

    private void OnEnable()
    {
        EnsureRadius();
        SubscribeToRadius();
    }

    private void Start()
    {
        EnsureRadius();
        SubscribeToRadius();
        RegisterCurrentRadiusTargets();
    }

    private void OnDisable()
    {
        UnsubscribeFromRadius();
        handsInside.Clear();
        ClearActiveHand();
    }

    public void Tick(float releaseCooldownSeconds)
    {
        EnsureRadius();
        SubscribeToRadius();
        RegisterCurrentRadiusTargets();

        if (activeHand == null)
        {
            BindNextHand();
        }
        else if (!useHandsResolver && !HasActionSource())
        {
            ResolveActionSources(null, activeHand);
        }

        bool actionActive = IsActionActive();
        LogGestureStateIfChanged(actionActive);
        if (!SeedsLoaded && actionActive && !hadActionActiveLastFrame)
        {
            TryPickUp("input");
        }

        if (SeedsLoaded &&
            !IsPickupModeStillActive() &&
            Time.time - lastReleaseTime >= releaseCooldownSeconds)
        {
            Log($"Seeds released from {GetActiveHandName()} using {CurrentPickupMode}.");
            Released?.Invoke();
            SeedsLoaded = false;
            CurrentPickupMode = PickupMode.None;
            lastReleaseTime = Time.time;
        }

        hadActionActiveLastFrame = actionActive;
    }

    public void RegisterHandCandidate(GameObject target)
    {
        RegisterHand(ResolveHandAnchor(target), target);
    }

    public void UnregisterHandCandidate(GameObject target)
    {
        UnregisterHand(ResolveHandAnchor(target));
    }

    private void RegisterHand(Transform handAnchor, GameObject sourceTarget)
    {
        if (handAnchor == null)
        {
            return;
        }

        bool added = handsInside.Add(handAnchor);
        if (activeHand == null || !HasActionSource())
        {
            BindHand(handAnchor, sourceTarget);
        }

        if (added)
        {
            Log($"Hand anchor entered radius: {handAnchor.name}.");
        }
    }

    private void UnregisterHand(Transform handAnchor)
    {
        if (handAnchor == null || !handsInside.Remove(handAnchor))
        {
            return;
        }

        Log($"Hand anchor exited radius: {handAnchor.name}.");
        if (handAnchor != activeHand || SeedsLoaded)
        {
            return;
        }

        ClearActiveHand();
        BindNextHand();
    }

    private void BindHand(Transform handAnchor, GameObject sourceTarget)
    {
        if (handAnchor == null)
        {
            return;
        }

        activeHand = handAnchor;
        ResolveActionSources(sourceTarget, handAnchor);
        IsRightHandLikely = GuessRightHand(handAnchor, activeGrabInteractor, activeOvrHand);
        ResolveHandsResolver();
        // Require a fresh grab/pinch after entering the radius before pickup can occur.
        hadActionActiveLastFrame = IsActionActive();

        Log(
            $"Bound hand anchor={activeHand.name}, side={(IsRightHandLikely ? "Right" : "Left")}, resolver={DescribeResolvedHand(GetActiveResolvedHand())}, grab={(activeGrabInteractor != null ? activeGrabInteractor.name : "null")}, ovrHand={(activeOvrHand != null ? activeOvrHand.name : "null")}, actionActive={IsActionActive()}, grabGesture={IsGrabGestureActive()}, xrGrab={IsUnityXrGrabGestureActive()}, pinch={DescribeOvrFinger(OVRHand.HandFinger.Index)}, grip={DescribeOvrFinger(OVRHand.HandFinger.Middle)}, thumb={DescribeOvrFinger(OVRHand.HandFinger.Thumb)}.");
    }

    private void BindNextHand()
    {
        foreach (Transform handAnchor in handsInside)
        {
            if (handAnchor != null)
            {
                BindHand(handAnchor, null);
                return;
            }
        }
    }

    private void ClearActiveHand()
    {
        activeHand = null;
        activeGrabInteractor = null;
        activeOvrHand = null;
        hadActionActiveLastFrame = false;
        CurrentPickupMode = PickupMode.None;
        hasGestureDebugSnapshot = false;
        lastGestureDebugHandName = null;
    }

    private bool TryPickUp(string reason)
    {
        if (SeedsLoaded || !IsActionActive())
        {
            return false;
        }

        SeedsLoaded = true;
        CurrentPickupMode = ResolvePickupMode();
        hadActionActiveLastFrame = true;
        Log($"Seeds picked up by {GetActiveHandName()} via {reason}. mode={CurrentPickupMode}.");
        PickedUp?.Invoke();
        return true;
    }

    private bool IsActionActive()
    {
        HandsResolver.ResolvedHand resolvedHand = GetActiveResolvedHand();
        bool resolverGrabbing = allowGrabAsPickup &&
                                resolvedHand != null &&
                                resolvedHand.Gestures.Grab;
        bool resolverPinching = allowPinchAsPickup &&
                                resolvedHand != null &&
                                resolvedHand.Gestures.Pinch;
        bool grabbing = allowGrabAsPickup && IsGrabGestureActive();
        bool pinching = allowPinchAsPickup &&
                        activeOvrHand != null &&
                        activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        return resolverGrabbing || resolverPinching || grabbing || pinching;
    }

    private PickupMode ResolvePickupMode()
    {
        HandsResolver.ResolvedHand resolvedHand = GetActiveResolvedHand();
        if (allowPinchAsPickup &&
            resolvedHand != null &&
            resolvedHand.Gestures.Pinch)
        {
            return PickupMode.Pinch;
        }

        if (allowGrabAsPickup &&
            resolvedHand != null &&
            resolvedHand.Gestures.Grab)
        {
            return PickupMode.Grab;
        }

        if (allowPinchAsPickup &&
            activeOvrHand != null &&
            activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Index))
        {
            return PickupMode.Pinch;
        }

        return allowGrabAsPickup && IsGrabGestureActive()
            ? PickupMode.Grab
            : PickupMode.None;
    }

    private bool IsPickupModeStillActive()
    {
        HandsResolver.ResolvedHand resolvedHand = GetActiveResolvedHand();
        switch (CurrentPickupMode)
        {
            case PickupMode.Pinch:
                if (allowPinchAsPickup &&
                    resolvedHand != null &&
                    resolvedHand.Gestures.Pinch)
                {
                    return true;
                }

                return allowPinchAsPickup &&
                       activeOvrHand != null &&
                       activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            case PickupMode.Grab:
                if (allowGrabAsPickup &&
                    resolvedHand != null &&
                    resolvedHand.Gestures.Grab)
                {
                    return true;
                }

                return allowGrabAsPickup && IsGrabGestureActive();
            default:
                return IsActionActive();
        }
    }

    private bool IsGrabGestureActive()
    {
        HandsResolver.ResolvedHand resolvedHand = GetActiveResolvedHand();
        if (resolvedHand != null && resolvedHand.Gestures.Grab)
        {
            return true;
        }

        if (activeGrabInteractor != null && activeGrabInteractor.IsGrabbing)
        {
            return true;
        }

        HandGrabAPI handGrabApi = activeGrabInteractor != null ? activeGrabInteractor.HandGrabApi : null;
        if (handGrabApi != null && handGrabApi.IsHandPalmGrabbing(GrabbingRule.DefaultPalmRule))
        {
            return true;
        }

        if (IsUnityXrGrabGestureActive())
        {
            return true;
        }

        return IsControllerGripGestureActive();
    }

    private bool IsUnityXrGrabGestureActive()
    {
        if (activeHand == null)
        {
            return false;
        }

        InputDeviceCharacteristics side = IsRightHandLikely
            ? InputDeviceCharacteristics.Right
            : InputDeviceCharacteristics.Left;

        return TryReadUnityXrGrab(side | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand) ||
               TryReadUnityXrGrab(side | InputDeviceCharacteristics.Controller) ||
               TryReadUnityXrGrab(side | InputDeviceCharacteristics.HeldInHand) ||
               TryReadUnityXrGrab(side);
    }

    private bool TryReadUnityXrGrab(InputDeviceCharacteristics characteristics)
    {
        unityXrInputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(characteristics, unityXrInputDevices);

        for (int i = 0; i < unityXrInputDevices.Count; i++)
        {
            InputDevice device = unityXrInputDevices[i];
            if (!device.isValid)
            {
                continue;
            }

            if (IsUnityXrButtonPressed(device, CommonUsages.gripButton, CommonUsages.grip) ||
                IsUnityXrButtonPressed(device, CommonUsages.triggerButton, CommonUsages.trigger))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsUnityXrButtonPressed(InputDevice device, InputFeatureUsage<bool> button, InputFeatureUsage<float> axis)
    {
        if (device.TryGetFeatureValue(button, out bool buttonPressed) && buttonPressed)
        {
            return true;
        }

        return device.TryGetFeatureValue(axis, out float axisValue) &&
               axisValue >= controllerGripStrengthThreshold;
    }

    private bool IsControllerGripGestureActive()
    {
        if (activeOvrHand == null || !activeOvrHand.IsDataValid)
        {
            return false;
        }

        bool middlePressed = activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Middle) ||
                             activeOvrHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) >= controllerGripStrengthThreshold;
        bool thumbPressed = activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Thumb) ||
                            activeOvrHand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb) >= controllerGripStrengthThreshold;
        return middlePressed && thumbPressed;
    }

    private void EnsureRadius()
    {
        if (triggerSystemRadius == null)
        {
            Transform source = ResolveSeedRadiusSource();
            if (source != null)
            {
                triggerSystemRadius = source.GetComponent<TSParasite>() ??
                                      source.GetComponentInChildren<TSParasite>(true);

                if (triggerSystemRadius == null && createRadiusParasiteIfMissing)
                {
                    triggerSystemRadius = source.gameObject.AddComponent<TSParasite>();
                }
            }
        }

        if (triggerSystemRadius == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(radiusHandTag))
        {
            radiusHandTag = DefaultHandAnchorTag;
        }

        triggerSystemRadius.DetectionRadius = seedPickupRadius;
        triggerSystemRadius.TargetRefreshInterval = radiusTargetRefreshInterval;

        string radiusTagSignature = BuildRadiusTagSignature();
        bool needsReconfigure = triggerSystemRadius != configuredRadiusParasite ||
                                !Mathf.Approximately(configuredRadiusValue, seedPickupRadius) ||
                                !Mathf.Approximately(configuredRefreshInterval, radiusTargetRefreshInterval) ||
                                !string.Equals(configuredRadiusTagSignature, radiusTagSignature, StringComparison.Ordinal);

        if (!needsReconfigure)
        {
            return;
        }

        triggerSystemRadius.Configure(BuildRadiusDetectTags(), seedPickupRadius, radiusTargetRefreshInterval);
        configuredRadiusParasite = triggerSystemRadius;
        configuredRadiusValue = seedPickupRadius;
        configuredRefreshInterval = radiusTargetRefreshInterval;
        configuredRadiusTagSignature = radiusTagSignature;
    }

    private Transform ResolveSeedRadiusSource()
    {
        if (seedRadiusSource != null)
        {
            return seedRadiusSource;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && string.Equals(child.name, "Seeds", StringComparison.OrdinalIgnoreCase))
            {
                seedRadiusSource = child;
                return seedRadiusSource;
            }
        }

        seedRadiusSource = transform;
        return seedRadiusSource;
    }

    private void SubscribeToRadius()
    {
        if (subscribedToRadius || triggerSystemRadius == null)
        {
            return;
        }

        triggerSystemRadius.RadiusEntered += HandleRadiusEntered;
        triggerSystemRadius.RadiusExited += HandleRadiusExited;
        subscribedToRadius = true;
    }

    private void UnsubscribeFromRadius()
    {
        if (!subscribedToRadius || triggerSystemRadius == null)
        {
            return;
        }

        triggerSystemRadius.RadiusEntered -= HandleRadiusEntered;
        triggerSystemRadius.RadiusExited -= HandleRadiusExited;
        subscribedToRadius = false;
    }

    private void RegisterCurrentRadiusTargets()
    {
        if (triggerSystemRadius == null)
        {
            return;
        }

        IReadOnlyList<GameObject> targets = triggerSystemRadius.TargetsInsideRadius;
        for (int i = 0; i < targets.Count; i++)
        {
            RegisterHandCandidate(targets[i]);
        }
    }

    private void HandleRadiusEntered(GameObject target)
    {
        Log($"Radius entered by {(target != null ? target.name : "null")}.");
        RegisterHandCandidate(target);
    }

    private void HandleRadiusExited(GameObject target)
    {
        Log($"Radius exited by {(target != null ? target.name : "null")}.");
        UnregisterHandCandidate(target);
    }

    private Transform ResolveHandAnchor(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        string handAnchorTag = string.IsNullOrWhiteSpace(radiusHandTag) ? DefaultHandAnchorTag : radiusHandTag;
        Transform anchor = FindTaggedTransform(target.transform, handAnchorTag);
        if (anchor != null)
        {
            return anchor;
        }

        if (!useHandTagFallback)
        {
            return null;
        }

        Transform visualHand = FindTaggedTransform(target.transform, LegacyHandVisualTag);
        if (visualHand != null)
        {
            return FindNearestTaggedTransform(visualHand.position, handAnchorTag);
        }

        if (target.GetComponentInParent<HandGrabInteractor>() != null ||
            target.GetComponentInParent<OVRHand>() != null)
        {
            return FindNearestTaggedTransform(target.transform.position, handAnchorTag);
        }

        return null;
    }

    private void ResolveActionSources(GameObject sourceTarget, Transform handAnchor)
    {
        activeGrabInteractor = ResolveActionSourceFromTarget<HandGrabInteractor>(sourceTarget) ??
                               ResolveNearestComponent<HandGrabInteractor>(handAnchor);
        activeOvrHand = ResolveActionSourceFromTarget<OVRHand>(sourceTarget) ??
                        ResolveNearestComponent<OVRHand>(handAnchor);
    }

    private static T ResolveActionSourceFromTarget<T>(GameObject sourceTarget) where T : Component
    {
        if (sourceTarget == null)
        {
            return null;
        }

        return sourceTarget.GetComponentInParent<T>();
    }

    private T ResolveNearestComponent<T>(Transform handAnchor) where T : Component
    {
        if (handAnchor == null)
        {
            return null;
        }

        T direct = handAnchor.GetComponent<T>();
        if (direct != null)
        {
            return direct;
        }

        T child = handAnchor.GetComponentInChildren<T>(true);
        if (child != null)
        {
            return child;
        }

        return FindNearest<T>(handAnchor.position, InferHandSide(handAnchor));
    }

    private T FindNearest<T>(Vector3 point, string preferredSide = null) where T : Component
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsSortMode.None);
        T bestAny = null;
        T bestSideMatch = null;
        T bestSideAnyDistance = null;
        float bestAnySqrDistance = closestInteractorMaxDistance <= 0f
            ? float.MaxValue
            : closestInteractorMaxDistance * closestInteractorMaxDistance;
        float bestSideSqrDistance = bestAnySqrDistance;
        float bestSideAnySqrDistance = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - point).sqrMagnitude;
            if (sqrDistance <= bestAnySqrDistance)
            {
                bestAnySqrDistance = sqrDistance;
                bestAny = candidate;
            }

            if (!string.IsNullOrWhiteSpace(preferredSide) &&
                LooksLikeSideLabel(candidate.transform, preferredSide))
            {
                if (sqrDistance <= bestSideSqrDistance)
                {
                    bestSideSqrDistance = sqrDistance;
                    bestSideMatch = candidate;
                }

                if (sqrDistance <= bestSideAnySqrDistance)
                {
                    bestSideAnySqrDistance = sqrDistance;
                    bestSideAnyDistance = candidate;
                }
            }
        }

        if (bestSideMatch != null)
        {
            return bestSideMatch;
        }

        return bestAny != null ? bestAny : bestSideAnyDistance;
    }

    private static Transform FindTaggedTransform(Transform start, string tagName)
    {
        if (start == null || string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        for (Transform cursor = start; cursor != null; cursor = cursor.parent)
        {
            if (string.Equals(cursor.tag, tagName, StringComparison.Ordinal))
            {
                return cursor;
            }
        }

        return null;
    }

    private Transform FindNearestTaggedTransform(Vector3 point, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tagName);
        Transform best = null;
        float bestSqrDistance = closestInteractorMaxDistance <= 0f
            ? float.MaxValue
            : closestInteractorMaxDistance * closestInteractorMaxDistance;

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject candidate = taggedObjects[i];
            if (candidate == null)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - point).sqrMagnitude;
            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = candidate.transform;
            }
        }

        return best;
    }

    private static string InferHandSide(Transform target)
    {
        if (LooksLikeSideLabel(target, "right"))
        {
            return "right";
        }

        if (LooksLikeSideLabel(target, "left"))
        {
            return "left";
        }

        return null;
    }

    private IEnumerable<string> BuildRadiusDetectTags()
    {
        yield return string.IsNullOrWhiteSpace(radiusHandTag)
            ? DefaultHandAnchorTag
            : radiusHandTag;

        if (useHandTagFallback &&
            !string.Equals(radiusHandTag, LegacyHandVisualTag, StringComparison.Ordinal))
        {
            yield return LegacyHandVisualTag;
        }
    }

    private string BuildRadiusTagSignature()
    {
        string primaryTag = string.IsNullOrWhiteSpace(radiusHandTag)
            ? DefaultHandAnchorTag
            : radiusHandTag;

        if (useHandTagFallback &&
            !string.Equals(primaryTag, LegacyHandVisualTag, StringComparison.Ordinal))
        {
            return $"{primaryTag}|{LegacyHandVisualTag}";
        }

        return primaryTag;
    }

    private static bool GuessRightHand(Transform handAnchor, HandGrabInteractor grabInteractor, OVRHand ovrHand)
    {
        if (LooksLikeHandSide(handAnchor, "right"))
        {
            return true;
        }

        if (LooksLikeHandSide(handAnchor, "left"))
        {
            return false;
        }

        if (grabInteractor != null)
        {
            if (LooksLikeHandSide(grabInteractor.transform, "right"))
            {
                return true;
            }

            if (LooksLikeHandSide(grabInteractor.transform, "left"))
            {
                return false;
            }
        }

        if (ovrHand != null)
        {
            if (LooksLikeHandSide(ovrHand.transform, "right"))
            {
                return true;
            }

            if (LooksLikeHandSide(ovrHand.transform, "left"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeHandSide(Transform target, string side)
    {
        for (Transform cursor = target; cursor != null; cursor = cursor.parent)
        {
            string name = cursor.name;
            if (!string.IsNullOrWhiteSpace(name) &&
                name.IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeSideLabel(Transform target, string side)
    {
        if (target == null || string.IsNullOrWhiteSpace(side))
        {
            return false;
        }

        for (Transform cursor = target; cursor != null; cursor = cursor.parent)
        {
            string name = cursor.name;
            if (!string.IsNullOrWhiteSpace(name) &&
                name.IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActionSource()
    {
        return activeGrabInteractor != null || activeOvrHand != null;
    }

    private void ResolveHandsResolver()
    {
        if (!useHandsResolver)
        {
            return;
        }

        if (handsResolver == null)
        {
            handsResolver = HandsResolver.Instance;
        }

        if (handsResolver != null)
        {
            handsResolver.Refresh();
        }
    }

    private HandsResolver.ResolvedHand GetActiveResolvedHand()
    {
        if (!useHandsResolver || activeHand == null)
        {
            return null;
        }

        ResolveHandsResolver();
        if (handsResolver == null)
        {
            return null;
        }

        return handsResolver.GetHand(IsRightHandLikely
            ? HandsResolver.HandSide.Right
            : HandsResolver.HandSide.Left);
    }

    private void LogGestureStateIfChanged(bool actionActive)
    {
        if (!logDebug || activeHand == null)
        {
            return;
        }

        bool interactorGrab = activeGrabInteractor != null && activeGrabInteractor.IsGrabbing;
        HandGrabAPI handGrabApi = activeGrabInteractor != null ? activeGrabInteractor.HandGrabApi : null;
        bool palmGrab = handGrabApi != null && handGrabApi.IsHandPalmGrabbing(GrabbingRule.DefaultPalmRule);
        HandsResolver.ResolvedHand resolvedHand = GetActiveResolvedHand();
        HandsResolver.HandInputSource resolverSource = resolvedHand != null
            ? resolvedHand.ActiveSource
            : HandsResolver.HandInputSource.None;
        bool resolverGrab = resolvedHand != null && resolvedHand.Gestures.Grab;
        bool resolverPinch = resolvedHand != null && resolvedHand.Gestures.Pinch;
        bool resolverTracked = resolvedHand != null && resolvedHand.IsTracked;
        bool resolverValid = resolvedHand != null && resolvedHand.IsDataValid;
        bool unityXrGrab = IsUnityXrGrabGestureActive();
        bool controllerGrip = IsControllerGripGestureActive();
        bool indexPinch = activeOvrHand != null && activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        bool middlePinch = activeOvrHand != null && activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
        bool thumbPinch = activeOvrHand != null && activeOvrHand.GetFingerIsPinching(OVRHand.HandFinger.Thumb);
        string handName = activeHand.name;

        bool changed = !hasGestureDebugSnapshot ||
                       !string.Equals(lastGestureDebugHandName, handName, StringComparison.Ordinal) ||
                       lastDebugInteractorGrab != interactorGrab ||
                       lastDebugPalmGrab != palmGrab ||
                       lastDebugResolverGrab != resolverGrab ||
                       lastDebugResolverPinch != resolverPinch ||
                       lastDebugResolverTracked != resolverTracked ||
                       lastDebugResolverValid != resolverValid ||
                       lastDebugResolverSource != resolverSource ||
                       lastDebugUnityXrGrab != unityXrGrab ||
                       lastDebugControllerGrip != controllerGrip ||
                       lastDebugIndexPinch != indexPinch ||
                       lastDebugMiddlePinch != middlePinch ||
                       lastDebugThumbPinch != thumbPinch ||
                       lastDebugActionActive != actionActive;

        if (!changed)
        {
            return;
        }

        hasGestureDebugSnapshot = true;
        lastGestureDebugHandName = handName;
        lastDebugInteractorGrab = interactorGrab;
        lastDebugPalmGrab = palmGrab;
        lastDebugResolverGrab = resolverGrab;
        lastDebugResolverPinch = resolverPinch;
        lastDebugResolverTracked = resolverTracked;
        lastDebugResolverValid = resolverValid;
        lastDebugResolverSource = resolverSource;
        lastDebugUnityXrGrab = unityXrGrab;
        lastDebugControllerGrip = controllerGrip;
        lastDebugIndexPinch = indexPinch;
        lastDebugMiddlePinch = middlePinch;
        lastDebugThumbPinch = thumbPinch;
        lastDebugActionActive = actionActive;

        Log(
            $"Gesture state hand={handName}, inside={handsInside.Contains(activeHand)}, resolver={DescribeResolvedHand(resolvedHand)}, selectGrab={interactorGrab}, palmGrab={palmGrab}, xrGrab={unityXrGrab}, controllerGrip={controllerGrip}, index={DescribeOvrFinger(OVRHand.HandFinger.Index)}, middle={DescribeOvrFinger(OVRHand.HandFinger.Middle)}, thumb={DescribeOvrFinger(OVRHand.HandFinger.Thumb)}, actionActive={actionActive}, seedsLoaded={SeedsLoaded}, mode={CurrentPickupMode}.");
    }

    private string DescribeResolvedHand(HandsResolver.ResolvedHand resolvedHand)
    {
        if (resolvedHand == null)
        {
            return "null";
        }

        return $"{resolvedHand.Side}/source={resolvedHand.ActiveSource}/tracked={resolvedHand.IsTracked}/valid={resolvedHand.IsDataValid}/grab={resolvedHand.Gestures.Grab}/pinch={resolvedHand.Gestures.Pinch}/grip={resolvedHand.Gestures.GripStrength:0.##}/trigger={resolvedHand.Gestures.TriggerStrength:0.##}";
    }

    private string DescribeOvrFinger(OVRHand.HandFinger finger)
    {
        if (activeOvrHand == null)
        {
            return "null";
        }

        return $"{activeOvrHand.GetFingerIsPinching(finger)}/{activeOvrHand.GetFingerPinchStrength(finger):0.##}";
    }

    private string GetActiveHandName()
    {
        return activeHand != null ? activeHand.name : "unknown";
    }

    private void Log(string message)
    {
        if (logDebug)
        {
            Debug.Log($"[SeedPickupState] {message}", this);
        }
    }
}
