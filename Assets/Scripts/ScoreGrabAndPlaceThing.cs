using UnityEngine;

public class ScoreGrabAndPlaceThing : MonoBehaviour
{
    public ScoreGrabThing scoreGrabThing;

    private Transform targetPosition;
    public float placementThreshold = 0.2f; // Adjust as needed
    public float maxDistanceThreshold = 0.5f;

    [HideInInspector]
    public bool succeeded = false; // Accessible but hidden in Inspector

    private HandDetection thingHDComponent;

    public TMPro.TMP_Text succeededTag;

    public void DoYourThing(GameObject thing, HandDetection thingHandDetectionComponent, Transform targetTransform)
    {
        succeeded = false;

        if (scoreGrabThing == null)
        {
            Debug.LogError("Assign ScoreGrabThing in the inspector.");
            return;
        }

        targetPosition = targetTransform;
        thingHDComponent = thingHandDetectionComponent;
        //thingHDComponent.enablePlacementCheck = true; //////////////////
        // Start ScoreGrabThing sequence
        scoreGrabThing.DoYourThing(thing, thingHandDetectionComponent, targetTransform);
    }

    private void Update()
    {
        succeededTag.text = succeeded.ToString();
        // Wait for ScoreGrabThing to succeed first
        if (!succeeded && scoreGrabThing.succeeded /*&& thingHDComponent.placementChecked*/)
        {
            succeeded = true;
            //thingHDComponent.enablePlacementCheck = false; //////////////////
        }
    }

    /*private void CheckPlacement()
    {
        placementChecked = true;

        GameObject grabbedObject = scoreGrabThing.handDetection.targetObject;

        if (grabbedObject == null)
        {
            Debug.LogWarning("No object was grabbed.");
            return;
        }

        // Check if grabbed object is near target position
        float distance = Vector3.Distance(grabbedObject.transform.position, targetPosition.position);

        if (distance <= placementThreshold)
        {
            Debug.Log("Object successfully placed!");
            PointsManager.Instance.Points += 2;
            succeeded = true;
        }
        else if (distance <= maxDistanceThreshold)
        {
            Debug.Log("Object not placed correctly.");
            PointsManager.Instance.Points += 1;
            succeeded = false;
        }
    }*/
}
