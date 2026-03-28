using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StoveFireHandler : MonoBehaviour
{
    public ParticleSystem fireAlpha;
    public ParticleSystem fireADD;
    public ParticleSystem fireGlow;
    public ParticleSystem fire;
    public GameObject fireLight;
    public AudioSource success;
    private bool hasPlayedSuccess = false;
    public GameObject drawerArrow;

    private float initialAngle;
    public float targetAngle = 45f;

    public AudioSource stoveSound;
    private bool isPlaying = false;
    public Image checkbox;
    public Sprite checkedBox;

    // Variables for the win condition
    public Image checkbox02;
    public GameObject winCanvas;
    private bool hasPlayedEnd = false;

    private PlayLocalizedAudio localizedAudio;
    public GameObject task04Text;
    public GameObject task05Text;
    public GameObject task06Text;
    public GameObject task07Text;

    public ScoreGrabThing scoreGrabThing;
    public GameObject spoon;
    public HandDetection spoonHDComponent;

    private bool turningOn = false;
    private float currentAngle;

    private StirringCarrotsObjective stirringCarrots;

    public GameObject spoonOutline;

    void Start()
    {
        initialAngle = transform.localEulerAngles.x;
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
        stirringCarrots = FindAnyObjectByType<StirringCarrotsObjective>();
    }

    void Update()
    {
        currentAngle = transform.localEulerAngles.x;

        Debug.Log($"Non-normalized Angle: {currentAngle}");

        // Normalize the angle to handle wrapping (e.g., 0-360 degrees)
        currentAngle = NormalizeAngle(currentAngle);

        Debug.Log($"(Normalized) current Angle: {currentAngle}");

        // Check if the current angle is within the target range
        if (currentAngle > targetAngle)
        {
            if(!hasPlayedSuccess)
            {
                turningOn = true;
                success.Play();
                drawerArrow.SetActive(true);
                checkbox.sprite = checkedBox;
                task04Text.SetActive(false);
                task05Text.SetActive(true);
                StartCoroutine(PlayAudioWithDelay(1f, 5)); // plays the next audio tip after 1 second
                hasPlayedSuccess = true;
                spoonOutline.SetActive(true);
                if (currentAngle < 90f)
                    PointsManager.Instance.Points += 2;
                else
                    PointsManager.Instance.Points += 1;
                scoreGrabThing.DoYourThing(spoon, spoonHDComponent, spoon.transform);
            }

            //Debug.Log("Angle is greater than targetAngle. Checking if effects are playing...");
            if (!isPlaying)
            {
                Debug.Log("Turning on stove effects.");
                fire.Play();
                fireADD.Play();
                fireAlpha.Play();
                fireGlow.Play();
                stoveSound.Play();
                fireLight.SetActive(true);
                isPlaying = true;
            }
        }
        else
        {
            //Debug.Log("Angle is less than or equal to targetAngle. Checking if effects are playing...");
            if (isPlaying)
            {
                Debug.Log("Turning off stove effects.");
                fire.Stop();
                fireADD.Stop();
                fireAlpha.Stop();
                fireGlow.Stop();
                stoveSound.Pause();
                fireLight.SetActive(false);
                isPlaying = false;
                if(hasPlayedSuccess && !hasPlayedEnd && stirringCarrots.hasStirredCarrots())
                {
                    turningOn = false;
                    success.Play();
                    checkbox02.sprite = checkedBox;
                    task06Text.SetActive(false);
                    task07Text.SetActive(true);
                    hasPlayedEnd = true;
                    if (currentAngle == initialAngle)
                        PointsManager.Instance.Points += 2;
                    else
                        PointsManager.Instance.Points += 1;
                    PointsManager.Instance.SaveCurrentUserPoints();
                    winCanvas.SetActive(true);
                    StartCoroutine(PlayAudioWithDelay(1f, 7)); // plays the next audio tip after 1 second
                }
            }
        }
    }

    // Helper function to normalize the angle to 0-360 degrees
    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f; // Convert angles > 180 to negative
        return angle;
    }

    IEnumerator PlayAudioWithDelay(float delay, int index)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(index);
    }

    public void TallyPoints()
    {
        if(turningOn)
        {
            if (currentAngle < 90f)
                PointsManager.Instance.Points += 2;
            else
                PointsManager.Instance.Points += 1;
            scoreGrabThing.DoYourThing(spoon, spoonHDComponent, spoon.transform);
        }
        else
        {
            if(stirringCarrots.hasStirredCarrots())
            {
                if (currentAngle == initialAngle)
                    PointsManager.Instance.Points += 2;
                else
                    PointsManager.Instance.Points += 1;
                PointsManager.Instance.SaveCurrentUserPoints();
                winCanvas.SetActive(true);
                StartCoroutine(PlayAudioWithDelay(1f, 7)); // plays the next audio tip after 1 second
            }
        }
    }
}