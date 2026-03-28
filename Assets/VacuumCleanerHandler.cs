using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class VacuumCleanerHandler : MonoBehaviour
{
    private bool isOn = false;
    private bool isPlaying = false;
    private AudioSource audioSource;
    private bool hasPlayedHint = false;

    public AudioClip vacuumStartSfx;
    public AudioClip vacuumSfx;
    public AudioClip vacuumEndSfx;

    private int dustRemoved = 0;

    public AudioSource victorySound;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject[] paperArrows; // arrow pointing to the paper

    private bool hasPlayed = false;
    private PlayLocalizedAudio localizedAudio;
    public GameObject vacuumText;
    public GameObject vacuumCleanText;
    public GameObject paperText;

    public SimpleHandDetection vacuumSHD;

    private ScreenFader screenFader;

    public GameObject playerRig;
    public GameObject deskPosition;
    public GameObject helper;
    public GameObject newHelperPosition;
    public GameObject newClonePosition;

    private CanvasNumberUpdater cnu;

    public GameObject[] dustArrows;

    public HandGrabInteractable vacuumHGI;

    private LoadCharacter lc;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        screenFader = FindAnyObjectByType<ScreenFader>();
        cnu = GetComponent<CanvasNumberUpdater>();
        lc = FindAnyObjectByType<LoadCharacter>();
    }

    public void TurnVacuumOnOrOff()
    {
        if (!isOn)
        {
            audioSource.PlayOneShot(vacuumStartSfx);
            if(!hasPlayedHint)
            {
                hasPlayedHint = true;
                StartCoroutine(PlayAudioWithDelay(1f, 3));
                vacuumText.SetActive(false);
                vacuumCleanText.SetActive(true);
                foreach (GameObject arrow in dustArrows)
                {
                    arrow.SetActive(true);
                }
            }
            isOn = true;
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
            audioSource.PlayOneShot(vacuumEndSfx);
            isOn = false;
        }
        Debug.Log("VacuumIsOn: " + isOn);
    }

    void Update()
    {
        if(isOn)
        {
            isPlaying = true;
            if (!audioSource.isPlaying)
                audioSource.PlayOneShot(vacuumSfx);
        }
        else
        {
            if(isPlaying)
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
                isPlaying = false;
            }
        }

        if ((dustRemoved == 10) && !hasPlayed)
        {
            hasPlayed = true;
            victorySound.Play();
            checkbox.sprite = checkedBox;
            foreach (GameObject arrow in paperArrows)
            {
                arrow.SetActive(true);
            }
            TurnVacuumOnOrOff();
            vacuumText.SetActive(false);
            vacuumCleanText.SetActive(false);
            paperText.SetActive(true);
            StartCoroutine(PlayAudioWithDelay(1f, 4));
            vacuumHGI.enabled = false;
            MoveHelper(helper.transform, newHelperPosition.transform);
            MoveHelper(lc.GetClone().transform, newClonePosition.transform);
            screenFader.FadeToBlackAndTeleport(playerRig.transform, deskPosition.transform);
            if (vacuumSHD.hasUsedWrongHand)
                PointsManager.Instance.ThirdLevelPoints += 1;
            else
                PointsManager.Instance.ThirdLevelPoints += 2;
        }
    }

    public bool VacuumIsOn()
    {
        return isOn;
    }

    public void UpdateDustRemovedVariable()
    {
        dustRemoved++;
        cnu.currentTally++;
        cnu.UpdateTallyOnClipboard();
    }

    IEnumerator PlayAudioWithDelay(float delay, int index)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(index);
    }

    void MoveHelper(Transform helper, Transform targetPos)
    {
        helper.position = targetPos.position;
        helper.rotation = targetPos.rotation;
    }
}
