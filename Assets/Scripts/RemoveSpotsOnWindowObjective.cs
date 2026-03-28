using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class RemoveSpotsOnWindowObjective : MonoBehaviour
{
    [HideInInspector]
    public int dirtSpotsRemoved = 0;

    public TMPro.TMP_Text canvasText;

    public AudioSource victorySound;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject vacuumArrow; // arrow pointing to the vacuum

    private bool hasPlayed = false;
    private PlayLocalizedAudio localizedAudio;
    public GameObject wipeText;
    public GameObject vacuumText;

    public SimpleHandDetection spongeSHD;

    private ScreenFader screenFader;

    public GameObject playerRig;
    public GameObject vacuumPosition;
    public HandGrabInteractable spongeHGI;

    public GameObject vacuumButtonOutline;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        screenFader = FindAnyObjectByType<ScreenFader>();
    }

    void Update()
    {
        if((dirtSpotsRemoved == 10) && !hasPlayed)
        {
            victorySound.Play();
            checkbox.sprite = checkedBox;
            vacuumArrow.SetActive(true);
            wipeText.SetActive(false);
            vacuumText.SetActive(true);
            StartCoroutine(PlayAudioWithDelay(1f));
            hasPlayed = true;
            spongeHGI.enabled = false;
            vacuumButtonOutline.SetActive(true);
            screenFader.FadeToBlackAndTeleport(playerRig.transform, vacuumPosition.transform);
            if (spongeSHD.hasUsedWrongHand)
                PointsManager.Instance.ThirdLevelPoints += 1;
            else
                PointsManager.Instance.ThirdLevelPoints += 2;
        }
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(2);
    }

    public void UpdateTallyOnClipboard()
    {
        // Update the localized progress number (e.g., "1/10", "2/10", etc.)
        canvasText.text = System.Text.RegularExpressions.Regex.Replace(canvasText.text, @"(\()([^\:]+:\s*)\d+/\d+(\))",
        match => match.Groups[1].Value + match.Groups[2].Value + dirtSpotsRemoved + "/10" + match.Groups[3].Value);
    }
}