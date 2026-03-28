using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class CarrotsInPanObjective : MonoBehaviour
{
    private int numPieces = 0; // number of carrot pieces currently inside of the pan
    public AudioSource success;
    private bool hasPlayed = false;
    public GameObject oilArrow;
    public Image checkbox;
    public Sprite checkedBox;
    private PlayLocalizedAudio localizedAudio;
    public GameObject task03Text;
    public GameObject task04Text;

    private HandGrabInteractable _interactable;
    private HandGrabInteractor _interactor;

    // Time tracking
    private float taskStartTime = 0f;
    private float taskEndTime = 0f;
    private bool taskInProgress = false;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Carrot"))
        {
            if(!taskInProgress && numPieces == 0)
            {
                taskStartTime = Time.time;
                taskInProgress = true;
            }
            numPieces++;
            CheckHandednessAndUpdatePoints(other.gameObject);
            if ((numPieces >= 10) && !hasPlayed)
            {
                taskEndTime = Time.time;
                success.Play();
                checkbox.sprite = checkedBox;
                oilArrow.SetActive(true);
                task03Text.SetActive(false);
                task04Text.SetActive(true);
                TallyPoints();
                StartCoroutine(PlayAudioWithDelay(1f)); // plays the next audio tip after 1 second
                //PointsManager.Instance.Points += 1;
                hasPlayed = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Carrot"))
            numPieces--;
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(4);
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
                                if (localizedAudio != null && !localizedAudio.IsClipPlaying())
                                {
                                    localizedAudio?.PlayLocalizedClip(9);
                                }
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
                                if (localizedAudio != null && !localizedAudio.IsClipPlaying())
                                {
                                    localizedAudio?.PlayLocalizedClip(9);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public void TallyPoints()
    {
        float totTime = taskEndTime - taskStartTime;
        if (totTime < 10)
            PointsManager.Instance.Points += 2;
        else if (totTime < 20)
            PointsManager.Instance.Points += 1;
    }
}
