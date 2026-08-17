using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SoundSystem
{
    public enum EventSoundCategory
    {
        UI,
        TaskFeedback,
        ObjectInteraction,
        Ambience,
        VoiceInstruction,
        Other
    }

    public enum EventSoundPlaybackMode
    {
        OneShot,
        Loop,
        InterruptPrevious,
        IgnoreIfAlreadyPlaying
    }

    [Serializable]
    public class EventSoundDefinition
    {
        [SerializeField] private string eventId;
        [SerializeField] private List<AudioClip> audioClips = new List<AudioClip>();
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] [Min(0.01f)] private float minPitch = 1f;
        [SerializeField] [Min(0.01f)] private float maxPitch = 1f;
        [SerializeField] [Range(0f, 1f)] private float spatialBlend;
        [SerializeField] [Min(0f)] private float minCooldownSeconds = 0.05f;
        [SerializeField] private EventSoundCategory category = EventSoundCategory.Other;
        [SerializeField] private EventSoundPlaybackMode playbackMode = EventSoundPlaybackMode.OneShot;
        [SerializeField] private AudioMixerGroup outputMixerGroup;
        [SerializeField] private bool respectSimulationPause = true;

        public string EventId => eventId;
        public IReadOnlyList<AudioClip> AudioClips => audioClips;
        public float Volume => Mathf.Clamp01(volume);
        public float MinPitch => Mathf.Max(0.01f, Mathf.Min(minPitch, maxPitch));
        public float MaxPitch => Mathf.Max(MinPitch, maxPitch);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public float MinCooldownSeconds => Mathf.Max(0f, minCooldownSeconds);
        public EventSoundCategory Category => category;
        public EventSoundPlaybackMode PlaybackMode => playbackMode;
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
        public bool RespectSimulationPause => respectSimulationPause;
        public bool HasUsableClip => GetFirstUsableClipIndex() >= 0;

        public AudioClip GetRandomClip()
        {
            int usableCount = 0;
            for (int index = 0; index < audioClips.Count; index++)
            {
                if (audioClips[index] != null)
                {
                    usableCount++;
                }
            }

            if (usableCount == 0)
            {
                return null;
            }

            int selectedUsableIndex = UnityEngine.Random.Range(0, usableCount);
            int currentUsableIndex = 0;
            for (int index = 0; index < audioClips.Count; index++)
            {
                AudioClip clip = audioClips[index];
                if (clip == null)
                {
                    continue;
                }

                if (currentUsableIndex == selectedUsableIndex)
                {
                    return clip;
                }

                currentUsableIndex++;
            }

            return audioClips[GetFirstUsableClipIndex()];
        }

        public float GetRandomPitch()
        {
            if (Mathf.Approximately(MinPitch, MaxPitch))
            {
                return MinPitch;
            }

            return UnityEngine.Random.Range(MinPitch, MaxPitch);
        }

        private int GetFirstUsableClipIndex()
        {
            for (int index = 0; index < audioClips.Count; index++)
            {
                if (audioClips[index] != null)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    [CreateAssetMenu(fileName = "EventSoundLibrary", menuName = "Sound System/Event Sound Library")]
    public class EventSoundLibrary : ScriptableObject
    {
        [SerializeField] private List<EventSoundDefinition> sounds = new List<EventSoundDefinition>();

        public IReadOnlyList<EventSoundDefinition> Sounds => sounds;

        public bool TryGetDefinition(string eventId, out EventSoundDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }

            string normalizedEventId = eventId.Trim();
            for (int index = 0; index < sounds.Count; index++)
            {
                EventSoundDefinition sound = sounds[index];
                if (sound == null || string.IsNullOrWhiteSpace(sound.EventId))
                {
                    continue;
                }

                if (string.Equals(sound.EventId.Trim(), normalizedEventId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = sound;
                    return true;
                }
            }

            return false;
        }
    }
}
