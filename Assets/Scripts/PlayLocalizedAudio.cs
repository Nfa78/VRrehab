using UnityEngine;

public class PlayLocalizedAudio : MonoBehaviour
{
    private AudioSource audioSource; // Assign the AudioSource (same GameObject as Localized Audio Changer)
    private LocalizedAudioChanger localizedAudioChanger; // Reference to Localized Audio Changer

    void Start()
    {
        localizedAudioChanger = FindAnyObjectByType<LocalizedAudioChanger>();
        if (localizedAudioChanger != null)
        {
            // Get the AudioSource from the same GameObject as LocalizedAudioChanger
            audioSource = localizedAudioChanger.GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Debug.LogError("No AudioSource found on the same GameObject as LocalizedAudioChanger!");
            }
        }
    }

    public void PlayLocalizedClip(int clipIndex)
    {
        if (localizedAudioChanger != null && clipIndex >= 0)
        {
            // Stop any currently playing audio before playing a new one
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            localizedAudioChanger.PlayLocalizedClip(clipIndex);
        }
        else
        {
            Debug.LogWarning("Invalid audio clip index or Localized Audio Changer not found!");
        }
    }

    public bool IsClipPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }

}
