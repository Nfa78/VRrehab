using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class UnscrewBottleLid : MonoBehaviour
{
    float initialAngle;
    public float targetAngle = 400f;
    public float rotationThreshold = 1f;
    public OneGrabRotateTransformer hingeScript;

    // Start is called before the first frame update
    void Start()
    {
        initialAngle = transform.localEulerAngles.z;
    }

    // Update is called once per frame
    void Update()
    {
        // Get the GameObject's current rotation angle around the up axis (Y-axis)
        float currentAngle = transform.localEulerAngles.z;

        // Normalize the angle between 0 and 360
        //currentAngle = NormalizeAngle(currentAngle);

        // Check if the current angle is within the target range
        if (Mathf.Abs(currentAngle - targetAngle) <= rotationThreshold)
        {
            // Disable Script A and enable Script B
            if (hingeScript != null) hingeScript.enabled = false;

            // Optional: Disable this script to stop further checks
            enabled = false;
        }
    }

    // Normalize an angle to the range [0, 360)
    private float NormalizeAngle(float angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }
}
