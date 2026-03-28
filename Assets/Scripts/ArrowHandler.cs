using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class ArrowHandler : MonoBehaviour
{
    public GameObject targetObject; // object containing the HandGrabInteractable component
    public GameObject arrow; // arrow pointing to the drawer (or fridge)
    public GameObject otherArrow; // arrow pointing to the stove (or cutting board)
    private HandGrabInteractable _interactable;
    private bool firstObjectiveDone = false;
    private PlayLocalizedAudio localizedAudio;

    public GameObject spoonOutline;

    public GameObject takeCapsuleText;
    public GameObject putCapsuleText;

    private bool hasPlayed = false;

    // Start is called before the first frame update
    void Start()
    {
        _interactable = targetObject.GetComponent<HandGrabInteractable>();
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    // Update is called once per frame
    void Update()
    {
        var hand = _interactable.State;
        if(hand == InteractableState.Select) // check if object is being grabbed
        {
            if (gameObject.CompareTag("Spoon") || gameObject.CompareTag("CoffeeCapsule") || gameObject.CompareTag("Plate") || gameObject.CompareTag("Cup"))
                spoonOutline?.SetActive(false);
            arrow.SetActive(false);
            if(!firstObjectiveDone)
                otherArrow.SetActive(true);
            firstObjectiveDone = true;
            if(gameObject.CompareTag("CoffeeCapsule") && !hasPlayed)
            {
                //StartCoroutine(PlayAudioWithDelay(1f));
                localizedAudio?.PlayLocalizedClip(2);
                takeCapsuleText.SetActive(false);
                putCapsuleText.SetActive(true);
                hasPlayed = true;
            }
        }
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(2);
        takeCapsuleText.SetActive(false);
        putCapsuleText.SetActive(true);
    }
}
