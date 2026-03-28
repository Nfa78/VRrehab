using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class ArmExtensionDetector : MonoBehaviour
{
    public Transform headTransform;   // Assign the HMD transform (e.g., Camera.main.transform)
    public Transform leftHandTransform;  // Assign left controller transform
    public Transform rightHandTransform; // Assign right controller transform
    public float extensionThreshold = 0.5f; // Adjust this value based on real-world testing
    [HideInInspector]
    internal bool hasExtendedArm = false;
    [HideInInspector]
    internal bool hasContractedArm = false;
    private bool lookingForExtension = true;

    [HideInInspector]
    internal float timeRemaining = 20;
    [HideInInspector]
    internal bool timerIsRunning = false;

    private GameObject targetObject;
    private Transform targetPosition;

    public TMPro.TMP_Text timerTag;

    private PlayLocalizedAudio localizedAudio;

    private float startTime;
    private float elapsedTime;
    private bool loggedMissingRefs;

    void Start()
    {
        timerIsRunning = true;
        startTime = Time.time;
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    void Update()
    {
        if (!timerIsRunning)
            return;
        if (!HasRequiredReferences())
            return;
        if(timerIsRunning)
        {
            if(timeRemaining > 0)
            {
                elapsedTime = Time.time - startTime;
                if (lookingForExtension && !hasExtendedArm)
                {
                    DetectArmExtension();
                }
                else if(!lookingForExtension && !hasContractedArm)
                {
                    DetectArmContraction();
                }
                timeRemaining -= Time.deltaTime;
                DisplayTime(elapsedTime); ///////
            }
            else
            {
                Debug.Log("Time has run out!");
                //MissionFailed();
                timeRemaining = 0;
                timerIsRunning = false;
                hasExtendedArm = false;
                hasContractedArm = false;
            }
        }
    }

    void DetectArmExtension()
    {
        if (hasExtendedArm) return; // Prevents multiple calls
        if (!HasRequiredReferences())
            return;

        float rightHandDistance = Vector3.Distance(rightHandTransform.position, headTransform.position);
        bool isRightHandExtended = rightHandDistance > extensionThreshold;
        float leftHandDistance = Vector3.Distance(leftHandTransform.position, headTransform.position);
        bool isLeftHandExtended = leftHandDistance > extensionThreshold;

        if (PointsManager.Instance.Arm == "Right")
        {
            if (isRightHandExtended)
            {
                Success();
            } 
            /*else if(isLeftHandExtended)
            {
                localizedAudio.PlayLocalizedClip(8);
            }*/
        }
        else
        {
            if (isLeftHandExtended)
            {
                Success();
            } 
            /*else if(isRightHandExtended)
            {
                localizedAudio.PlayLocalizedClip(8);
            }*/
        }
    }

    void DetectArmContraction()
    {
        if (hasContractedArm) return; // Prevents multiple calls
        if (!HasRequiredReferences())
            return;

        if (PointsManager.Instance.Arm == "Right")
        {
            float rightHandDistance = Vector3.Distance(rightHandTransform.position, headTransform.position);
            bool isRightHandContracted = rightHandDistance < extensionThreshold;
            if (isRightHandContracted)
            {
                Success();
            }
        }
        else
        {
            float leftHandDistance = Vector3.Distance(leftHandTransform.position, headTransform.position);
            bool isLeftHandContracted = leftHandDistance < extensionThreshold;
            if (isLeftHandContracted)
            {
                Success();
            }
        }
    }

    void Success()
    {
        string lvlName = SceneManager.GetActiveScene().name;
        if (lvlName == "ApartmentScene")
            PointsManager.Instance.Points += 1;
        else
        {
            if(elapsedTime <= 10)
                PointsManager.Instance.SecondLevelPoints += 2;
            else if(elapsedTime > 10 && elapsedTime <= 20)
                PointsManager.Instance.SecondLevelPoints += 1;
        }
        if (lookingForExtension)
            hasExtendedArm = true;
        else
            hasContractedArm = true;
        timeRemaining = 0;
        timerIsRunning = false;
    }

    void MissionFailed()
    {
        if(targetObject != null && targetPosition != null)
        {
            StartCoroutine(MoveThingSmoothly(1f));
        }
        localizedAudio?.PlayLocalizedClip(10);
    }

    private IEnumerator MoveThingSmoothly(float duration)
    {
        Vector3 startPosition = targetObject.transform.position;
        Quaternion startRotation = targetObject.transform.rotation;
        Vector3 endPosition = targetPosition.position;
        Quaternion endRotation = targetPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            targetObject.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            targetObject.transform.rotation = Quaternion.Lerp(startRotation, endRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        targetObject.transform.position = endPosition;
        targetObject.transform.rotation = endRotation;
    }


    public void SetTimer(float time, GameObject o, Transform p, bool e) // e = true if extension exercise, false if contraction exercise
    {
        startTime = Time.time;
        timeRemaining = time;
        timerIsRunning = true;
        targetObject = o;
        targetPosition = p;
        lookingForExtension = e;
        hasExtendedArm = false;
        hasContractedArm = false;
    }

    public void SetTimer(float time, bool e)
    {
        startTime = Time.time;
        timeRemaining = time;
        timerIsRunning = true;
        lookingForExtension = e;
        hasExtendedArm = false;
        hasContractedArm = false;
    }

    void DisplayTime(float timeToDisplay)
    {
        if (timerTag == null)
            return;

        timeToDisplay += 1;
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerTag.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    bool HasRequiredReferences()
    {
        if (headTransform == null || leftHandTransform == null || rightHandTransform == null || PointsManager.Instance == null)
        {
            if (!loggedMissingRefs)
            {
                Debug.LogWarning($"{nameof(ArmExtensionDetector)}: Missing transforms or PointsManager.", this);
                loggedMissingRefs = true;
            }
            return false;
        }

        return true;
    }
}
