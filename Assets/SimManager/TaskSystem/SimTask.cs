using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskSystem
{
    [Serializable]
    public class SimTask
    {
        public enum TaskState
        {
            Idle,
            Running,
            Paused,
            Completed,
            Failed
        }

        public enum DifficultyLevel
        {
            Level0 = 0,
            Level1 = 1,
            Level2 = 2
        }

        [Header("Task Identity")]
        [SerializeField] private string taskId;
        [SerializeField] private string title;
        [SerializeField] [TextArea] private string description;

        [Header("Task Content")]
        [SerializeField] private List<TrackedTaskObject> trackedObjects = new List<TrackedTaskObject>();
        [SerializeField] private List<SimTaskObjective> objectives = new List<SimTaskObjective>();
        [SerializeField] private MonoBehaviour taskDriver;

        [Header("Task Limits")]
        [SerializeField] private float timeLimitSeconds;
        [SerializeField] private bool failOnTimeout = true;
        [SerializeField] private string timeoutFailureReason = "Timeout";

        [Header("Task Behavior")]
        [SerializeField] private DifficultyLevel difficultyLevel = DifficultyLevel.Level0;
        [SerializeField] private bool captureTrackedObjectStartStateOnStart = true;
        [SerializeField] private bool resetTrackedObjectsOnReset = true;
        [SerializeField] private bool autoCompleteWhenAllRequiredObjectivesComplete = true;

        [NonSerialized] private TaskState _state = TaskState.Idle;
        [NonSerialized] private float _lastResumeClock;
        [NonSerialized] private float _lastPauseClock;
        [NonSerialized] private float _accumulatedActiveSeconds;
        [NonSerialized] private float _accumulatedPausedSeconds;
        [NonSerialized] private string _startedAtUtc;
        [NonSerialized] private string _endedAtUtc;
        [NonSerialized] private string _lastFailureReason;
        [NonSerialized] private int _currentObjectiveIndex = -1;
        [NonSerialized] private int _lastCompletedObjectiveIndex = -1;
        [NonSerialized] private int _lastReachedMilestoneIndex = -1;

        public event Action<SimTask> Started;
        public event Action<SimTask> Paused;
        public event Action<SimTask> Resumed;
        public event Action<SimTask> Ended;
        public event Action<SimTask, string> Failed;
        public event Action<SimTask, SimTaskObjective> CurrentObjectiveChanged;
        public event Action<SimTask, SimTaskObjective> ObjectiveProgressChanged;
        public event Action<SimTask, SimTaskObjective> ObjectiveCompleted;

        public string TaskId => taskId;
        public string Title => title;
        public string Description => description;
        public IReadOnlyList<TrackedTaskObject> TrackedObjects => trackedObjects;
        public IReadOnlyList<SimTaskObjective> Objectives => objectives;
        public IReadOnlyList<SimTaskObjective> Steps => objectives;
        public MonoBehaviour TaskDriverComponent => taskDriver;
        public TaskState State => _state;
        public int CurrentObjectiveIndex => _currentObjectiveIndex;
        public int CurrentStepIndex => _currentObjectiveIndex;
        public int LastCompletedObjectiveIndex => _lastCompletedObjectiveIndex;
        public int LastReachedMilestoneIndex => _lastReachedMilestoneIndex;
        public string StartedAtUtc => _startedAtUtc;
        public string EndedAtUtc => _endedAtUtc;
        public string LastFailureReason => _lastFailureReason;
        public float TimeLimitSeconds => timeLimitSeconds;
        public bool FailOnTimeout => failOnTimeout;
        public string TimeoutFailureReason => timeoutFailureReason;
        public DifficultyLevel Difficulty => difficultyLevel;
        public int DifficultyProfileLevel => (int)difficultyLevel + 1;
        public bool HasTimeLimit => timeLimitSeconds > 0f;
        public bool IsRunning => _state == TaskState.Running;
        public bool IsPaused => _state == TaskState.Paused;

        public SimTaskObjective CurrentObjective =>
            _currentObjectiveIndex >= 0 && _currentObjectiveIndex < objectives.Count ? objectives[_currentObjectiveIndex] : null;

        public SimTaskObjective CurrentStep => CurrentObjective;

        public bool AreAllRequiredObjectivesCompleted => CountRequiredObjectives(includeCompletedOnly: false) == CompletedRequiredObjectiveCount;
        public int RequiredObjectiveCount => CountRequiredObjectives(includeCompletedOnly: false);
        public int CompletedRequiredObjectiveCount => CountRequiredObjectives(includeCompletedOnly: true);

        public float GetElapsedSeconds(float currentClock)
        {
            if (_state == TaskState.Running)
            {
                return _accumulatedActiveSeconds + (currentClock - _lastResumeClock);
            }

            return _accumulatedActiveSeconds;
        }

        public float GetPausedSeconds(float currentClock)
        {
            if (_state == TaskState.Paused)
            {
                return _accumulatedPausedSeconds + (currentClock - _lastPauseClock);
            }

            return _accumulatedPausedSeconds;
        }

        public bool HasTimedOut(float currentClock)
        {
            return HasTimeLimit && GetElapsedSeconds(currentClock) >= timeLimitSeconds;
        }

        public void StartTask(float currentClock)
        {
            if (_state == TaskState.Running)
            {
                return;
            }

            if (_state == TaskState.Paused)
            {
                ResumeTask(currentClock);
                return;
            }

            ResetRuntimeState();
            ResetObjectives();
            BeginObjectives(0f);

            if (captureTrackedObjectStartStateOnStart)
            {
                CaptureTrackedObjectStartStates();
            }

            _startedAtUtc = DateTime.UtcNow.ToString("o");
            _lastResumeClock = currentClock;
            _state = TaskState.Running;
            Started?.Invoke(this);
        }

        public void PauseTask(float currentClock)
        {
            if (_state != TaskState.Running)
            {
                return;
            }

            _accumulatedActiveSeconds += currentClock - _lastResumeClock;
            _lastPauseClock = currentClock;
            _state = TaskState.Paused;
            Paused?.Invoke(this);
        }

        public void ResumeTask(float currentClock)
        {
            if (_state != TaskState.Paused)
            {
                return;
            }

            _accumulatedPausedSeconds += currentClock - _lastPauseClock;
            _lastResumeClock = currentClock;
            _state = TaskState.Running;
            Resumed?.Invoke(this);
        }

        public void EndTask(float currentClock)
        {
            if (_state != TaskState.Running && _state != TaskState.Paused)
            {
                return;
            }

            FinalizeTiming(currentClock);
            _endedAtUtc = DateTime.UtcNow.ToString("o");
            _state = TaskState.Completed;
            Ended?.Invoke(this);
        }

        public void FailTask(float currentClock, string failureReason)
        {
            if (_state != TaskState.Running && _state != TaskState.Paused)
            {
                return;
            }

            _lastFailureReason = failureReason;
            FinalizeTiming(currentClock);
            _endedAtUtc = DateTime.UtcNow.ToString("o");
            _state = TaskState.Failed;
            Failed?.Invoke(this, failureReason);
        }

        public void StopTask(float currentClock)
        {
            EndTask(currentClock);
        }

        public void ResetTask()
        {
            ResetRuntimeState();
            ResetObjectives();

            if (resetTrackedObjectsOnReset)
            {
                ResetTrackedObjectsToStartState();
            }

            _state = TaskState.Idle;
            NotifyCurrentObjectiveChanged();
        }

        public bool RestartFromLastMilestone(float currentClock)
        {
            if (objectives.Count == 0)
            {
                return false;
            }

            int restartIndex = Mathf.Clamp(GetRestartStepIndex(), 0, objectives.Count - 1);

            if (resetTrackedObjectsOnReset)
            {
                ResetTrackedObjectsToStartState();
            }

            ResetObjectivesFrom(restartIndex);
            _currentObjectiveIndex = restartIndex;

            SimTaskObjective restartObjective = objectives[_currentObjectiveIndex];
            _lastCompletedObjectiveIndex = restartIndex - 1;
            if (restartObjective != null && restartObjective.IsMilestone)
            {
                _lastReachedMilestoneIndex = restartIndex;
            }

            if (restartObjective != null)
            {
                restartObjective.Begin(GetElapsedSeconds(currentClock));
            }

            _state = TaskState.Running;
            _lastFailureReason = string.Empty;
            NotifyCurrentObjectiveChanged();
            return true;
        }

        public void CaptureTrackedObjectStartStates()
        {
            for (int i = 0; i < trackedObjects.Count; i++)
            {
                trackedObjects[i]?.CaptureStartState();
            }
        }

        public void ResetTrackedObjectsToStartState()
        {
            for (int i = 0; i < trackedObjects.Count; i++)
            {
                trackedObjects[i]?.ResetToStartState();
            }
        }

        public void ResetObjectives()
        {
            ResetObjectivesFrom(0);
            _lastCompletedObjectiveIndex = -1;
            _lastReachedMilestoneIndex = -1;
        }

        public void ResetObjectivesFrom(int startIndex)
        {
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            for (int i = startIndex; i < objectives.Count; i++)
            {
                objectives[i]?.Reset();
            }

            if (startIndex <= _lastCompletedObjectiveIndex)
            {
                _lastCompletedObjectiveIndex = startIndex - 1;
            }

            if (startIndex <= _lastReachedMilestoneIndex)
            {
                _lastReachedMilestoneIndex = FindCompletedMilestoneBeforeIndex(startIndex);
            }
        }

        public void BeginObjectives(float currentTaskTimeSeconds)
        {
            _currentObjectiveIndex = FindNextObjectiveIndex(-1);
            if (_currentObjectiveIndex < 0)
            {
                NotifyCurrentObjectiveChanged();
                return;
            }

            objectives[_currentObjectiveIndex]?.Begin(currentTaskTimeSeconds);
            NotifyCurrentObjectiveChanged();
        }

        public SimTaskObjective GetObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return CurrentObjective;
            }

            int objectiveIndex = GetObjectiveIndex(objectiveId);
            return objectiveIndex >= 0 ? objectives[objectiveIndex] : null;
        }

        public int GetObjectiveIndex(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return -1;
            }

            for (int i = 0; i < objectives.Count; i++)
            {
                SimTaskObjective objective = objectives[i];
                if (objective != null && objective.ObjectiveId == objectiveId)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool IsObjectiveCompleted(string objectiveId)
        {
            SimTaskObjective objective = GetObjective(objectiveId);
            return objective != null && objective.IsCompleted;
        }

        public bool MoveCurrentObjectiveTo(string objectiveId, float currentClock, bool resetTargetBeforeBegin = false)
        {
            int targetIndex = GetObjectiveIndex(objectiveId);
            if (targetIndex < 0)
            {
                return false;
            }

            SimTaskObjective currentObjective = CurrentObjective;
            if (currentObjective != null && !currentObjective.IsCompleted && !currentObjective.IsFailed)
            {
                currentObjective.Reset();
            }

            _currentObjectiveIndex = targetIndex;
            SimTaskObjective targetObjective = objectives[_currentObjectiveIndex];
            if (targetObjective == null)
            {
                NotifyCurrentObjectiveChanged();
                return false;
            }

            if (resetTargetBeforeBegin)
            {
                targetObjective.Reset();
            }

            if (!targetObjective.IsCompleted)
            {
                targetObjective.Begin(GetElapsedSeconds(currentClock));
            }

            NotifyCurrentObjectiveChanged();
            return true;
        }

        public TrackedTaskObject GetTrackedObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            for (int i = 0; i < trackedObjects.Count; i++)
            {
                TrackedTaskObject trackedObject = trackedObjects[i];
                if (trackedObject != null && trackedObject.ObjectId == objectId)
                {
                    return trackedObject;
                }
            }

            return null;
        }

        public bool CompleteObjective(string objectiveId, float currentClock)
        {
            return UpdateObjective(
                objectiveId,
                currentClock,
                requireCurrentObjective: true,
                (objective, elapsedSeconds) =>
                {
                    objective.Complete(elapsedSeconds);
                    return true;
                });
        }

        public bool SetObjectiveState(string objectiveId, bool value, float currentClock)
        {
            return UpdateObjective(
                objectiveId,
                currentClock,
                requireCurrentObjective: true,
                (objective, elapsedSeconds) => objective.SetBooleanState(value, elapsedSeconds));
        }

        public bool SetObjectiveStateAny(string objectiveId, bool value, float currentClock)
        {
            return UpdateObjective(
                objectiveId,
                currentClock,
                requireCurrentObjective: false,
                (objective, elapsedSeconds) => objective.SetBooleanState(value, elapsedSeconds));
        }

        public bool SetObjectiveProgress(string objectiveId, float value, float currentClock)
        {
            return UpdateObjective(
                objectiveId,
                currentClock,
                requireCurrentObjective: true,
                (objective, elapsedSeconds) => objective.SetProgress(value, elapsedSeconds));
        }

        public bool AddObjectiveProgress(string objectiveId, float delta, float currentClock)
        {
            return UpdateObjective(
                objectiveId,
                currentClock,
                requireCurrentObjective: true,
                (objective, elapsedSeconds) => objective.AddProgress(delta, elapsedSeconds));
        }

        public bool FailObjective(string objectiveId)
        {
            SimTaskObjective objective = GetObjective(objectiveId);
            if (objective == null || objective != CurrentObjective)
            {
                return false;
            }

            objective.Fail();
            return true;
        }

        public bool ResetObjective(string objectiveId, float currentClock)
        {
            SimTaskObjective objective = GetObjective(objectiveId);
            if (objective == null)
            {
                return false;
            }

            objective.Reset();
            if (objective == CurrentObjective)
            {
                objective.Begin(GetElapsedSeconds(currentClock));
                NotifyCurrentObjectiveChanged();
            }

            return true;
        }

        public bool ResetTrackedObject(string objectId)
        {
            TrackedTaskObject trackedObject = GetTrackedObject(objectId);
            if (trackedObject == null)
            {
                return false;
            }

            trackedObject.ResetToStartState();
            return true;
        }

        public void SetTimeLimitSeconds(float seconds)
        {
            timeLimitSeconds = Mathf.Max(0f, seconds);
        }

        public bool SetObjectiveMaxValue(string objectiveId, float maxValue)
        {
            SimTaskObjective objective = GetObjective(objectiveId);
            if (objective == null)
            {
                return false;
            }

            objective.SetMaxValue(maxValue);
            return true;
        }

        public bool TryFailForTimeout(float currentClock)
        {
            if (!failOnTimeout || !IsRunning || !HasTimedOut(currentClock))
            {
                return false;
            }

            FailTask(currentClock, string.IsNullOrWhiteSpace(timeoutFailureReason) ? "Timeout" : timeoutFailureReason);
            return true;
        }

        private void FinalizeTiming(float currentClock)
        {
            if (_state == TaskState.Running)
            {
                _accumulatedActiveSeconds += currentClock - _lastResumeClock;
                return;
            }

            if (_state == TaskState.Paused)
            {
                _accumulatedPausedSeconds += currentClock - _lastPauseClock;
            }
        }

        private void ResetRuntimeState()
        {
            _accumulatedActiveSeconds = 0f;
            _accumulatedPausedSeconds = 0f;
            _lastResumeClock = 0f;
            _lastPauseClock = 0f;
            _startedAtUtc = string.Empty;
            _endedAtUtc = string.Empty;
            _lastFailureReason = string.Empty;
            _currentObjectiveIndex = -1;
            _lastCompletedObjectiveIndex = -1;
            _lastReachedMilestoneIndex = -1;
        }

        private void AdvanceToNextObjective(float currentClock)
        {
            int nextObjectiveIndex = FindNextObjectiveIndex(_currentObjectiveIndex);
            _currentObjectiveIndex = nextObjectiveIndex;

            if (_currentObjectiveIndex < 0)
            {
                NotifyCurrentObjectiveChanged();
                return;
            }

            SimTaskObjective nextObjective = objectives[_currentObjectiveIndex];
            if (nextObjective == null || nextObjective.IsCompleted)
            {
                NotifyCurrentObjectiveChanged();
                return;
            }

            nextObjective.Begin(GetElapsedSeconds(currentClock));
            NotifyCurrentObjectiveChanged();
        }

        private bool UpdateObjective(
            string objectiveId,
            float currentClock,
            bool requireCurrentObjective,
            Func<SimTaskObjective, float, bool> mutation)
        {
            SimTaskObjective objective = GetObjective(objectiveId);
            if (objective == null)
            {
                return false;
            }

            bool wasCurrentObjective = objective == CurrentObjective;
            if (requireCurrentObjective && !wasCurrentObjective)
            {
                return false;
            }

            float elapsedSeconds = GetElapsedSeconds(currentClock);
            float previousValue = objective.CurrentValue;
            bool wasCompleted = objective.IsCompleted;
            bool isCompleted = mutation(objective, elapsedSeconds);
            bool progressChanged = !Mathf.Approximately(previousValue, objective.CurrentValue);

            if (progressChanged)
            {
                ObjectiveProgressChanged?.Invoke(this, objective);
            }

            if (isCompleted)
            {
                int objectiveIndex = GetObjectiveIndex(objective.ObjectiveId);
                MarkCompletedObjective(objectiveIndex);
            }

            if (!wasCompleted && objective.IsCompleted)
            {
                ObjectiveCompleted?.Invoke(this, objective);
            }

            if (wasCurrentObjective && isCompleted)
            {
                AdvanceToNextObjective(currentClock);
            }

            if (autoCompleteWhenAllRequiredObjectivesComplete && AreAllRequiredObjectivesCompleted)
            {
                EndTask(currentClock);
            }

            return true;
        }

        private void MarkCompletedObjective(int objectiveIndex)
        {
            if (objectiveIndex < 0)
            {
                return;
            }

            if (objectiveIndex > _lastCompletedObjectiveIndex)
            {
                _lastCompletedObjectiveIndex = objectiveIndex;
            }

            SimTaskObjective objective = objectives[objectiveIndex];
            if (objective != null && objective.IsMilestone)
            {
                _lastReachedMilestoneIndex = objectiveIndex;
            }
        }

        private int FindNextObjectiveIndex(int startIndex)
        {
            for (int i = startIndex + 1; i < objectives.Count; i++)
            {
                if (objectives[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CountRequiredObjectives(bool includeCompletedOnly)
        {
            int count = 0;
            for (int i = 0; i < objectives.Count; i++)
            {
                SimTaskObjective objective = objectives[i];
                if (objective == null || !objective.Required)
                {
                    continue;
                }

                if (!includeCompletedOnly || objective.IsCompleted)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindCompletedMilestoneBeforeIndex(int indexExclusive)
        {
            for (int i = Mathf.Min(indexExclusive - 1, objectives.Count - 1); i >= 0; i--)
            {
                SimTaskObjective objective = objectives[i];
                if (objective != null && objective.IsMilestone && objective.IsCompleted)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetRestartStepIndex()
        {
            if (_lastReachedMilestoneIndex >= 0)
            {
                return _lastReachedMilestoneIndex;
            }

            return objectives.Count > 0 ? 0 : -1;
        }

        private void NotifyCurrentObjectiveChanged()
        {
            CurrentObjectiveChanged?.Invoke(this, CurrentObjective);
        }
    }
}
