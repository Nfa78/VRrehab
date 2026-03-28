using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KitchenTask01ScoreHandler : MonoBehaviour
{
    public ScoreOpenDrawer sod;
    public ScoreGrabAndPlaceThing sgapt;

    public GameObject pan;
    public HandDetection panHandDetectionComponent;
    public Transform panTargetTransform;

    public GameObject carrot;
    public HandDetection carrotHandDetectionComponent;
    public Transform carrotTargetTransform;

    private bool sgaptStarted = false;
    private bool sgaptCarrotStarted = false;

    private PlayLocalizedAudio localizedAudio;

    // Start is called before the first frame update
    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        sod.DoYourThing();
    }

    // Update is called once per frame
    void Update()
    {
        if(sod.done && !sgaptStarted)
        {
            sgaptStarted = true;
            sgapt.DoYourThing(pan, panHandDetectionComponent, panTargetTransform);
        }
        else if(sod.done && sgapt.succeeded && !sgaptCarrotStarted)
        {
            sgaptCarrotStarted = true;
            sgapt.DoYourThing(carrot, carrotHandDetectionComponent, carrotTargetTransform);
        }
    }
}
