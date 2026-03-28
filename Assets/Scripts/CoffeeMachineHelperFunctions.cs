using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;

public class CoffeeMachineHelperFunctions : MonoBehaviour
{
    public Image checkbox05;
    public Sprite checkedBox;

    private PlayLocalizedAudio localizedAudio;

    public GameObject pressButtonText;
    public GameObject wellDoneText;

    public GameObject endingScreen;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    public void FinishCoffeeLevel()
    {
        checkbox05.sprite = checkedBox;
        PointsManager.Instance.SaveCurrentUserPoints();
        StartCoroutine(PlayAudioWithDelay(7f));
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(8);
        pressButtonText.SetActive(false);
        wellDoneText.SetActive(true);
        endingScreen.SetActive(true);
    }
}
