using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.SubsystemsImplementation;
using System.Collections.Generic;

public class HandGrabWithShape : MonoBehaviour
{
    public AudioSource audioSource;
    private XRHandSubsystem handSubsystem;

    private void OnEnable()
    {
        // Find the active XRHandSubsystem
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
        }
    }

    private void Update()
    {
        if (handSubsystem == null) return;

        var leftHand = handSubsystem.leftHand;
        var rightHand = handSubsystem.rightHand;

        if (leftHand.isTracked)
        {
            DetectGrab(leftHand, "Left");
        }

        if (rightHand.isTracked)
        {
            DetectGrab(rightHand, "Right");
        }
    }

    private void DetectGrab(XRHand hand, string handName)
    {
        // Check if key joints are curled
        float thumbCurl = CalculateFingerCurl(hand, XRHandJointID.ThumbProximal, XRHandJointID.ThumbTip);
        float indexCurl = CalculateFingerCurl(hand, XRHandJointID.IndexProximal, XRHandJointID.IndexTip);
        float middleCurl = CalculateFingerCurl(hand, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip);

        if (thumbCurl > 0.8f && indexCurl > 0.8f && middleCurl > 0.8f)
        {
            Debug.Log($"{handName} hand is grabbing!");
            audioSource.Play();
        }
    }

    private float CalculateFingerCurl(XRHand hand, XRHandJointID proximalJointID, XRHandJointID tipJointID)
    {
        // Get joint poses
        XRHandJoint proximalJoint = hand.GetJoint(proximalJointID);
        XRHandJoint tipJoint = hand.GetJoint(tipJointID);

        if (proximalJoint.TryGetPose(out Pose proximalPose) && tipJoint.TryGetPose(out Pose tipPose))
        {
            // Compute curl based on joint direction
            Vector3 jointDirection = (tipPose.position - proximalPose.position).normalized;
            return Vector3.Dot(proximalPose.forward, jointDirection); // Dot product for curl
        }

        return 0f;
    }
}
