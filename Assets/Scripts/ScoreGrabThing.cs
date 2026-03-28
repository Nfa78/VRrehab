using UnityEngine;

public class ScoreGrabThing : MonoBehaviour
{
    public ArmExtensionDetector armExtensionDetector;
    [HideInInspector]
    public HandDetection handDetection;

    private GameObject targetObject;
    private Transform targetPosition;

    [HideInInspector]
    public bool armExtensionSuccess = false; // Accessible but hidden in Inspector
    [HideInInspector]
    public bool succeeded = false; // Accessible but hidden in Inspector

    public void DoYourThing(GameObject thing, HandDetection thingHandDetectionComponent, Transform targetTransform)
    {
        if (armExtensionDetector == null)
        {
            Debug.LogError("Assign ArmExtensionDetector and HandDetection in the inspector.");
            return;
        }

        targetObject = thing;
        handDetection = thingHandDetectionComponent;
        targetPosition = targetTransform;
        armExtensionSuccess = false;
        succeeded = false;

        // Start first timer (Arm Extension)
        armExtensionDetector.SetTimer(20, thing, targetTransform, true);
    }

    private void Update()
    {
        // Check Arm Extension success
        if (!armExtensionDetector.timerIsRunning && armExtensionDetector.hasExtendedArm && !armExtensionSuccess)
        {
            armExtensionSuccess = true;
            if (!handDetection.timerIsRunning)
                // Start second timer (Hand Detection)
                handDetection.SetTimer(10, targetObject, targetPosition);
        }

        // Check Hand Detection success
        if (!handDetection.timerIsRunning && handDetection.done && !succeeded)
        {
            succeeded = true;
        }
    }
}
