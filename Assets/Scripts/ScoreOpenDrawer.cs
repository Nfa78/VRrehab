using UnityEngine;

public class ScoreOpenDrawer : MonoBehaviour
{
    public ArmExtensionDetector armExtensionDetector;
    public HandDetection handDetection;

    public GameObject drawer;
    public Transform targetPosition;

    private bool armExtensionSuccess = false;
    private bool handGrabSuccess = false;
    private bool armContractionStarted = false;

    [HideInInspector]
    public bool done = false;

    /*private void Start()
    {
        DoYourThing();
    }*/

    private void Update()
    {
        if (!done)
        {
            // Check for Arm Extension success
            if (!armExtensionDetector.timerIsRunning && armExtensionDetector.hasExtendedArm && !armExtensionSuccess)
            {
                armExtensionSuccess = true;
                Debug.Log("-----Arm Extension Success-----");

                // Start Hand Detection timer
                handDetection.SetTimer(10, drawer, targetPosition);
            }

            // Check for Hand Grab success
            if (!handDetection.timerIsRunning && handDetection.hasGrabbed && !handGrabSuccess)
            {
                handGrabSuccess = true;
                armContractionStarted = true;
                Debug.Log("-----Hand Grab Success-----");

                // Start Arm Contraction exercise
                armExtensionDetector.SetTimer(15, drawer, targetPosition, false);
            }

            // Check for Arm Contraction success
            if (!armExtensionDetector.timerIsRunning && armContractionStarted)
            {
                Debug.Log("-----Final Timer Complete-----");
                done = true;
            }
        }
    }

    public void DoYourThing()
    {
        armExtensionSuccess = false;
        handGrabSuccess = false;
        armContractionStarted = false;
        done = false;
        if (armExtensionDetector == null || handDetection == null)
        {
            Debug.LogError("Assign ArmExtensionDetector and HandDetection in the inspector.");
            return;
        }

        // Start the first exercise
        armExtensionDetector.SetTimer(20, drawer, targetPosition, true);
    }
}
