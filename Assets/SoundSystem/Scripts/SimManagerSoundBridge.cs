using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

namespace SoundSystem
{
    public class SimManagerSoundBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private EventSoundManager soundManager;
        [SerializeField] private bool autoFindReferences = true;

        [Header("Simulation Events")]
        [SerializeField] private string simulationStartedEventId = "sim.started";
        [SerializeField] private string simulationPausedEventId = "sim.paused";
        [SerializeField] private string simulationResumedEventId = "sim.resumed";
        [SerializeField] private string simulationCompletedEventId = "sim.completed";
        [SerializeField] private string simulationFailedEventId = "sim.failed";
        [SerializeField] private string simulationStoppedEventId = "sim.stopped";

        [Header("Task Events")]
        [SerializeField] private string taskStartedEventId = "task.started";
        [SerializeField] private string taskCompletedEventId = "task.completed";
        [SerializeField] private string taskFailedEventId = "task.failed";
        [SerializeField] private bool playTaskSpecificEvents;

        [Header("Objective Events")]
        [SerializeField] private string objectiveCompletedEventId = "objective.completed";
        [SerializeField] private string objectiveProgressTickEventId = "objective.progress_tick";
        [SerializeField] private bool playObjectiveProgressTicks = true;
        [SerializeField] private bool playObjectiveSpecificEvents;
        [SerializeField] private bool skipBooleanObjectiveProgressTicks = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<string, int> _lastProgressTickByObjectiveKey = new Dictionary<string, int>();
        private SimManager.SimulationState _lastKnownState = SimManager.SimulationState.Idle;
        private bool _subscribed;
        private bool _warnedMissingSimManager;

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void ResolveReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (simManager == null)
            {
                simManager = FindFirstObjectByType<SimManager>();
            }

            if (soundManager == null)
            {
                soundManager = EventSoundManager.Instance != null
                    ? EventSoundManager.Instance
                    : FindFirstObjectByType<EventSoundManager>();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (simManager == null)
            {
                WarnMissingSimManager();
                return;
            }

            _lastKnownState = simManager.State;
            simManager.SimulationStateChanged += HandleSimulationStateChanged;
            simManager.LogicalTaskStarted += HandleTaskStarted;
            simManager.LogicalTaskCompleted += HandleTaskCompleted;
            simManager.LogicalTaskFailed += HandleTaskFailed;
            simManager.LogicalTaskObjectiveCompleted += HandleObjectiveCompleted;
            simManager.LogicalTaskObjectiveProgressChanged += HandleObjectiveProgressChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || simManager == null)
            {
                return;
            }

            simManager.SimulationStateChanged -= HandleSimulationStateChanged;
            simManager.LogicalTaskStarted -= HandleTaskStarted;
            simManager.LogicalTaskCompleted -= HandleTaskCompleted;
            simManager.LogicalTaskFailed -= HandleTaskFailed;
            simManager.LogicalTaskObjectiveCompleted -= HandleObjectiveCompleted;
            simManager.LogicalTaskObjectiveProgressChanged -= HandleObjectiveProgressChanged;
            _subscribed = false;
        }

        private void HandleSimulationStateChanged(SimManager.SimulationState state)
        {
            EventSoundManager manager = ResolveSoundManager();
            if (manager != null)
            {
                manager.SetGameplayPlaybackPaused(state == SimManager.SimulationState.Paused);
            }

            switch (state)
            {
                case SimManager.SimulationState.Running:
                    Play(_lastKnownState == SimManager.SimulationState.Paused
                        ? simulationResumedEventId
                        : simulationStartedEventId);
                    break;
                case SimManager.SimulationState.Paused:
                    Play(simulationPausedEventId);
                    break;
                case SimManager.SimulationState.Completed:
                    Play(simulationCompletedEventId);
                    break;
                case SimManager.SimulationState.Failed:
                    Play(simulationFailedEventId);
                    break;
                case SimManager.SimulationState.Stopped:
                    Play(simulationStoppedEventId);
                    break;
            }

            _lastKnownState = state;
        }

        private void HandleTaskStarted(SimTask task)
        {
            _lastProgressTickByObjectiveKey.Clear();
            Play(taskStartedEventId);

            if (playTaskSpecificEvents && task != null)
            {
                Play($"task.{task.TaskId}.started");
            }
        }

        private void HandleTaskCompleted(SimTask task)
        {
            Play(taskCompletedEventId);

            if (playTaskSpecificEvents && task != null)
            {
                Play($"task.{task.TaskId}.completed");
            }
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            Play(taskFailedEventId);

            if (playTaskSpecificEvents && task != null)
            {
                Play($"task.{task.TaskId}.failed");
            }
        }

        private void HandleObjectiveCompleted(SimTask task, SimTaskObjective objective)
        {
            Play(objectiveCompletedEventId);

            if (playObjectiveSpecificEvents && task != null && objective != null)
            {
                Play($"objective.{task.TaskId}.{objective.ObjectiveId}.completed");
            }
        }

        private void HandleObjectiveProgressChanged(SimTask task, SimTaskObjective objective)
        {
            if (!playObjectiveProgressTicks || objective == null)
            {
                return;
            }

            if (skipBooleanObjectiveProgressTicks && objective.Mode == SimTaskObjective.ObjectiveMode.Boolean)
            {
                return;
            }

            string objectiveKey = GetObjectiveKey(task, objective);
            int progressTick = Mathf.FloorToInt(objective.CurrentValue);
            if (progressTick <= 0)
            {
                return;
            }

            if (_lastProgressTickByObjectiveKey.TryGetValue(objectiveKey, out int lastProgressTick) &&
                progressTick <= lastProgressTick)
            {
                return;
            }

            _lastProgressTickByObjectiveKey[objectiveKey] = progressTick;
            Play(objectiveProgressTickEventId);

            if (playObjectiveSpecificEvents && task != null)
            {
                Play($"objective.{task.TaskId}.{objective.ObjectiveId}.progress_tick");
            }
        }

        private bool Play(string eventId)
        {
            EventSoundManager manager = ResolveSoundManager();
            bool played = manager != null && manager.Play(eventId);
            if (debugLogging)
            {
                Debug.Log($"[{nameof(SimManagerSoundBridge)}] Event '{eventId}' played={played}", this);
            }

            return played;
        }

        private EventSoundManager ResolveSoundManager()
        {
            if (soundManager != null)
            {
                return soundManager;
            }

            soundManager = EventSoundManager.Instance;
            return soundManager;
        }

        private string GetObjectiveKey(SimTask task, SimTaskObjective objective)
        {
            string taskId = task != null && !string.IsNullOrWhiteSpace(task.TaskId) ? task.TaskId : "<task>";
            string objectiveId = objective != null && !string.IsNullOrWhiteSpace(objective.ObjectiveId) ? objective.ObjectiveId : "<objective>";
            return $"{taskId}:{objectiveId}";
        }

        private void WarnMissingSimManager()
        {
            if (_warnedMissingSimManager)
            {
                return;
            }

            _warnedMissingSimManager = true;
            Debug.LogWarning($"{nameof(SimManagerSoundBridge)} has no SimManager reference. Simulation sounds will not be wired.", this);
        }
    }
}
