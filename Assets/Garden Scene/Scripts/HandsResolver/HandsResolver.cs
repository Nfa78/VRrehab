using System;
using System.Collections.Generic;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(-250)]
public sealed class HandsResolver : MonoBehaviour
{
    public enum HandSide
    {
        Left,
        Right
    }

    [Flags]
    public enum HandInputSource
    {
        None = 0,
        OVRHand = 1,
        OVRSkeleton = 2,
        UnityXR = 4,
        HandGrabInteractor = 8
    }

    [Header("Optional explicit sources")]
    [SerializeField] private OVRHand leftOvrHand;
    [SerializeField] private OVRHand rightOvrHand;
    [SerializeField] private OVRSkeleton leftSkeleton;
    [SerializeField] private OVRSkeleton rightSkeleton;
    [SerializeField] private HandGrabInteractor leftGrabInteractor;
    [SerializeField] private HandGrabInteractor rightGrabInteractor;

    [Header("Resolution")]
    [SerializeField] private bool autoResolveSources = true;
    [SerializeField][Min(0.1f)] private float sourceRefreshInterval = 1f;
    [SerializeField][Range(0f, 1f)] private float gripThreshold = 0.45f;
    [SerializeField][Range(0f, 1f)] private float triggerThreshold = 0.45f;
    [SerializeField][Range(0f, 1f)] private float pinchStrengthThreshold = 0.65f;
    [Header("Debug")]
    [SerializeField] private bool logDebug;
    [SerializeField] private bool logSourceResolution = true;
    [SerializeField] private bool logHandState = true;
    [SerializeField][Min(0.1f)] private float debugLogInterval = 0.5f;

    private static HandsResolver instance;
    private static readonly ResolvedHand NullLeftHand = new ResolvedHand(HandSide.Left);
    private static readonly ResolvedHand NullRightHand = new ResolvedHand(HandSide.Right);

    private readonly ResolvedHand leftHand = new ResolvedHand(HandSide.Left);
    private readonly ResolvedHand rightHand = new ResolvedHand(HandSide.Right);
    private readonly List<InputDevice> inputDevices = new List<InputDevice>();
    private float nextSourceRefreshTime;
    private int lastRefreshFrame = -1;
    private bool isRefreshing;
    private string lastSourceDebugSnapshot;

    public static HandsResolver Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<HandsResolver>();
            if (instance != null || !Application.isPlaying)
            {
                return instance;
            }

