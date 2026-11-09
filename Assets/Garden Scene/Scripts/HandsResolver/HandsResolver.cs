using System;
using System.Collections.Generic;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;


public class Hand
{
    public enum Pose { None, Open, Grab, Pinch }

    public Pose pose = Pose.None;
    public Vector3 velocity = Vector3.zero;
    public Transform TrackedAnchor;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;

    public Hand(Transform trackedObject)
    {
        TrackedAnchor = trackedObject;
    }
    public void setPose(Pose p)
    {
        pose = p;
    }
    public Pose getPose()
    {
        return pose;
    }

    public void setVelocity(Vector3 v)
    {
        velocity = v;
    }

    public void UpdateVelocity(float deltaTime)
    {
        if (TrackedAnchor == null || deltaTime <= 0f)
        {
            setVelocity(Vector3.zero);
            return;
        }

        Vector3 currentPosition = TrackedAnchor.position;
        setVelocity(hasPreviousPosition
            ? (currentPosition - previousPosition) / deltaTime
            : Vector3.zero);
        previousPosition = currentPosition;
        hasPreviousPosition = true;
    }
}
public class Hands
{
    public Hand right = new Hand(null);
    public Hand left = new Hand(null);


}

[DefaultExecutionOrder(-250)]
public class HandsResolver : MonoBehaviour
{
    public static Hands hands;
    [SerializeField] private OVRHand rightOvrHand;
    [SerializeField] private OVRHand leftOvrHand;
    [SerializeField] private HandGrabInteractor rightGrabInteractor;
    [SerializeField] private HandGrabInteractor leftGrabInteractor;
    [SerializeField] private HandGrabAPI rightHandGrabAPI;
    [SerializeField] private HandGrabAPI leftHandGrabAPI;
    [SerializeField, Range(0f, 1f)] private float palmGrabScoreThreshold = 0.55f;
    [SerializeField] private bool logPoseChanges = true;
    private Hand.Pose lastRightPose = Hand.Pose.None;
    private Hand.Pose lastLeftPose = Hand.Pose.None;

    private void Start()
    {
        hands = new Hands();
        GameObject temp = GameObject.Find("RightHandAnchor");
        if (temp != null) hands.right = new Hand(temp.transform);
        else Debug.LogError("RightHandAnchor was not found.");

        temp = GameObject.Find("LeftHandAnchor");
        if (temp != null) hands.left = new Hand(temp.transform);
        else Debug.LogError("LeftHandAnchor was not found.");

        rightOvrHand ??= FindSideComponent<OVRHand>("right");
        leftOvrHand ??= FindSideComponent<OVRHand>("left");
        rightGrabInteractor ??= FindSideComponent<HandGrabInteractor>("right");
        leftGrabInteractor ??= FindSideComponent<HandGrabInteractor>("left");
        rightHandGrabAPI ??= FindSideComponent<HandGrabAPI>("right");
        leftHandGrabAPI ??= FindSideComponent<HandGrabAPI>("left");
    }

    private void FixedUpdate()
    {
        if (hands == null) return;

        hands.right.UpdateVelocity(Time.fixedDeltaTime);
        SetPose(
            hands.right,
            GetPose(rightOvrHand, rightGrabInteractor, rightHandGrabAPI, palmGrabScoreThreshold),
            "Right",
            ref lastRightPose);

        hands.left.UpdateVelocity(Time.fixedDeltaTime);
        SetPose(
            hands.left,
            GetPose(leftOvrHand, leftGrabInteractor, leftHandGrabAPI, palmGrabScoreThreshold),
            "Left",
            ref lastLeftPose);
    }

    private void SetPose(Hand hand, Hand.Pose pose, string side, ref Hand.Pose lastPose)
    {
        hand.setPose(pose);
        if (logPoseChanges && pose != lastPose)
        {
            Debug.Log($"[HandsResolver] {side} hand pose: {lastPose} -> {pose}", this);
            lastPose = pose;
        }
    }

    private static Hand.Pose GetPose(
        OVRHand hand,
        HandGrabInteractor grab,
        HandGrabAPI handGrabAPI,
        float palmGrabScoreThreshold)
    {
        if (hand == null || !hand.IsTracked) return Hand.Pose.None;
        if (hand.GetFingerIsPinching(OVRHand.HandFinger.Index)) return Hand.Pose.Pinch;

        if (grab != null && grab.IsGrabbing)
        {
            return Hand.Pose.Grab;
        }

        HandGrabAPI palmGrabAPI = handGrabAPI != null
            ? handGrabAPI
            : grab != null
                ? grab.HandGrabApi
                : null;

        if (palmGrabAPI != null)
        {
            float palmGrabScore = palmGrabAPI.GetHandPalmScore(GrabbingRule.DefaultPalmRule, true);
            if (palmGrabScore >= palmGrabScoreThreshold)
            {
                return Hand.Pose.Grab;
            }
        }

        return Hand.Pose.Open;
    }

    private static T FindSideComponent<T>(string side) where T : Component
    {
        foreach (T component in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            for (Transform current = component.transform; current != null; current = current.parent)
                if (current.name.IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0) return component;
        }
        return null;
    }

}
