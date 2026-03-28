using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class SnapEventsHandler : MonoBehaviour
{
    private SnapInteractor _snapInteractor;
    private SnapInteractable _lastInteractable;

    private PlayLocalizedAudio localizedAudio;

    public AudioSource success;
    public GameObject machineArrow;

    [HideInInspector]
    public bool hasPlayed = false;

    public Image checkbox03; // Take a saucer and put it under the dispenser
    public Image checkbox04; // Take a cup and put it on the saucer
    public Sprite checkedBox;

    public GameObject cupOutline;
    public GameObject buttonsOutline;

    public GameObject takePlateText;
    public GameObject takeCupText;

    public LeverRotationDetection leverRotationDetection;

    private ArmExtensionDetector armExtensionDetector;

    public GameObject buttonsArrow;

    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;

    void Awake()
    {
        _interactable = GetComponentInChildren<HandGrabInteractable>();

        if (_interactable == null)
        {
            Debug.LogError("HandGrabInteractable not found on " + gameObject.name);
        }

        armExtensionDetector = FindAnyObjectByType<ArmExtensionDetector>();
    }

    IEnumerator Start()
    {
        if (gameObject.CompareTag("Plate"))
            Debug.Log("Start called from plate");
        else if (gameObject.CompareTag("Cup"))
            Debug.Log("Start called from cup");
        else
            Debug.Log("Start called from coffee capsule");
        _snapInteractor = GetComponent<SnapInteractor>();

        while (localizedAudio == null)
        {
            localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
            yield return null; // Wait until found
        }
        if (gameObject.CompareTag("Plate"))
            Debug.Log("Plate finally found localizedAudio");
        else if (gameObject.CompareTag("Cup"))
            Debug.Log("Cup finally found localizedAudio");
        else
            Debug.Log("Coffee capsule finally found localizedAudio");
    }
    private Coroutine waitForReleaseCoroutine;

    void Update()
    {
        if (_snapInteractor == null) return;

        var current = _snapInteractor.Interactable;

        if (current != _lastInteractable)
        {
            // Snapped
            if (current != null)
            {
                Debug.Log($"Snapped to: {current.name}");
                if (waitForReleaseCoroutine != null)
                    StopCoroutine(waitForReleaseCoroutine);
                waitForReleaseCoroutine = StartCoroutine(WaitUntilReleasedWhileSnapped());
            }
            // Unsnapped
            else
            {
                Debug.Log($"Unsnapped from: {_lastInteractable?.name}");
                if (waitForReleaseCoroutine != null)
                {
                    StopCoroutine(waitForReleaseCoroutine); // Cancel logic if user moved it away before releasing
                    waitForReleaseCoroutine = null;
                }
            }

            _lastInteractable = current;
        }
    }

    IEnumerator WaitUntilReleasedWhileSnapped()
    {
        Debug.Log("Waiting for release while snapped...");

        // Wait until no interactors are holding this object
        while (_interactable.Interactors.Count > 0)
        {
            // If it's unsnapped while still being held, cancel
            if (_snapInteractor.Interactable == null)
            {
                Debug.Log("Object was moved away before releasing — cancelling.");
                yield break;
            }

            yield return null;
        }

        // Now object is released — check again if it's still snapped
        if (_snapInteractor.Interactable != null && !hasPlayed)
        {
            hasPlayed = true;
            machineArrow.SetActive(false);

            if (gameObject.CompareTag("CoffeeCapsule"))
                leverRotationDetection.StartRotation();
            else if (gameObject.CompareTag("Plate"))
                OnPlatePositioned();
            else if (gameObject.CompareTag("Cup"))
                OnCupPositioned();
        }
    }


    IEnumerator PlayAudioWithDelay(float delay, int index)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(index);
        takePlateText.SetActive(false);
        takeCupText.SetActive(true);
    }

    void OnPlatePositioned()
    {
        success.Play();
        checkbox03.sprite = checkedBox;
        cupOutline.SetActive(true);
        StartCoroutine(PlayAudioWithDelay(1f, 4));
        armExtensionDetector.SetTimer(20f, true);
    }

    void OnCupPositioned()
    {
        success.Play();
        checkbox04.sprite = checkedBox;
        buttonsOutline.SetActive(true);
        buttonsArrow?.SetActive(true);
        StartCoroutine(PlayAudioWithDelay(1f, 5));
        armExtensionDetector.SetTimer(20f, true);
    }
}

