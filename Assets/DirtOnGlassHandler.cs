using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DirtOnGlassHandler : MonoBehaviour
{
    [HideInInspector]
    public bool hasBeenHit = false; // To check whether the dirt has been sprayed

    private int numOfRubs = 0;
    private AudioSource spongeAudioSource;

    public AudioClip[] spongeRubSounds; // all the different possible sponge sfx

    private Vector3 originalScale;

    public GameObject arrow;
    private bool arrowStillActive = true;

    private float timer = 0f;

    private RemoveSpotsOnWindowObjective rsowo;

    public SprayCleanerHandler sch1;
    public SprayCleanerHandler sch2;
    private bool allSpotsSprayed = false;

    void Start()
    {
        originalScale = transform.localScale; // Store original scale
        rsowo = FindAnyObjectByType<RemoveSpotsOnWindowObjective>();
    }

    void Update()
    {
        if (sch1.hasPlayed || sch2.hasPlayed)
            allSpotsSprayed = true;
        if (hasBeenHit && arrowStillActive) // When the dirt gets sprayed, the arrow disappears
        {
            arrow.SetActive(false);
            arrowStillActive = false;
            timer = 0f;
        } 
        else if(hasBeenHit && !arrowStillActive && allSpotsSprayed)
        {
            timer += Time.deltaTime;
            if(timer >= 10f) // If 10 seconds pass after the arrow has disappeared without the dirt getting rubbed, the arrow reappears
            {
                arrow.SetActive(true);
                timer = 0f;
            }
        }

        if (numOfRubs > 3)
        {
            rsowo.dirtSpotsRemoved++;
            this.gameObject.SetActive(false);
            rsowo.UpdateTallyOnClipboard();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Sponge") && hasBeenHit)
        {
            timer = 0f;
            arrow.SetActive(false);

            Debug.Log("Sponge rubbing on spot");
            if (numOfRubs == 0)
            {
                spongeAudioSource = other.gameObject.GetComponent<AudioSource>();
            }
            if (spongeRubSounds.Length > 0 && spongeAudioSource != null)
            {
                PlayRandomAudio();
            }
            else
            {
                Debug.LogWarning("AudioClips or AudioSource is missing!");
            }
            numOfRubs++;
            // Shrink the object proportionally
            float shrinkFactor = Mathf.Clamp01(1f - (numOfRubs / 3f));
            transform.localScale = originalScale * shrinkFactor;
        }
        
    }

    public void PlayRandomAudio()
    {
        int randomIndex = Random.Range(0, spongeRubSounds.Length); // Choose a random index
        spongeAudioSource.clip = spongeRubSounds[randomIndex]; // Set the chosen clip
        spongeAudioSource.Play(); // Play the clip
    }
}
