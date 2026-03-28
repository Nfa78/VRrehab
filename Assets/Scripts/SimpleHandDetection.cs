using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class SimpleHandDetection : MonoBehaviour
{
    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;

    [HideInInspector]
    public bool hasUsedWrongHand = false;

    private PlayLocalizedAudio localizedAudio;

    public int currentLevel = 0;

    private int grabs = 0;

    private float timer = 0f;

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
    }

    // Update is called once per frame
    void Update()
    {
        if (_interactable != null)
        {
            var interactors = _interactable.Interactors; // List of active interactors

            if (interactors.Count > 0)
            {
                _interactor = interactors.FirstOrDefault() as HandGrabInteractor;

                if ((_interactor != null) && _interactor.IsGrabbing)
                {
                    timer += Time.deltaTime;
                    IHand hand = _interactor.Hand;
                    if (hand != null)
                    {
                        if (PointsManager.Instance.Arm == "Right")
                        {
                            if (hand.Handedness == Handedness.Left)
                            {
                                if (!localizedAudio.IsClipPlaying())
                                {
                                    if(this.tag == "Carrot")
                                    {
                                        grabs++;
                                        if(grabs >= 4)
                                        {
                                            if (currentLevel == 0 /*&& (timer >= 3f)*/)
                                                localizedAudio?.PlayLocalizedClip(9);
                                            else if(currentLevel == 1 /*&& (timer >= 3f)*/)
                                                localizedAudio?.PlayLocalizedClip(7);
                                            timer = 0f;
                                        }
                                    }
                                    else
                                    {
                                        /*if(timer >= 3f)
                                        {*/
                                            if (currentLevel == 0)
                                                localizedAudio?.PlayLocalizedClip(9);
                                            else if (currentLevel == 1)
                                                localizedAudio?.PlayLocalizedClip(7);
                                            timer = 0f;
                                        /*}*/
                                    }
                                }
                                hasUsedWrongHand = true;
                            }
                        }
                        else
                        {
                            if (hand.Handedness == Handedness.Right)
                            {
                                if (!localizedAudio.IsClipPlaying())
                                {
                                    if(timer >= 3f)
                                    {
                                        if (currentLevel == 0)
                                            localizedAudio?.PlayLocalizedClip(9);
                                        else
                                            localizedAudio?.PlayLocalizedClip(7);
                                        timer = 0f;
                                    }
                                }
                                hasUsedWrongHand = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
