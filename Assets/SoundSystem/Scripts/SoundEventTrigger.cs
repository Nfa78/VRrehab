using UnityEngine;

namespace SoundSystem
{
    public class SoundEventTrigger : MonoBehaviour
    {
        [SerializeField] private string eventId;
        [SerializeField] private EventSoundManager soundManager;
        [SerializeField] private bool warnIfManagerMissing = true;

        public string EventId
        {
            get => eventId;
            set => eventId = value;
        }

        public void Play()
        {
            if (!TryGetSoundManager(out EventSoundManager manager))
            {
                return;
            }

            manager.Play(eventId);
        }

        public void PlayAtSelf()
        {
            if (!TryGetSoundManager(out EventSoundManager manager))
            {
                return;
            }

            manager.PlayAt(eventId, transform.position);
        }

        public void StartLoop()
        {
            if (!TryGetSoundManager(out EventSoundManager manager))
            {
                return;
            }

            manager.StartLoop(eventId);
        }

        public void StopLoop()
        {
            if (!TryGetSoundManager(out EventSoundManager manager))
            {
                return;
            }

            manager.StopLoop(eventId);
        }

        private bool TryGetSoundManager(out EventSoundManager manager)
        {
            manager = soundManager != null ? soundManager : EventSoundManager.Instance;
            if (manager != null)
            {
                return true;
            }

            if (warnIfManagerMissing)
            {
                Debug.LogWarning($"No {nameof(EventSoundManager)} found. Sound event '{eventId}' skipped.", this);
            }

            return false;
        }
    }
}
