using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class PanOnStoveObjective : MonoBehaviour
{
    public AudioSource youDidIt;
    public GameObject trigger;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject arrow; // arrow pointing to the pan on stove objective
    public GameObject otherArrow; // arrow pointing to the
    private bool hasPlayed = false;
    private PlayLocalizedAudio localizedAudio;
    public GameObject welcomeText;
    public GameObject task01Text;

    public ScoreGrabAndPlaceThing sgapt;
    public GameObject carrot;
    public HandDetection carrotHandDetectionComponent;
    public Transform carrotTargetTransform;

    public GameObject drawer;
    public Transform drawerClosedPosition;

    public GameObject panHandGrabInteraction;

    private SimpleHandDetection simpleHandDetection;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Pan") && !hasPlayed)
        {
            youDidIt.Play();
            checkbox.sprite = checkedBox;
            trigger.SetActive(false);
            arrow.SetActive(false);
            otherArrow.SetActive(true);
            Rigidbody rb = other.gameObject.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            other.gameObject.GetComponent<MeshCollider>().convex = false;
            StartCoroutine(MakePanNotGrabbable(other.gameObject, 5f));
            welcomeText.SetActive(false);
            task01Text.SetActive(true);
            StartCoroutine(PlayAudioWithDelay(3f)); // plays the next audio tip after 1 second
            StartCoroutine(MoveDrawerSmoothly(drawerClosedPosition.position, 1f)); // Move drawer
            hasPlayed = true;
            //sgapt.DoYourThing(carrot, carrotHandDetectionComponent, carrotTargetTransform);
            simpleHandDetection = other.GetComponent<SimpleHandDetection>();
            if (simpleHandDetection.hasUsedWrongHand)
                PointsManager.Instance.Points += 1;
            else
                PointsManager.Instance.Points += 2;
        }
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(1);
    }

    private IEnumerator MoveDrawerSmoothly(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = drawer.transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            drawer.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        drawer.transform.position = targetPosition;
    }

    IEnumerator MakePanNotGrabbable(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        /*Grabbable grab = panHandGrabInteraction.GetComponent<Grabbable>();
        grab.enabled = false;*/
        HandGrabInteractable hgi = panHandGrabInteraction.GetComponent<HandGrabInteractable>();
        hgi.enabled = false;
        GrabInteractable grabint = panHandGrabInteraction.GetComponent<GrabInteractable>();
        grabint.enabled = false;
        //OneGrabPhysicsJointTransformer one = panHandGrabInteraction.AddComponent<OneGrabPhysicsJointTransformer>();
    }
}
