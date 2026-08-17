using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TaskSystem
{
    [Serializable]
    public class SimTaskChangedEvent : UnityEvent<SimTask> { }

    [Serializable]
    public class SimTaskObjectiveChangedEvent : UnityEvent<SimTaskObjective> { }

    [Serializable]
    public class SimManagerStateChangedEvent : UnityEvent<SimManager.SimulationState> { }

    public class SimManager : MonoBehaviour
    {
        public enum SimulationState
        {
            Idle,
            Running,
            Paused,
            Completed,
            Failed,
            Stopped
        }

        public enum FailurePolicy
        {
            StopSimulation,
            ContinueToNextTask,
            RetryCurrentTask
        }

        [Header("Task Sequence")]
        [SerializeField] private List<SimTask> tasks = new List<SimTask>();
        [SerializeField] private int startTaskIndex;
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private bool resetTasksBeforeStart = true;

        [Header("Runtime Policy")]
        [SerializeField] private FailurePolicy failurePolicy = FailurePolicy.StopSimulation;
        [SerializeField] private bool failTaskOnTimeout = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool freezeUnityTimeWhenPaused = true;
        [SerializeField] [Min(1)] private int targetCycles = 1;
        [SerializeField] [Min(0f)] private float taskTransitionDelaySeconds = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSimulationStarted;
        [SerializeField] private UnityEvent onSimulationPaused;
        [SerializeField] private UnityEvent onSimulationResumed;
        [SerializeField] private UnityEvent onSimulationCompleted;
        [SerializeField] private UnityEvent onSimulationFailed;
        [SerializeField] private UnityEvent onSimulationStopped;
        [SerializeField] private SimTaskChangedEvent onCurrentTaskChanged;
        [SerializeField] private SimTaskChangedEvent onTaskCompleted;
        [SerializeField] private SimTaskObjectiveChangedEvent onObjectiveProgressChanged;
        [SerializeField] private SimTaskObjectiveChangedEvent onObjectiveCompleted;
        [SerializeField] private SimManagerStateChangedEvent onSimulationStateChanged;

        private SimulationState _state = SimulationState.Idle;
        private int _currentTaskIndex = -1;
        private int _currentTaskCycle = 1;
        private int _currentTaskTargetCycles = 1;
        private bool _logicalTaskStartRaised;
        private Coroutine _pendingTaskTransitionCoroutine;
        private float _taskTransitionDelayRemainingSeconds;
        private ISimTaskDriver _currentTaskDriver;
        private float _timeScaleBeforePause = 1f;
        private bool _unityTimeFrozen;

        public event Action<SimTask> LogicalTaskStarted;
        public event Action<SimTask> LogicalTaskEnded;
        public event Action<SimTask> LogicalTaskCompleted;
        public event Action<SimTask, string> LogicalTaskFailed;
        public event Action<SimTask, SimTaskObjective> LogicalTaskStepChanged;
        public event Action<SimTask, SimTaskObjective> LogicalTaskObjectiveProgressChanged;
        public event Action<SimTask, SimTaskObjective> LogicalTaskObjectiveCompleted;
        public event Action<SimulationState> SimulationStateChanged;

        public IReadOnlyList<SimTask> Tasks => tasks;
        public SimulationState State => _state;
        public int CurrentTaskIndex => _currentTaskIndex;
        public SimTask CurrentTask => _currentTaskIndex >= 0 && _currentTaskIndex < tasks.Count ? tasks[_currentTaskIndex] : null;
        public SimTaskObjective CurrentObjective => CurrentTask != null ? CurrentTask.CurrentObjective : null;
        public bool IsRunning => _state == SimulationState.Running;
        public bool IsPaused => _state == SimulationState.Paused;
        public int CurrentTaskCycle => _currentTaskCycle;
        public int CurrentTaskTargetCycles => _currentTaskTargetCycles;
        public float TaskTransitionDelaySeconds => Mathf.Max(0f, taskTransitionDelaySeconds);
        public bool IsTaskTransitionPending => _pendingTaskTransitionCoroutine != null;
        public float TaskTransitionDelayRemainingSeconds => Mathf.Max(0f, _taskTransitionDelayRemainingSeconds);
        public ISimTaskDriver CurrentTaskDriver => _currentTaskDriver;
        internal float CurrentClock => useUnscaledTime ? Time.unscaledTime : Time.time;

        private void OnEnable()
        {
            SubscribeToTasks();

            if (autoStartOnEnable)
            {
                StartSimulation();
            }
        }

        private void OnDisable()
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();
            UnbindCurrentTaskDriver(notifyStopped: true);
            UnsubscribeFromTasks();
        }

        private void Update()
        {
            if (!IsRunning || CurrentTask == null)
            {
                return;
            }

            if (failTaskOnTimeout)
            {
                CurrentTask.TryFailForTimeout(CurrentClock);
            }
        }

        public void StartSimulation()
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();

            if (tasks.Count == 0)
            {
                Debug.LogWarning("SimManager cannot start because no tasks are configured.", this);
                return;
            }

            if (resetTasksBeforeStart)
            {
                ResetAllTasks();
            }

            int clampedStartIndex = Mathf.Clamp(startTaskIndex, 0, tasks.Count - 1);
            SetSimulationState(SimulationState.Running);
            onSimulationStarted?.Invoke();
            RunTaskAtIndex(clampedStartIndex);
        }

        public void PauseSimulation()
        {
            if (!IsRunning)
            {
                return;
            }

            CurrentTask?.PauseTask(CurrentClock);
            FreezeUnityTime();
            SetSimulationState(SimulationState.Paused);
            onSimulationPaused?.Invoke();
        }

        public void ResumeSimulation()
        {
            if (!IsPaused)
            {
                return;
            }

            RestoreUnityTime();
            CurrentTask?.ResumeTask(CurrentClock);
            SetSimulationState(SimulationState.Running);
            onSimulationResumed?.Invoke();
        }

        public void StopSimulation()
        {
            if (_state == SimulationState.Stopped)
            {
                return;
            }

            CancelPendingTaskTransition();
            RestoreUnityTime();
            CurrentTask?.PauseTask(CurrentClock);
            UnbindCurrentTaskDriver(notifyStopped: true);
            _currentTaskIndex = -1;
            SetSimulationState(SimulationState.Stopped);
            onSimulationStopped?.Invoke();
        }

        public void ResetSimulation()
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();
            ResetAllTasks();
            UnbindCurrentTaskDriver(notifyStopped: false);
            _currentTaskIndex = -1;
            SetSimulationState(SimulationState.Idle);
        }

        public bool RunTaskAtIndex(int taskIndex)
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();

            if (taskIndex < 0 || taskIndex >= tasks.Count)
            {
                return false;
            }

            if (_currentTaskIndex != taskIndex)
            {
                UnbindCurrentTaskDriver(notifyStopped: false);
            }

            _currentTaskIndex = taskIndex;
            _logicalTaskStartRaised = false;
            BeginTaskRepeatState();

            if (CurrentTask == null)
            {
                return false;
            }

            if (_state != SimulationState.Running)
            {
                SetSimulationState(SimulationState.Running);
            }

            onCurrentTaskChanged?.Invoke(CurrentTask);
            StartTaskRun(CurrentTask);
            return true;
        }

        public bool AdvanceToNextTask()
        {
            CancelPendingTaskTransition();

            int nextIndex = _currentTaskIndex + 1;
            if (nextIndex >= tasks.Count)
            {
                CompleteSimulation();
                return false;
            }

            if (TaskTransitionDelaySeconds > 0f)
            {
                _taskTransitionDelayRemainingSeconds = TaskTransitionDelaySeconds;
                _pendingTaskTransitionCoroutine = StartCoroutine(AdvanceToTaskAfterDelay(nextIndex));
                return true;
            }

            _taskTransitionDelayRemainingSeconds = 0f;
            return RunTaskAtIndex(nextIndex);
        }

        public bool RetryCurrentTask()
        {
            CancelPendingTaskTransition();

            if (CurrentTask == null)
            {
                return false;
            }

            bool restarted = CurrentTask.RestartFromLastMilestone(CurrentClock);
            if (!restarted)
            {
                CurrentTask.ResetTask();
                CurrentTask.StartTask(CurrentClock);
            }

            _currentTaskDriver?.OnTaskResetToStep(CurrentTask.CurrentObjective, CurrentTask.CurrentObjectiveIndex);
            return true;
        }

        public void ResetAllTasks()
        {
            for (int index = 0; index < tasks.Count; index++)
            {
                tasks[index]?.ResetTask();
            }
        }

        public float GetCurrentTaskElapsedSeconds()
        {
            return CurrentTask != null ? CurrentTask.GetElapsedSeconds(CurrentClock) : 0f;
        }

        private void StartTaskRun(SimTask task)
        {
            BindTaskDriver(task);
            task.StartTask(CurrentClock);
            RaiseLogicalTaskStartedIfNeeded(task);
            _currentTaskDriver?.OnTaskStarted();
        }

        private void RestartTaskCycle(SimTask task)
        {
            _currentTaskCycle++;
            task.ResetTask();
            task.StartTask(CurrentClock);
            _currentTaskDriver?.OnTaskResetToStep(task.CurrentObjective, task.CurrentObjectiveIndex);
        }

        private void CompleteSimulation()
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();
            UnbindCurrentTaskDriver(notifyStopped: false);
            SetSimulationState(SimulationState.Completed);
            onSimulationCompleted?.Invoke();
            _currentTaskIndex = -1;
        }

        private void FailSimulation()
        {
            RestoreUnityTime();
            CancelPendingTaskTransition();
            UnbindCurrentTaskDriver(notifyStopped: false);
            _currentTaskIndex = -1;
            SetSimulationState(SimulationState.Failed);
            onSimulationFailed?.Invoke();
        }

        private void HandleTaskEnded(SimTask task)
        {
            if (task == null || task != CurrentTask)
            {
                return;
            }

            if (ShouldRepeatCurrentTask())
            {
                RestartTaskCycle(task);
                return;
            }

            LogicalTaskCompleted?.Invoke(task);
            onTaskCompleted?.Invoke(task);
            LogicalTaskEnded?.Invoke(task);
            _currentTaskDriver?.OnTaskCompleted();
            UnbindCurrentTaskDriver(notifyStopped: false);
            AdvanceToNextTask();
        }

        private IEnumerator AdvanceToTaskAfterDelay(int nextTaskIndex)
        {
            float remainingSeconds = TaskTransitionDelaySeconds;
            _taskTransitionDelayRemainingSeconds = remainingSeconds;
            while (remainingSeconds > 0f)
            {
                if (_state == SimulationState.Stopped ||
                    _state == SimulationState.Completed ||
                    _state == SimulationState.Failed)
                {
                    _pendingTaskTransitionCoroutine = null;
                    _taskTransitionDelayRemainingSeconds = 0f;
                    yield break;
                }

                if (_state == SimulationState.Running)
                {
                    remainingSeconds -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    _taskTransitionDelayRemainingSeconds = Mathf.Max(0f, remainingSeconds);
                }

                yield return null;
            }

            _pendingTaskTransitionCoroutine = null;
            _taskTransitionDelayRemainingSeconds = 0f;
            RunTaskAtIndex(nextTaskIndex);
        }

        private void CancelPendingTaskTransition()
        {
            if (_pendingTaskTransitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(_pendingTaskTransitionCoroutine);
            _pendingTaskTransitionCoroutine = null;
            _taskTransitionDelayRemainingSeconds = 0f;
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            if (task == null || task != CurrentTask)
            {
                return;
            }

            LogicalTaskFailed?.Invoke(task, failureReason);
            _currentTaskDriver?.OnTaskFailed(failureReason);

            switch (failurePolicy)
            {
                case FailurePolicy.ContinueToNextTask:
                    UnbindCurrentTaskDriver(notifyStopped: false);
                    AdvanceToNextTask();
                    break;
                case FailurePolicy.RetryCurrentTask:
                    RetryCurrentTask();
                    break;
                default:
                    FailSimulation();
                    break;
            }
        }

        private void HandleTaskObjectiveChanged(SimTask task, SimTaskObjective currentObjective)
        {
            if (task == null || task != CurrentTask)
            {
                return;
            }

            LogicalTaskStepChanged?.Invoke(task, currentObjective);
            _currentTaskDriver?.OnStepChanged(currentObjective, task.CurrentObjectiveIndex);
        }

        private void HandleTaskObjectiveProgressChanged(SimTask task, SimTaskObjective objective)
        {
            if (task == null || task != CurrentTask)
            {
                return;
            }

            LogicalTaskObjectiveProgressChanged?.Invoke(task, objective);
            onObjectiveProgressChanged?.Invoke(objective);
        }

        private void HandleTaskObjectiveCompleted(SimTask task, SimTaskObjective objective)
        {
            if (task == null || task != CurrentTask)
            {
                return;
            }

            LogicalTaskObjectiveCompleted?.Invoke(task, objective);
            onObjectiveCompleted?.Invoke(objective);
        }

        private void SubscribeToTasks()
        {
            for (int index = 0; index < tasks.Count; index++)
            {
                SimTask task = tasks[index];
                if (task == null)
                {
                    continue;
                }

                task.Ended -= HandleTaskEnded;
                task.Failed -= HandleTaskFailed;
                task.CurrentObjectiveChanged -= HandleTaskObjectiveChanged;
                task.ObjectiveProgressChanged -= HandleTaskObjectiveProgressChanged;
                task.ObjectiveCompleted -= HandleTaskObjectiveCompleted;
                task.Ended += HandleTaskEnded;
                task.Failed += HandleTaskFailed;
                task.CurrentObjectiveChanged += HandleTaskObjectiveChanged;
                task.ObjectiveProgressChanged += HandleTaskObjectiveProgressChanged;
                task.ObjectiveCompleted += HandleTaskObjectiveCompleted;
            }
        }

        private void UnsubscribeFromTasks()
        {
            for (int index = 0; index < tasks.Count; index++)
            {
                SimTask task = tasks[index];
                if (task == null)
                {
                    continue;
                }

                task.Ended -= HandleTaskEnded;
                task.Failed -= HandleTaskFailed;
                task.CurrentObjectiveChanged -= HandleTaskObjectiveChanged;
                task.ObjectiveProgressChanged -= HandleTaskObjectiveProgressChanged;
                task.ObjectiveCompleted -= HandleTaskObjectiveCompleted;
            }
        }

        private void SetSimulationState(SimulationState newState)
        {
            if (_state == newState)
            {
                return;
            }

            _state = newState;
            SimulationStateChanged?.Invoke(_state);
            onSimulationStateChanged?.Invoke(_state);
        }

        private void FreezeUnityTime()
        {
            if (!freezeUnityTimeWhenPaused || _unityTimeFrozen)
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _unityTimeFrozen = true;
        }

        private void RestoreUnityTime()
        {
            if (!_unityTimeFrozen)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _unityTimeFrozen = false;
        }

        private void RaiseLogicalTaskStartedIfNeeded(SimTask task)
        {
            if (_logicalTaskStartRaised || task == null)
            {
                return;
            }

            _logicalTaskStartRaised = true;
            LogicalTaskStarted?.Invoke(task);
        }

        private void BeginTaskRepeatState()
        {
            _currentTaskCycle = 1;
            _currentTaskTargetCycles = Mathf.Max(1, targetCycles);
        }

        private bool ShouldRepeatCurrentTask()
        {
            return _currentTaskCycle < _currentTaskTargetCycles;
        }

        private void BindTaskDriver(SimTask task)
        {
            UnbindCurrentTaskDriver(notifyStopped: false);

            _currentTaskDriver = ResolveTaskDriver(task);
            if (_currentTaskDriver == null)
            {
                return;
            }

            _currentTaskDriver.Bind(this, task);
            if (_currentTaskDriver is SimTaskDriver simTaskDriver)
            {
                simTaskDriver.DifficultyLevel = task.DifficultyProfileLevel;
            }
        }

        private void UnbindCurrentTaskDriver(bool notifyStopped)
        {
            if (_currentTaskDriver == null)
            {
                return;
            }

            if (notifyStopped)
            {
                _currentTaskDriver.OnTaskStopped();
            }

            _currentTaskDriver.Unbind();
            _currentTaskDriver = null;
        }

        private ISimTaskDriver ResolveTaskDriver(SimTask task)
        {
            if (task == null)
            {
                return null;
            }

            if (task.TaskDriverComponent is ISimTaskDriver explicitDriver &&
                explicitDriver.CanDriveTask(task.TaskId))
            {
                return explicitDriver;
            }

            SimTaskDriver[] drivers = FindObjectsByType<SimTaskDriver>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < drivers.Length; i++)
            {
                SimTaskDriver driver = drivers[i];
                if (driver != null && driver.CanDriveTask(task.TaskId))
                {
                    return driver;
                }
            }

            return null;
        }
    }
}
