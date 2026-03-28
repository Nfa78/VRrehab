using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class SprayCleanerHandler : MonoBehaviour
{
    private DirtOnGlassHandler dogh;
    private int spotsHit = 0;

    public AudioSource victorySound;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject spongeArrow; // arrow pointing to the sponge

    [HideInInspector]
    public bool hasPlayed = false;

    private PlayLocalizedAudio localizedAudio;
    public GameObject sprayText;
    public GameObject wipeText;

    public SimpleHandDetection sprayBottleSHD;
    public HandGrabInteractable spongeHGI;

    private CanvasNumberUpdater cnu;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        cnu = GetComponent<CanvasNumberUpdater>();
    }

    void OnParticleCollision(GameObject other)
    {
        if(other.CompareTag("Spice"))
        {
            dogh = other.GetComponent<DirtOnGlassHandler>();
            if(!dogh.hasBeenHit)
            {
                Debug.Log("Water spray hit " + other.gameObject.name.ToString());
                dogh.hasBeenHit = true;
                spotsHit++;
                // Disable the non-trigger BoxCollider
                BoxCollider[] colliders = other.GetComponents<BoxCollider>();
                foreach (BoxCollider col in colliders)
                {
                    if (!col.isTrigger)
                    {
                        col.enabled = false;
                    }
                }
                cnu.currentTally++;
                cnu.UpdateTallyOnClipboard();
            }
            if ((spotsHit == 10) && !hasPlayed)
            {
                victorySound.Play();
                checkbox.sprite = checkedBox;
                spongeArrow.SetActive(true);
                sprayText.SetActive(false);
                wipeText.SetActive(true);
                StartCoroutine(PlayAudioWithDelay(1f)); // plays the next audio tip after 1 second
                hasPlayed = true;
                spongeHGI.enabled = true;
                if (sprayBottleSHD.hasUsedWrongHand)
                    PointsManager.Instance.ThirdLevelPoints += 1;
                else
                    PointsManager.Instance.ThirdLevelPoints += 2;
            }
        }
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(1);
    }
}
