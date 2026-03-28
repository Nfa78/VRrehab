using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class LeverRotationDetection : MonoBehaviour
{
    public AudioSource success;
    public Image checkbox01;
    public Image checkbox02;
    public Sprite checkedBox;
    private PlayLocalizedAudio localizedAudio;
    private ArmExtensionDetector armExtensionDetector;

    public GameObject welcomeText;
    public GameObject removeLidText;
    public GameObject putCapsuleText;
    public GameObject openCupboardText;

    public GameObject capsuleOutline;

    [Tooltip("Axis of rotation. Use (1,0,0) for X, (0,1,0) for Y, (0,0,1) for Z.")]
    public Vector3 rotationAxis = new Vector3(0, 1, 0); // Default to X

    [Tooltip("Target angle in degrees (e.g. -60)")]
    public float targetAngle = -60f;

    [Tooltip("How close the current angle must be to the target to trigger (degrees)")]
    public float angleThreshold = 1f;

    [Tooltip("Only trigger once when angle is reached")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private GameObject outline;

    public SnapEventsHandler capsuleSnapEventsHandler;

    public GameObject cupboardArrow;
    public GameObject plateOutline;

    private bool hasPulledDownLever = false;

    private float startTime;
    private float elapsedTime;

    private Quaternion initialRotation;

    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;
    private bool hasUsedWrongHand = false;

    public HandGrabInteractable lidHGI;

    private void Awake()
    {
        _interactable = GetComponentInChildren<HandGrabInteractable>();

        if (_interactable == null)
        {
            Debug.LogError("HandGrabInteractable not found on " + gameObject.name);
        }
    }

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        armExtensionDetector = FindAnyObjectByType<ArmExtensionDetector>();
        Transform childTransform = transform.Find("LeverOutline");

        if (childTransform != null)
        {
            outline = childTransform.gameObject;
        }

        initialRotation = childTransform.localRotation;
    }

    void Update()
    {
        float angleDifference = Quaternion.Angle(initialRotation, transform.localRotation);
        Debug.Log($"Lever angle difference: {angleDifference}");

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
                            if (hand.Handedness == Handedness.Left)
                            {
                                if (!localizedAudio.IsClipPlaying())
                                    localizedAudio?.PlayLocalizedClip(7);
                                hasUsedWrongHand = true;
                            }
                        }
                        else
                        {
                            if (hand.Handedness == Handedness.Right)
                            {
                                if (!localizedAudio.IsClipPlaying())
                                    localizedAudio?.PlayLocalizedClip(7);
                                hasUsedWrongHand = true;
                            }
                        }
                    }
                }
            }

            if (!hasTriggered && (Mathf.Abs(angleDifference - targetAngle) <= angleThreshold) && (interactors.Count == 0))
            {
                Debug.Log("Lever reached target angle!");
                OnLeverFullyRotated();

                if (triggerOnce)
                    hasTriggered = true;
            }

            if (capsuleSnapEventsHandler.hasPlayed && hasTriggered && (angleDifference <= 91) && !hasPulledDownLever && (interactors.Count == 0))
            {
                OnLeverPulledDown();
                hasPulledDownLever = true;
            }
        }
    }

    float GetCurrentRotationOnAxis()
    {
        Vector3 localEuler = transform.localEulerAngles;

        float angle = 0f;

        // Use normalized axis detection
        if (rotationAxis == Vector3.right)
            angle = Mathf.DeltaAngle(0, localEuler.x);
        else if (rotationAxis == Vector3.up)
            angle = Mathf.DeltaAngle(0, localEuler.y);
        else if (rotationAxis == Vector3.forward)
            angle = Mathf.DeltaAngle(0, localEuler.z);
        else
            Debug.LogWarning("Unsupported rotationAxis. Use Vector3.right, .up, or .forward.");

        return angle;
    }

    void OnLeverFullyRotated()
    {
        success.Play();
        checkbox01.sprite = checkedBox;
        outline.SetActive(false);
        capsuleOutline.SetActive(true);
        elapsedTime = Time.time - startTime;
        CalculatePoints();
        armExtensionDetector.SetTimer(20f, true);
        lidHGI.enabled = true;
        StartCoroutine(PlayAudioWithDelay(1f, 1));
    }

    void OnLeverPulledDown()
    {
        success.Play();
        checkbox02.sprite = checkedBox;
        cupboardArrow.SetActive(true);
        plateOutline.SetActive(true);
        CalculatePoints();
        armExtensionDetector.SetTimer(20f, true);
        StartCoroutine(PlayAudioWithDelay(1f, 3));
    }

    IEnumerator PlayAudioWithDelay(float delay, int index)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(index);
        if(index == 1)
        {
            welcomeText.SetActive(false);
            removeLidText.SetActive(true);
        }
        else if(index == 3)
        {
            putCapsuleText.SetActive(false);
            openCupboardText.SetActive(true);
        }
    }

    void CalculatePoints()
    {
        if (elapsedTime <= 10)
        {
            if(hasUsedWrongHand)
                PointsManager.Instance.SecondLevelPoints += 1;
            else
                PointsManager.Instance.SecondLevelPoints += 2;
        }
        else if (elapsedTime > 10 && elapsedTime <= 20)
            if(!hasUsedWrongHand)
                PointsManager.Instance.SecondLevelPoints += 1;
    }

    public void StartRotation()
    {
        startTime = Time.time;
    }
}
