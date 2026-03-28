using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class GrabLidObjective : MonoBehaviour
{
    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;
    private PlayLocalizedAudio localizedAudio;

    private ArmExtensionDetector armExtensionDetector;

    private bool hasGrabbed = false;

    public HandGrabInteractable capsuleHGI;

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
        armExtensionDetector = FindAnyObjectByType<ArmExtensionDetector>();
    }

    // Update is called once per frame
    void Update()
    {
        if ((_interactable != null) && !hasGrabbed)
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
                                PointsManager.Instance.SecondLevelPoints += 1;
                            }
                            else
                                PointsManager.Instance.SecondLevelPoints += 2;
                        }
                        else
                        {
                            if (hand.Handedness == Handedness.Left)
                            {
                                PointsManager.Instance.SecondLevelPoints += 2;
                            }
                            else
                                PointsManager.Instance.SecondLevelPoints += 1;
                        }

                        hasGrabbed = true;
                        if (this.gameObject.CompareTag("JarLid") && capsuleHGI != null)
                            capsuleHGI.enabled = true;
                        armExtensionDetector.SetTimer(20f, true);
                    }
                }
            }
        }
    }
}
