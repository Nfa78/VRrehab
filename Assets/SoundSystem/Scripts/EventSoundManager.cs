using System.Collections.Generic;
using UnityEngine;

namespace SoundSystem
{
    [DisallowMultipleComponent]
    public class EventSoundManager : MonoBehaviour
    {
        private sealed class ActiveLoop
        {
            public AudioSource Source;
            public EventSoundDefinition Definition;
            public bool PausedByManager;
        }

        public static EventSoundManager Instance { get; private set; }

        [Header("Libraries")]
        [SerializeField] private List<EventSoundLibrary> soundLibraries = new List<EventSoundLibrary>();

        [Header("Playback")]
        [SerializeField] [Min(1)] private int initialPoolSize = 8;
        [SerializeField] private Transform poolRoot;
        [SerializeField] private bool useUnscaledTimeForCooldowns = true;
        [SerializeField] private bool dontDestroyOnLoad;

        [Header("Missing Sound Gate")]
        [SerializeField] private bool ignoreMissingSoundEvents = true;
        [SerializeField] private bool warnOnceForMissingSoundEvents = true;
        [SerializeField] private bool warnOnceForMissingClips = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private readonly List<AudioSource> _oneShotPool = new List<AudioSource>();
        private readonly Dictionary<string, float> _nextAllowedPlayTimeByEventId = new Dictionary<string, float>();
        private readonly Dictionary<string, ActiveLoop> _activeLoopsByEventId = new Dictionary<string, ActiveLoop>();
        private readonly Dictionary<string, AudioSource> _activeOneShotSourcesByEventId = new Dictionary<string, AudioSource>();
        private readonly Dictionary<AudioSource, string> _eventIdByOneShotSource = new Dictionary<AudioSource, string>();
        private readonly HashSet<string> _warnedMissingEventIds = new HashSet<string>();
        private readonly HashSet<string> _warnedMissingClipEventIds = new HashSet<string>();

        public bool GameplayPlaybackPaused { get; private set; }
        public bool DebugLogging
        {
            get => debugLogging;
            set => debugLogging = value;
        }

        public static bool TryPlay(string eventId)
        {
            return Instance != null && Instance.Play(eventId);
        }

        public static bool TryPlayAt(string eventId, Vector3 worldPosition)
        {
            return Instance != null && Instance.PlayAt(eventId, worldPosition);
        }

        public static bool TryStartLoop(string eventId)
        {
            return Instance != null && Instance.StartLoop(eventId);
        }

        public static bool TryStopLoop(string eventId)
        {
            return Instance != null && Instance.StopLoop(eventId);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(EventSoundManager)} instances found. Static calls will use '{Instance.name}'.", this);
            }

