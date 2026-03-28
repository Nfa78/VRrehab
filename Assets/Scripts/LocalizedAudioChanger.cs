using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public class LocalizedAudioChanger : MonoBehaviour
{
    public LocalizeAudioClipEvent localizeAudioClipEvent;
    public LocalizedAudioClip[] clips;
    public AudioSource audioSource;
    int currentClip = 0;

    // Start is called before the first frame update
    void Start()
    {
        localizeAudioClipEvent.OnUpdateAsset.AddListener(PlayClip);
        ChangeAudio(clips[currentClip]);
    }

    void ChangeAudio(LocalizedAudioClip clip)
    {
        localizeAudioClipEvent.AssetReference = clip;
    }

    // Add this function to change audio from another script
    public void PlayLocalizedClip(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= clips.Length)
        {
            Debug.LogError("Invalid clip index: " + clipIndex);
            return;
        }

        ChangeAudio(clips[clipIndex]); // Set the new localized clip
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("Localized clip is null.");
        }
    }

}