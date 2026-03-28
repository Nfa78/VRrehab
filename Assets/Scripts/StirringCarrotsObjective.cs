using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StirringCarrotsObjective : MonoBehaviour
{
    public string targetTag = "Spoon"; // Tag to check for
    public float timerThreshold = 5f;  // Time required to trigger the sound

    private float timer = 0f;          // Tracks time
    private bool isInside = false;     // Tracks if the object is inside
    public AudioSource success;   // To play the sound
    private bool hasPlayed = false; // To play the victory sound only once
    public Image checkbox;
    public Sprite checkedBox;

    private PlayLocalizedAudio localizedAudio;
    public GameObject task05Text;
    public GameObject task06Text;

    public TMPro.TMP_Text timerTag;

    private SimpleHandDetection simpleHandDetection;

    void Start()
    {
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInside = true;
            simpleHandDetection = other.GetComponent<SimpleHandDetection>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInside = false;
        }
    }

    private void Update()
    {
        if (isInside)
        {
            timer += Time.deltaTime;
            DisplayTime(timer); ///////

            if ((timer >= timerThreshold) && !hasPlayed)
            {
                success.Play();
                checkbox.sprite = checkedBox;
                task05Text.SetActive(false);
                task06Text.SetActive(true);
                StartCoroutine(PlayAudioWithDelay(1f)); // plays the next audio tip after 1 second
                hasPlayed = true;
                if(simpleHandDetection.hasUsedWrongHand)
                    PointsManager.Instance.Points += 1;
                else
                    PointsManager.Instance.Points += 2;
            }
        }
    }

    public bool hasStirredCarrots()
    {
        return hasPlayed;
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(6);
    }

    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerTag.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