            if (dontDestroyOnLoad && Instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsurePoolRoot();
            EnsurePoolSize(initialPoolSize);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool Play(string eventId)
        {
            return PlayInternal(eventId, transform.position, hasWorldPosition: false);
        }

        public bool PlayAt(string eventId, Vector3 worldPosition)
        {
            return PlayInternal(eventId, worldPosition, hasWorldPosition: true);
        }

        public bool StartLoop(string eventId)
        {
            if (!TryResolveSound(eventId, out EventSoundDefinition definition))
            {
                return false;
            }

            string normalizedEventId = NormalizeEventId(eventId);
            if (ShouldBlockForPause(definition))
            {
                LogDebug($"Blocked '{normalizedEventId}' because gameplay playback is paused.");
                return false;
            }

            if (_activeLoopsByEventId.TryGetValue(normalizedEventId, out ActiveLoop existingLoop) &&
                existingLoop.Source != null)
            {
                if (definition.PlaybackMode == EventSoundPlaybackMode.InterruptPrevious)
                {
                    existingLoop.Source.Stop();
                }
                else if (existingLoop.Source.isPlaying || existingLoop.Source.clip != null)
                {
                    LogDebug($"Ignored loop '{normalizedEventId}' because it is already active.");
                    return false;
                }
            }

            if (!CanPlayByCooldown(normalizedEventId, definition))
            {
                return false;
            }

            AudioClip clip = definition.GetRandomClip();
            AudioSource source = existingLoop != null && existingLoop.Source != null
                ? existingLoop.Source
                : CreateAudioSource($"Loop_{normalizedEventId}");

            ApplyDefinitionToSource(source, definition, transform.position, hasWorldPosition: false);
            source.clip = clip;
            source.loop = true;
            source.Play();

            _activeLoopsByEventId[normalizedEventId] = new ActiveLoop
            {
                Source = source,
                Definition = definition,
                PausedByManager = false
            };

            RegisterCooldown(normalizedEventId, definition);
            LogDebug($"Started loop '{normalizedEventId}' using clip '{clip.name}'.");
            return true;
        }

        public bool StopLoop(string eventId)
        {
            string normalizedEventId = NormalizeEventId(eventId);
            if (string.IsNullOrEmpty(normalizedEventId))
            {
                return false;
            }

            if (!_activeLoopsByEventId.TryGetValue(normalizedEventId, out ActiveLoop activeLoop) ||
                activeLoop.Source == null)
            {
                LogDebug($"StopLoop ignored for '{normalizedEventId}' because no active loop exists.");
                return false;
            }

            activeLoop.Source.Stop();
            activeLoop.Source.clip = null;
            activeLoop.Source.loop = false;
            _activeLoopsByEventId.Remove(normalizedEventId);
            LogDebug($"Stopped loop '{normalizedEventId}'.");
            return true;
        }

        public void StopAllLoops()
        {
            foreach (KeyValuePair<string, ActiveLoop> pair in _activeLoopsByEventId)
            {
                if (pair.Value.Source == null)
                {
                    continue;
                }

                pair.Value.Source.Stop();
                pair.Value.Source.clip = null;
                pair.Value.Source.loop = false;
            }

            _activeLoopsByEventId.Clear();
        }

        public void SetGameplayPlaybackPaused(bool paused)
        {
            if (GameplayPlaybackPaused == paused)
            {
                return;
            }

            GameplayPlaybackPaused = paused;

            foreach (KeyValuePair<string, ActiveLoop> pair in _activeLoopsByEventId)
            {
                ActiveLoop activeLoop = pair.Value;
                if (activeLoop.Source == null || !ShouldRespectPause(activeLoop.Definition))
                {
                    continue;
                }

                if (paused && activeLoop.Source.isPlaying)
                {
                    activeLoop.Source.Pause();
                    activeLoop.PausedByManager = true;
                    LogDebug($"Paused loop '{pair.Key}'.");
                }
                else if (!paused && activeLoop.PausedByManager)
                {
                    activeLoop.Source.UnPause();
                    activeLoop.PausedByManager = false;
                    LogDebug($"Resumed loop '{pair.Key}'.");
                }
            }
        }

        private bool PlayInternal(string eventId, Vector3 worldPosition, bool hasWorldPosition)
        {
            if (!TryResolveSound(eventId, out EventSoundDefinition definition))
            {
                return false;
            }

            string normalizedEventId = NormalizeEventId(eventId);
            if (definition.PlaybackMode == EventSoundPlaybackMode.Loop)
            {
                return StartLoop(normalizedEventId);
            }

            if (ShouldBlockForPause(definition))
            {
                LogDebug($"Blocked '{normalizedEventId}' because gameplay playback is paused.");
                return false;
            }

            PruneFinishedOneShot(normalizedEventId);
            if (definition.PlaybackMode == EventSoundPlaybackMode.IgnoreIfAlreadyPlaying &&
                _activeOneShotSourcesByEventId.ContainsKey(normalizedEventId))
            {
                LogDebug($"Ignored '{normalizedEventId}' because it is already playing.");
                return false;
            }

            if (definition.PlaybackMode == EventSoundPlaybackMode.InterruptPrevious)
            {
                StopActiveOneShot(normalizedEventId);
            }

            if (!CanPlayByCooldown(normalizedEventId, definition))
            {
                return false;
            }

            AudioClip clip = definition.GetRandomClip();
            AudioSource source = GetAvailableOneShotSource();
            ApplyDefinitionToSource(source, definition, worldPosition, hasWorldPosition);
            source.loop = false;
            source.clip = null;
            source.PlayOneShot(clip, 1f);

            _activeOneShotSourcesByEventId[normalizedEventId] = source;
            _eventIdByOneShotSource[source] = normalizedEventId;

            RegisterCooldown(normalizedEventId, definition);
            LogDebug($"Played '{normalizedEventId}' using clip '{clip.name}'.");
            return true;
        }

        private bool TryResolveSound(string eventId, out EventSoundDefinition definition)
        {
            definition = null;
            string normalizedEventId = NormalizeEventId(eventId);
            if (string.IsNullOrEmpty(normalizedEventId))
            {
                WarnMissingEvent("<empty>");
                return false;
            }

            bool foundDefinitionWithoutClip = false;
            for (int index = soundLibraries.Count - 1; index >= 0; index--)
            {
                EventSoundLibrary library = soundLibraries[index];
                if (library == null)
                {
                    continue;
                }

                if (library.TryGetDefinition(normalizedEventId, out definition))
                {
                    if (definition != null && definition.HasUsableClip)
                    {
                        return true;
                    }

                    foundDefinitionWithoutClip = true;
                    definition = null;
                }
            }

            if (foundDefinitionWithoutClip)
            {
                WarnMissingClip(normalizedEventId);
                return false;
            }

            WarnMissingEvent(normalizedEventId);
            return false;
        }

        private bool CanPlayByCooldown(string eventId, EventSoundDefinition definition)
        {
            float now = GetClock();
            if (_nextAllowedPlayTimeByEventId.TryGetValue(eventId, out float nextAllowedPlayTime) &&
                now < nextAllowedPlayTime)
            {
                LogDebug($"Ignored '{eventId}' due to cooldown.");
                return false;
            }

            return true;
        }

        private void RegisterCooldown(string eventId, EventSoundDefinition definition)
        {
            if (definition.MinCooldownSeconds <= 0f)
            {
                return;
            }

            _nextAllowedPlayTimeByEventId[eventId] = GetClock() + definition.MinCooldownSeconds;
        }

        private void ApplyDefinitionToSource(AudioSource source, EventSoundDefinition definition, Vector3 worldPosition, bool hasWorldPosition)
        {
            if (source == null)
            {
                return;
            }

            source.outputAudioMixerGroup = definition.OutputMixerGroup;
            source.volume = definition.Volume;
            source.pitch = definition.GetRandomPitch();
            source.spatialBlend = definition.SpatialBlend;
            source.playOnAwake = false;

            if (hasWorldPosition)
            {
                source.transform.position = worldPosition;
            }
            else
            {
                source.transform.position = transform.position;
            }
        }

        private bool ShouldBlockForPause(EventSoundDefinition definition)
        {
            return GameplayPlaybackPaused && ShouldRespectPause(definition);
        }

        private bool ShouldRespectPause(EventSoundDefinition definition)
        {
            return definition != null &&
                   definition.RespectSimulationPause &&
                   definition.Category != EventSoundCategory.UI;
        }

        private void StopActiveOneShot(string eventId)
        {
            PruneFinishedOneShot(eventId);
            if (!_activeOneShotSourcesByEventId.TryGetValue(eventId, out AudioSource activeSource) ||
                activeSource == null)
            {
                return;
            }

            activeSource.Stop();
            _activeOneShotSourcesByEventId.Remove(eventId);
            _eventIdByOneShotSource.Remove(activeSource);
        }

        private void PruneFinishedOneShot(string eventId)
        {
            if (!_activeOneShotSourcesByEventId.TryGetValue(eventId, out AudioSource activeSource))
            {
                return;
            }

            if (activeSource == null ||
                !activeSource.isPlaying ||
                !_eventIdByOneShotSource.TryGetValue(activeSource, out string sourceEventId) ||
                sourceEventId != eventId)
            {
                _activeOneShotSourcesByEventId.Remove(eventId);
            }
        }

        private AudioSource GetAvailableOneShotSource()
        {
            for (int index = 0; index < _oneShotPool.Count; index++)
            {
                AudioSource source = _oneShotPool[index];
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            AudioSource newSource = CreateAudioSource($"OneShot_{_oneShotPool.Count}");
            _oneShotPool.Add(newSource);
            return newSource;
        }

        private void EnsurePoolSize(int count)
        {
            while (_oneShotPool.Count < count)
            {
                _oneShotPool.Add(CreateAudioSource($"OneShot_{_oneShotPool.Count}"));
            }
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            EnsurePoolRoot();

            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(poolRoot, worldPositionStays: false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return;
            }

            GameObject poolRootObject = new GameObject("AudioSourcePool");
            poolRootObject.transform.SetParent(transform, worldPositionStays: false);
            poolRoot = poolRootObject.transform;
        }

        private float GetClock()
        {
            return useUnscaledTimeForCooldowns ? Time.unscaledTime : Time.time;
        }

        private string NormalizeEventId(string eventId)
        {
            return string.IsNullOrWhiteSpace(eventId) ? string.Empty : eventId.Trim();
        }

        private void WarnMissingEvent(string eventId)
        {
            if (!ignoreMissingSoundEvents)
            {
                Debug.LogWarning($"Sound event '{eventId}' does not exist in assigned sound libraries.", this);
                return;
            }

            if (!warnOnceForMissingSoundEvents || !_warnedMissingEventIds.Add(eventId))
            {
                return;
            }

            Debug.LogWarning($"Sound event '{eventId}' is not configured yet. Playback skipped safely.", this);
        }

        private void WarnMissingClip(string eventId)
        {
            if (!warnOnceForMissingClips || !_warnedMissingClipEventIds.Add(eventId))
            {
                return;
            }

            Debug.LogWarning($"Sound event '{eventId}' exists but has no usable AudioClip. Playback skipped safely.", this);
        }

        private void LogDebug(string message)
        {
            if (!debugLogging)
            {
                return;
            }

            Debug.Log($"[{nameof(EventSoundManager)}] {message}", this);
        }
    }
}
