using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CarrotOnBoardObjective : MonoBehaviour
{
    public AudioSource youDidIt;
    public GameObject trigger;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject arrow; // arrow pointing to the pan on stove objective
    private PlayLocalizedAudio localizedAudio;
    public GameObject task01Text;
    public GameObject task02Text;

    public ScoreOpenDrawer sod;
    public ScoreGrabAndPlaceThing sgapt;
    private bool sgaptStarted = false;

    public GameObject knife;
    public GameObject knifeOutline;
    public HandDetection knifeHandDetectionComponent;
    public Transform knifeTargetTransform;

    private SimpleHandDetection simpleHandDetection;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Sliceable"))
        {
            youDidIt.Play();
            checkbox.sprite = checkedBox;
            trigger.SetActive(false);
            arrow.SetActive(false);
            task01Text.SetActive(false);
            task02Text.SetActive(true);
            Invoke(nameof(PlayAudio), 1f);
            simpleHandDetection = other.GetComponent<SimpleHandDetection>();
            if (simpleHandDetection.hasUsedWrongHand)
                PointsManager.Instance.Points += 1;
            else
                PointsManager.Instance.Points += 2;
            knifeOutline.SetActive(true);
            //sod.DoYourThing();
            //sgapt.DoYourThing(knife, knifeHandDetectionComponent, knifeTargetTransform);
        }
    }

    void PlayAudio()
    {
        localizedAudio?.PlayLocalizedClip(2);
    }

    void Update()
    {
        if (sod.done && !sgaptStarted)
        {
            sgaptStarted = true;
            sgapt.DoYourThing(knife, knifeHandDetectionComponent, knifeTargetTransform);
        }
    }
}