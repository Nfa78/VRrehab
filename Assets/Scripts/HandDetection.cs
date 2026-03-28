using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class HandDetection : MonoBehaviour
{
    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;

    [HideInInspector]
    internal float timeRemaining = 10;
    [HideInInspector]
    internal bool timerIsRunning = false;
    [HideInInspector]
    internal bool done = false;

    [HideInInspector]
    internal GameObject targetObject;
    [HideInInspector]
    internal Transform targetPosition;

    public TMPro.TMP_Text timerTag;

    [HideInInspector]
    internal bool hasGrabbed = false;

    private PlayLocalizedAudio localizedAudio;

    private GameObject thingThisScriptIsAttachedTo;
    public GameObject outline;
    public GameObject knifeOutline;

    private void Awake()
    {
        _interactable = GetComponent<HandGrabInteractable>();

        if (_interactable == null)
        {
            Debug.LogError("HandGrabInteractable not found on " + gameObject.name);
        }
    }

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        thingThisScriptIsAttachedTo = this.transform.parent.gameObject;
    }

    private void Update()
    {
        if (!hasGrabbed)
        {
            if (_interactable != null)
            {
                var interactors = _interactable.Interactors; // List of active interactors

                if (interactors.Count > 0)
                {
                    _interactor = interactors.FirstOrDefault() as HandGrabInteractor;

                    if ((_interactor != null) && _interactor.IsGrabbing)
                    {
                        IHand hand = _interactor.Hand;
                        if (hand != null)
                        {
                            if (thingThisScriptIsAttachedTo.CompareTag("Pan") || this.CompareTag("Pan"))
                                outline.SetActive(false);
                            else if (this.CompareTag("Knife"))
                                knifeOutline.SetActive(false);
                        }
                    }
                }
            }
        }

        if (!timerIsRunning)
            return;

        if (timeRemaining > 0)
        {
            if (!hasGrabbed)
            {
                if (_interactable != null)
                {
                    var interactors = _interactable.Interactors; // List of active interactors

                    if (interactors.Count > 0)
                    {
                        _interactor = interactors.FirstOrDefault() as HandGrabInteractor;

                        if ((_interactor != null) && _interactor.IsGrabbing)
                        {
                            if (thingThisScriptIsAttachedTo.CompareTag("Pan") || this.CompareTag("Pan"))
                                outline.SetActive(false);
                            IHand hand = _interactor.Hand;
                            if (hand != null)
                            {
                                DetectHandGrab(hand);
                            }
                        }
                    }
                }
            }
            timeRemaining -= Time.deltaTime;
            DisplayTime(timeRemaining);
        }
        else
        {
            //MissionFailed();
            StopTimer();
            hasGrabbed = false;
        }
    }

    void DetectHandGrab(IHand hand)
    {
        if (PointsManager.Instance.Arm == "Right")
        {
            if (hand.Handedness == Handedness.Right)
            {
                Success();
            }
        }
        else
        {
            if (hand.Handedness == Handedness.Left)
            {
                Success();
            }
        }
    }

    void Success()
    {
        PointsManager.Instance.Points += 1;
        hasGrabbed = true;
        StopTimer();
    }

    void MissionFailed()
    {
        if (targetObject != null && targetPosition != null)
        {
            StartCoroutine(MoveThingSmoothly(1f));
            localizedAudio?.PlayLocalizedClip(10);
        }
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


    public void SetTimer(float time, GameObject o, Transform p)
    {
        timeRemaining = time;
        targetObject = o;
        targetPosition = p;
        timerIsRunning = true;
        hasGrabbed = false;
        done = true;
    }

    public void StopTimer()
    {
        timeRemaining = 0;
        timerIsRunning = false;
    }

    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerTag.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void CheckHandednessAndUpdatePoints(GameObject obj)
    {
        _interactable = obj.GetComponent<HandGrabInteractable>();
        if (_interactable != null)
        {
            var interactors = _interactable.Interactors; // List of active interactors

            if (interactors.Count > 0)
            {
                _interactor = interactors.FirstOrDefault() as HandGrabInteractor;

                if ((_interactor != null) && _interactor.IsGrabbing)
                {
                    IHand hand = _interactor.Hand;
                    if (hand != null)
                    {
                        if (PointsManager.Instance.Arm == "Right")
                        {
                            if (hand.Handedness == Handedness.Right)
                            {
                                PointsManager.Instance.Points += 1;
                            }
                            else
                            {
                                localizedAudio?.PlayLocalizedClip(7);
                            }
                        }
                        else
                        {
                            if (hand.Handedness == Handedness.Left)
                            {
                                PointsManager.Instance.Points += 1;
                            }
                            else
                            {
                                localizedAudio?.PlayLocalizedClip(7);
                            }
                        }
                    }
                }
            }
        }
    }
}
/*using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class HandDetection : MonoBehaviour
{
    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;

    [HideInInspector]
    internal float timeRemaining = 10;
    [HideInInspector]
    internal bool timerIsRunning = false;

    [HideInInspector]
    internal GameObject targetObject;
    [HideInInspector]
    internal Transform targetPosition;

    public TMPro.TMP_Text timerTag;

    [HideInInspector]
    internal bool hasGrabbed = false;

    [HideInInspector]
    public bool enablePlacementCheck = false;
    [HideInInspector]
    public bool placementChecked = false;

    private bool wasGrabbingLastFrame = false; // Track state changes

    private void Start()
    {
        _interactable = GetComponent<HandGrabInteractable>();

        if (_interactable == null)
        {
            Debug.LogError("HandGrabInteractable not found on " + gameObject.name);
        }
    }

    private void Update()
    {
        // Timer Logic
        if (timerIsRunning)
        {
            HandleTimer();
        }
        else if(!timerIsRunning && enablePlacementCheck && !placementChecked)
        {
            // Grab/Release Detection Logic
            if (_interactable != null)
            {
                var interactors = _interactable.Interactors;
                if (interactors.Count > 0)
                {
                    _interactor = interactors.FirstOrDefault() as HandGrabInteractor;
                    if (_interactor != null)
                    {
                        CheckHandGrabState();
                    }
                }
            }
        }
    }

    private void HandleTimer()
    {
        if (timeRemaining > 0)
        {
            if (!hasGrabbed)
            {
                DetectHandGrab();
            }

            timeRemaining -= Time.deltaTime;
            DisplayTime(timeRemaining);
        }
        else
        {
            MissionFailed();
            StopTimer();
            hasGrabbed = false;
        }
    }

    private void CheckHandGrabState()
    {
        if (_interactor.IsGrabbing && !wasGrabbingLastFrame)
        {
            // Grab started
            wasGrabbingLastFrame = true;
            //hasGrabbed = true;
            Debug.Log("Object grabbed");
        }
        else if (!_interactor.IsGrabbing && wasGrabbingLastFrame)
        {
            // Grab ended
            wasGrabbingLastFrame = false;
            //hasGrabbed = false;
            Debug.Log("Object released");
            OnHandRelease();
        }
    }

    void DetectHandGrab()
    {
        if (_interactor != null)
        {
            IHand hand = _interactor.Hand;

            // Check if hand is actually grabbing the object (not just near it)
            if (_interactor.IsGrabbing)
            {
                if (hand != null)
                {
                    if (PointsManager.Instance.Arm == "Right" && hand.Handedness == Handedness.Right)
                    {
                        Success();
                    }
                    else if (PointsManager.Instance.Arm == "Left" && hand.Handedness == Handedness.Left)
                    {
                        Success();
                    }
                }
            }
        }
    }

    void Success()
    {
        PointsManager.Instance.Points += 1;
        hasGrabbed = true;
        StopTimer();
    }

    void MissionFailed()
    {
        if (targetObject != null && targetPosition != null)
        {
            targetObject.transform.position = targetPosition.position;
            targetObject.transform.rotation = targetPosition.rotation;
        }
    }

    public void SetTimer(float time, GameObject o, Transform p) 
    {
        timeRemaining = time;
        targetObject = o;
        targetPosition = p;
        hasGrabbed = false;
        placementChecked = false;
        timerIsRunning = true;
    }

    public void StopTimer()
    {
        timeRemaining = 0;
        timerIsRunning = false;
    }

    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerTag.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void OnHandRelease()
    {
        Debug.Log("Object released from hand!");

        if(enablePlacementCheck)
        {
            // Trigger your placement check or scoring logic here
            ScorePlacementCheck();
            enablePlacementCheck = false;
        }
    }

    private void ScorePlacementCheck()
    {
        placementChecked = true;
        // Perform your placement logic
        if (Vector3.Distance(targetObject.transform.position, targetPosition.position) < 0.1f)
        {
            Debug.Log("Object successfully placed!");
            PointsManager.Instance.Points += 2;
        }
        else if(Vector3.Distance(targetObject.transform.position, targetPosition.position) < 0.2f)
        {
            Debug.Log("Object not placed correctly.");
            PointsManager.Instance.Points += 1;

        }
        else
        {
            targetObject.transform.position = targetPosition.position;
            targetObject.transform.rotation = targetPosition.rotation;
        }
    }

}
*/