            GameObject resolverObject = new GameObject(nameof(HandsResolver));
            instance = resolverObject.AddComponent<HandsResolver>();
            DontDestroyOnLoad(resolverObject);
            return instance;
        }
    }

    public static ResolvedHand LeftHand => Instance != null ? Instance.leftHand : NullLeftHand;
    public static ResolvedHand RightHand => Instance != null ? Instance.rightHand : NullRightHand;

    public ResolvedHand Left => leftHand;
    public ResolvedHand Right => rightHand;
    public bool DebugLoggingEnabled
    {
        get => logDebug;
        set => logDebug = value;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveSources(true);
    }

    private void OnEnable()
    {
        ResolveSources(true);
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (isRefreshing || lastRefreshFrame == Time.frameCount)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            ResolveSources(false);
            UpdateHand(leftHand, leftOvrHand, leftSkeleton, leftGrabInteractor, HandSide.Left);
            UpdateHand(rightHand, rightOvrHand, rightSkeleton, rightGrabInteractor, HandSide.Right);
            lastRefreshFrame = Time.frameCount;
        }
        finally
        {
            isRefreshing = false;
        }
    }

    public ResolvedHand GetHand(HandSide side)
    {
        return side == HandSide.Right ? rightHand : leftHand;
    }

    private void ResolveSources(bool force)
    {
        if (!autoResolveSources)
        {
            return;
        }

        if (!force && Time.unscaledTime < nextSourceRefreshTime)
        {
            return;
        }

        nextSourceRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, sourceRefreshInterval);

        leftOvrHand = leftOvrHand != null ? leftOvrHand : FindSource<OVRHand>(HandSide.Left);
        rightOvrHand = rightOvrHand != null ? rightOvrHand : FindSource<OVRHand>(HandSide.Right);
        leftSkeleton = leftSkeleton != null ? leftSkeleton : FindSource<OVRSkeleton>(HandSide.Left);
        rightSkeleton = rightSkeleton != null ? rightSkeleton : FindSource<OVRSkeleton>(HandSide.Right);
        leftGrabInteractor = leftGrabInteractor != null ? leftGrabInteractor : FindSource<HandGrabInteractor>(HandSide.Left);
        rightGrabInteractor = rightGrabInteractor != null ? rightGrabInteractor : FindSource<HandGrabInteractor>(HandSide.Right);

        LogSourceResolutionSnapshot(force);
    }

    private void UpdateHand(
        ResolvedHand hand,
        OVRHand ovrHand,
        OVRSkeleton skeleton,
        HandGrabInteractor grabInteractor,
        HandSide side)
    {
        hand.ResetFrame();

        if (ovrHand != null)
        {
            ApplyOvrHand(hand, ovrHand);
        }

        if (skeleton != null)
        {
            ApplySkeleton(hand, skeleton);
        }

        ApplyUnityXrInput(hand, side);

        if (grabInteractor != null)
        {
            ApplyGrabInteractor(hand, grabInteractor);
        }

        hand.Gestures.Pinch = hand.Gestures.IndexPinch;
        hand.Gestures.Grab = hand.Gestures.SelectGrab ||
                              hand.Gestures.PalmGrab ||
                              hand.Gestures.ControllerGrip ||
                              hand.Gestures.ControllerTrigger ||
                              IsOvrHandGrabGesture(hand);

        LogHandDebugSnapshot(hand, ovrHand, skeleton, grabInteractor);
    }

    private void ApplyOvrHand(ResolvedHand hand, OVRHand ovrHand)
    {
        hand.ActiveSource |= HandInputSource.OVRHand;
        hand.OvrHand = ovrHand;
        hand.IsTracked |= ovrHand.IsTracked;
        hand.IsDataValid |= ovrHand.IsDataValid;

        hand.Gestures.IndexPinch = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        hand.Gestures.MiddlePinch = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
        hand.Gestures.RingPinch = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Ring);
        hand.Gestures.PinkyPinch = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Pinky);
        hand.Gestures.ThumbPinch = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Thumb);

        hand.Gestures.IndexPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        hand.Gestures.MiddlePinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        hand.Gestures.RingPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        hand.Gestures.PinkyPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);
        hand.Gestures.ThumbPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
    }

    private void ApplySkeleton(ResolvedHand hand, OVRSkeleton skeleton)
    {
        hand.ActiveSource |= HandInputSource.OVRSkeleton;
        hand.Skeleton = skeleton;
        hand.IsTracked |= skeleton.IsDataValid;
        hand.IsDataValid |= skeleton.IsDataValid;

        hand.Joints.Wrist.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_Wrist, OVRSkeleton.BoneId.Hand_WristRoot));
        hand.Joints.Palm.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_Palm, OVRSkeleton.BoneId.Hand_WristRoot));
        hand.Joints.Thumb.Joint0.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, OVRSkeleton.BoneId.Hand_Thumb0));
        hand.Joints.Thumb.Joint1.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_ThumbProximal, OVRSkeleton.BoneId.Hand_Thumb1));
        hand.Joints.Thumb.Joint2.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_ThumbDistal, OVRSkeleton.BoneId.Hand_Thumb2));
        hand.Joints.Thumb.Joint3.Set(FindBone(skeleton, OVRSkeleton.BoneId.Hand_Thumb3));
        hand.Joints.Thumb.Tip.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_ThumbTip, OVRSkeleton.BoneId.Hand_ThumbTip));
        hand.Joints.Index.Joint0.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_IndexMetacarpal));
        hand.Joints.Index.Joint1.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.Hand_Index1));
        hand.Joints.Index.Joint2.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_IndexIntermediate, OVRSkeleton.BoneId.Hand_Index2));
        hand.Joints.Index.Joint3.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_IndexDistal, OVRSkeleton.BoneId.Hand_Index3));
        hand.Joints.Index.Tip.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.Hand_IndexTip));
        hand.Joints.Middle.Tip.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_MiddleTip, OVRSkeleton.BoneId.Hand_MiddleTip));
        hand.Joints.Ring.Tip.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_RingTip, OVRSkeleton.BoneId.Hand_RingTip));
        hand.Joints.Pinky.Tip.Set(FindBone(skeleton, OVRSkeleton.BoneId.XRHand_LittleTip, OVRSkeleton.BoneId.Hand_PinkyTip));
    }

    private void ApplyUnityXrInput(ResolvedHand hand, HandSide side)
    {
        inputDevices.Clear();
        InputDeviceCharacteristics sideFlag = side == HandSide.Right
            ? InputDeviceCharacteristics.Right
            : InputDeviceCharacteristics.Left;

        InputDevices.GetDevicesWithCharacteristics(sideFlag, inputDevices);
        hand.UnityXrDeviceCount = inputDevices.Count;

        for (int i = 0; i < inputDevices.Count; i++)
        {
            InputDevice device = inputDevices[i];
            if (!device.isValid)
            {
                continue;
            }

            hand.ActiveSource |= HandInputSource.UnityXR;
            hand.UnityXrDevice = device;
            hand.UnityXrValidDeviceCount++;
            hand.UnityXrDeviceName = string.IsNullOrWhiteSpace(hand.UnityXrDeviceName)
                ? device.name
                : $"{hand.UnityXrDeviceName}, {device.name}";
            hand.UnityXrDeviceCharacteristics |= device.characteristics;

            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
            {
                hand.HasUnityXrIsTrackedFeature = true;
                hand.IsTracked |= isTracked;
            }

            if (device.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
            {
                hand.HasUnityXrGripAxis = true;
                hand.Gestures.GripStrength = Mathf.Max(hand.Gestures.GripStrength, gripValue);
            }

            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
            {
                hand.HasUnityXrTriggerAxis = true;
                hand.Gestures.TriggerStrength = Mathf.Max(hand.Gestures.TriggerStrength, triggerValue);
            }

            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton))
            {
                hand.HasUnityXrGripButton = true;
                hand.Gestures.ControllerGrip |= gripButton;
            }

            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
            {
                hand.HasUnityXrTriggerButton = true;
                hand.Gestures.ControllerTrigger |= triggerButton;
            }
        }

        hand.Gestures.ControllerGrip |= hand.Gestures.GripStrength >= gripThreshold;
        hand.Gestures.ControllerTrigger |= hand.Gestures.TriggerStrength >= triggerThreshold;
    }

    private void ApplyGrabInteractor(ResolvedHand hand, HandGrabInteractor grabInteractor)
    {
        hand.ActiveSource |= HandInputSource.HandGrabInteractor;
        hand.HandGrabInteractor = grabInteractor;
        hand.Gestures.SelectGrab = grabInteractor.IsGrabbing;

        HandGrabAPI handGrabApi = grabInteractor.HandGrabApi;
        hand.Gestures.PalmGrab = handGrabApi != null &&
                                 handGrabApi.IsHandPalmGrabbing(GrabbingRule.DefaultPalmRule);
    }

    private bool IsOvrHandGrabGesture(ResolvedHand hand)
    {
        return hand.Gestures.MiddlePinch ||
               hand.Gestures.ThumbPinch ||
               hand.Gestures.MiddlePinchStrength >= pinchStrengthThreshold ||
               hand.Gestures.ThumbPinchStrength >= pinchStrengthThreshold;
    }

    private static T FindSource<T>(HandSide side) where T : Component
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsSortMode.None);
        T fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (LooksLikeSide(candidate.transform, side))
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static Transform FindBone(OVRSkeleton skeleton, params OVRSkeleton.BoneId[] ids)
    {
        if (skeleton == null || skeleton.Bones == null)
        {
            return null;
        }

        for (int idIndex = 0; idIndex < ids.Length; idIndex++)
        {
            OVRSkeleton.BoneId id = ids[idIndex];
            for (int boneIndex = 0; boneIndex < skeleton.Bones.Count; boneIndex++)
            {
                OVRBone bone = skeleton.Bones[boneIndex];
                if (bone != null && bone.Id == id)
                {
                    return bone.Transform;
                }
            }
        }

        return null;
    }

    private static bool LooksLikeSide(Transform transform, HandSide side)
    {
        string sideLabel = side == HandSide.Right ? "right" : "left";
        for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
        {
            string name = cursor.name;
            if (!string.IsNullOrWhiteSpace(name) &&
                name.IndexOf(sideLabel, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void LogSourceResolutionSnapshot(bool force)
    {
        if (!logDebug || !logSourceResolution)
        {
            return;
        }

        string snapshot =
            $"force={force}, auto={autoResolveSources}, " +
            $"leftOvr={DescribeComponent(leftOvrHand)}, rightOvr={DescribeComponent(rightOvrHand)}, " +
            $"leftSkeleton={DescribeComponent(leftSkeleton)}, rightSkeleton={DescribeComponent(rightSkeleton)}, " +
            $"leftGrabInteractor={DescribeComponent(leftGrabInteractor)}, rightGrabInteractor={DescribeComponent(rightGrabInteractor)}";

        if (string.Equals(snapshot, lastSourceDebugSnapshot, StringComparison.Ordinal))
        {
            return;
        }

        lastSourceDebugSnapshot = snapshot;
        Debug.Log($"[HandsResolver] Source resolution: {snapshot}.", this);
    }

    private void LogHandDebugSnapshot(
        ResolvedHand hand,
        OVRHand ovrHand,
        OVRSkeleton skeleton,
        HandGrabInteractor grabInteractor)
    {
        if (!logDebug || !logHandState)
        {
            return;
        }

        string snapshot =
            $"source={hand.ActiveSource}, tracked={hand.IsTracked}, valid={hand.IsDataValid}, " +
            $"final(grab={hand.Gestures.Grab}, pinch={hand.Gestures.Pinch}), " +
            $"ovr={DescribeOvrHand(ovrHand, hand)}, " +
            $"skeleton={DescribeSkeleton(skeleton, hand)}, " +
            $"unityXR={DescribeUnityXr(hand)}, " +
            $"handGrab={DescribeGrabInteractor(grabInteractor, hand)}";

        bool changed = !string.Equals(snapshot, hand.LastDebugSnapshot, StringComparison.Ordinal);
        bool due = Time.unscaledTime >= hand.NextDebugLogTime;
        if (!changed && !due)
        {
            return;
        }

        hand.LastDebugSnapshot = snapshot;
        hand.NextDebugLogTime = Time.unscaledTime + Mathf.Max(0.1f, debugLogInterval);
        Debug.Log($"[HandsResolver] {hand.Side}: {snapshot}.", this);
    }

    private static string DescribeOvrHand(OVRHand ovrHand, ResolvedHand hand)
    {
        if (ovrHand == null)
        {
            return "null";
        }

        return $"{ovrHand.name}(tracked={ovrHand.IsTracked}, valid={ovrHand.IsDataValid}, " +
               $"index={hand.Gestures.IndexPinch}/{hand.Gestures.IndexPinchStrength:0.##}, " +
               $"middle={hand.Gestures.MiddlePinch}/{hand.Gestures.MiddlePinchStrength:0.##}, " +
               $"thumb={hand.Gestures.ThumbPinch}/{hand.Gestures.ThumbPinchStrength:0.##})";
    }

    private static string DescribeSkeleton(OVRSkeleton skeleton, ResolvedHand hand)
    {
        if (skeleton == null)
        {
            return "null";
        }

        return $"{skeleton.name}(initialized={skeleton.IsInitialized}, valid={skeleton.IsDataValid}, " +
               $"wrist={hand.Joints.Wrist.HasPose}, palm={hand.Joints.Palm.HasPose}, " +
               $"indexTip={hand.Joints.Index.Tip.HasPose}, thumbTip={hand.Joints.Thumb.Tip.HasPose})";
    }

    private static string DescribeUnityXr(ResolvedHand hand)
    {
        return $"devices={hand.UnityXrDeviceCount}, validDevices={hand.UnityXrValidDeviceCount}, " +
               $"device={(string.IsNullOrWhiteSpace(hand.UnityXrDeviceName) ? "null" : hand.UnityXrDeviceName)}, " +
               $"chars={hand.UnityXrDeviceCharacteristics}, " +
               $"isTrackedFeature={hand.HasUnityXrIsTrackedFeature}, " +
               $"gripAxis={hand.HasUnityXrGripAxis}/{hand.Gestures.GripStrength:0.##}, " +
               $"triggerAxis={hand.HasUnityXrTriggerAxis}/{hand.Gestures.TriggerStrength:0.##}, " +
               $"gripButton={hand.HasUnityXrGripButton}/{hand.Gestures.ControllerGrip}, " +
               $"triggerButton={hand.HasUnityXrTriggerButton}/{hand.Gestures.ControllerTrigger}";
    }

    private static string DescribeGrabInteractor(HandGrabInteractor grabInteractor, ResolvedHand hand)
    {
        if (grabInteractor == null)
        {
            return "null";
        }

        return $"{grabInteractor.name}(select={hand.Gestures.SelectGrab}, palm={hand.Gestures.PalmGrab})";
    }

    private static string DescribeComponent(Component component)
    {
        return component != null ? component.name : "null";
    }

    [Serializable]
    public sealed class ResolvedHand
    {
        public ResolvedHand(HandSide side)
        {
            Side = side;
        }

        public HandSide Side { get; private set; }
        public bool IsTracked { get; internal set; }
        public bool IsDataValid { get; internal set; }
        public HandInputSource ActiveSource { get; internal set; }
        public HandGestures Gestures { get; } = new HandGestures();
        public HandJoints Joints { get; } = new HandJoints();
        public FingerJoints Indexes => Joints.Index;
        public OVRHand OvrHand { get; internal set; }
        public OVRSkeleton Skeleton { get; internal set; }
        public HandGrabInteractor HandGrabInteractor { get; internal set; }
        public InputDevice UnityXrDevice { get; internal set; }
        public int UnityXrDeviceCount { get; internal set; }
        public int UnityXrValidDeviceCount { get; internal set; }
        public string UnityXrDeviceName { get; internal set; }
        public InputDeviceCharacteristics UnityXrDeviceCharacteristics { get; internal set; }
        public bool HasUnityXrIsTrackedFeature { get; internal set; }
        public bool HasUnityXrGripAxis { get; internal set; }
        public bool HasUnityXrTriggerAxis { get; internal set; }
        public bool HasUnityXrGripButton { get; internal set; }
        public bool HasUnityXrTriggerButton { get; internal set; }
        internal string LastDebugSnapshot { get; set; }
        internal float NextDebugLogTime { get; set; }

        internal void ResetFrame()
        {
            IsTracked = false;
            IsDataValid = false;
            ActiveSource = HandInputSource.None;
            OvrHand = null;
            Skeleton = null;
            HandGrabInteractor = null;
            UnityXrDevice = default;
            UnityXrDeviceCount = 0;
            UnityXrValidDeviceCount = 0;
            UnityXrDeviceName = null;
            UnityXrDeviceCharacteristics = InputDeviceCharacteristics.None;
            HasUnityXrIsTrackedFeature = false;
            HasUnityXrGripAxis = false;
            HasUnityXrTriggerAxis = false;
            HasUnityXrGripButton = false;
            HasUnityXrTriggerButton = false;
            Gestures.Reset();
            Joints.Reset();
        }
    }

    [Serializable]
    public sealed class HandGestures
    {
        public bool Grab { get; internal set; }
        public bool Pinch { get; internal set; }
        public bool IndexPinch { get; internal set; }
        public bool MiddlePinch { get; internal set; }
        public bool RingPinch { get; internal set; }
        public bool PinkyPinch { get; internal set; }
        public bool ThumbPinch { get; internal set; }
        public bool SelectGrab { get; internal set; }
        public bool PalmGrab { get; internal set; }
        public bool ControllerGrip { get; internal set; }
        public bool ControllerTrigger { get; internal set; }
        public float GripStrength { get; internal set; }
        public float TriggerStrength { get; internal set; }
        public float IndexPinchStrength { get; internal set; }
        public float MiddlePinchStrength { get; internal set; }
        public float RingPinchStrength { get; internal set; }
        public float PinkyPinchStrength { get; internal set; }
        public float ThumbPinchStrength { get; internal set; }

        public bool AnyActive => Grab || Pinch || ControllerGrip || ControllerTrigger || SelectGrab || PalmGrab;

        internal void Reset()
        {
            Grab = false;
            Pinch = false;
            IndexPinch = false;
            MiddlePinch = false;
            RingPinch = false;
            PinkyPinch = false;
            ThumbPinch = false;
            SelectGrab = false;
            PalmGrab = false;
            ControllerGrip = false;
            ControllerTrigger = false;
            GripStrength = 0f;
            TriggerStrength = 0f;
            IndexPinchStrength = 0f;
            MiddlePinchStrength = 0f;
            RingPinchStrength = 0f;
            PinkyPinchStrength = 0f;
            ThumbPinchStrength = 0f;
        }
    }

    [Serializable]
    public sealed class HandJoints
    {
        public JointPose Wrist { get; } = new JointPose();
        public JointPose Palm { get; } = new JointPose();
        public FingerJoints Thumb { get; } = new FingerJoints();
        public FingerJoints Index { get; } = new FingerJoints();
        public FingerJoints Middle { get; } = new FingerJoints();
        public FingerJoints Ring { get; } = new FingerJoints();
        public FingerJoints Pinky { get; } = new FingerJoints();

        internal void Reset()
        {
            Wrist.Reset();
            Palm.Reset();
            Thumb.Reset();
            Index.Reset();
            Middle.Reset();
            Ring.Reset();
            Pinky.Reset();
        }
    }

    [Serializable]
    public sealed class FingerJoints
    {
        public JointPose Joint0 { get; } = new JointPose();
        public JointPose Joint1 { get; } = new JointPose();
        public JointPose Joint2 { get; } = new JointPose();
        public JointPose Joint3 { get; } = new JointPose();
        public JointPose Tip { get; } = new JointPose();

        public JointPose Index0 => Joint0;
        public JointPose Index1 => Joint1;
        public JointPose Index2 => Joint2;
        public JointPose Index3 => Joint3;

        internal void Reset()
        {
            Joint0.Reset();
            Joint1.Reset();
            Joint2.Reset();
            Joint3.Reset();
            Tip.Reset();
        }
    }

    [Serializable]
    public sealed class JointPose
    {
        public bool HasPose { get; private set; }
        public Transform Transform { get; private set; }
        public Pose Pose { get; private set; }
        public Vector3 Position => Pose.position;
        public Quaternion Rotation => Pose.rotation;

        internal void Set(Transform source)
        {
            Transform = source;
            HasPose = source != null;
            Pose = source != null
                ? new Pose(source.position, source.rotation)
                : default;
        }

        internal void Reset()
        {
            HasPose = false;
            Transform = null;
            Pose = default;
        }
    }
}
