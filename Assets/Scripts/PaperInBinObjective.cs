using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Samples;
using TMPro;
using UnityEngine.UI;

public class PaperInBinObjective : MonoBehaviour
{
    private int piecesInBin = 0;

    public AudioSource victorySound;
    public Image checkbox;
    public Sprite checkedBox;

    private AudioSource audioSource;

    private bool hasPlayed = false;
    private PlayLocalizedAudio localizedAudio;
    public GameObject paperText;
    public GameObject victoryText;

    private RespawnOnDrop rod;

    public GameObject binPositionRight;
    public GameObject binPositionLeft;

    private CanvasNumberUpdater cnu;

    public GameObject victoryCanvas;

    private PaperBallHandler pbh;

    void Start()
    {
        if ((PointsManager.Instance != null) && (PointsManager.Instance.Arm == "Right"))
        {
            this.transform.position = binPositionRight.transform.position;
            this.transform.rotation = binPositionRight.transform.rotation;
        }
        else if ((PointsManager.Instance != null) && (PointsManager.Instance.Arm == "Left"))
        {
            this.transform.position = binPositionLeft.transform.position;
            this.transform.rotation = binPositionLeft.transform.rotation;
        }

        audioSource = GetComponent<AudioSource>();
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        cnu = GetComponent<CanvasNumberUpdater>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Paper"))
        {
            piecesInBin++;
            cnu.currentTally++;
            cnu.UpdateTallyOnClipboard();
            rod = other.gameObject.GetComponent<RespawnOnDrop>();
            rod.enabled = false;
            pbh = other.gameObject.GetComponent<PaperBallHandler>();
            if(pbh.HasUsedWrongHand())
                PointsManager.Instance.ThirdLevelPoints += 1;
            else
                PointsManager.Instance.ThirdLevelPoints += 2;
        }
        if((piecesInBin == 10) && !hasPlayed)
        {
            hasPlayed = true;
            victorySound.Play();
            checkbox.sprite = checkedBox;
            paperText.SetActive(false);
            victoryText.SetActive(true);
            StartCoroutine(PlayAudioWithDelay(1f));
            victoryCanvas.SetActive(true);
            PointsManager.Instance.SaveCurrentUserPoints();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Paper"))
        {
            cnu.currentTally--;
            cnu.UpdateTallyOnClipboard();
            piecesInBin--;
            rod = other.gameObject.GetComponent<RespawnOnDrop>();
            rod.enabled = true;
        }
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(5);
    }
}
