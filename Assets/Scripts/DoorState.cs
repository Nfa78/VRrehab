using UnityEngine;

// Script to check whether a door is opened or closed

public class DoorState : MonoBehaviour
{
    public HingeJoint hinge; // Reference to the hinge component
    public float closedAngle = -90f; // The angle when the door is closed
    public float openThreshold = 10f; // Angle deviation to consider the door open
    public bool isDoorClosed = true; // Tracks if the door is closed

    void Update()
    {
        // Check the current angle of the hinge
        float currentAngle = hinge.angle;
        
        // Determine if the door is closed
        isDoorClosed = Mathf.Abs(currentAngle - closedAngle) < openThreshold;
    }
}

