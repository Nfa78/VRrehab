using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuHandler : MonoBehaviour
{
    public Camera sceneCamera;
    public OVRHand leftHand;
    public OVRHand rightHand;
    public GameObject menu;

    private bool menuEnabled = false;

    void Update()
    {
        if (IsHandPinchingAndFacingCamera(leftHand) || IsHandPinchingAndFacingCamera(rightHand))
        {
            menuEnabled = !menuEnabled;
            menu.SetActive(menuEnabled);
        }
    }

    private bool IsHandPinchingAndFacingCamera(OVRHand hand)
    {
        if (hand == null || !hand.IsTracked)
            return false;

        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        if (!isPinching)
            return false;

        // Check if palm is facing the camera
        Vector3 handForward = hand.transform.forward;  // Hand's forward direction
        Vector3 toCamera = (sceneCamera.transform.position - hand.transform.position).normalized; // Direction to the camera

        float dotProduct = Vector3.Dot(handForward, toCamera);

        return dotProduct > 0.9f; // Adjust this threshold as needed (closer to 1 means more directly facing)
    }
}
