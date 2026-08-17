using AdaptiveSystem.Api;
using AdaptiveSystem.Models;
using UnityEngine;

namespace TaskSystem
{
    public class TaskAdaptiveBridge : MonoBehaviour
    {
        [SerializeField] private SimManager simManager;
        [SerializeField] private AdaptiveApiClient adaptiveApi;
        [SerializeField] private string sessionId;
        [SerializeField] private int difficultyLevel = 1;
        [SerializeField] private bool applyDifficultyLevelToLocalTaskDriver = true;
        [SerializeField] private bool startAdaptiveExecutionOnTaskStart = true;
        [SerializeField] private bool endAdaptiveExecutionOnTaskEnd = true;

        private string _taskExecutionId;

        public string SessionId => sessionId;

        public int DifficultyLevel => difficultyLevel;

        public string TaskExecutionId => _taskExecutionId;

        private void Awake()
        {
            if (simManager == null)
            {
                simManager = GetComponent<SimManager>();
            }
        }

        private void Reset()
        {
            simManager = GetComponent<SimManager>();
        }

        private void OnEnable()
        {
            SubscribeToSimulation();
        }

        private void OnDisable()
        {
            UnsubscribeFromSimulation();
        }

        public void ConfigureAdaptiveContext(string newSessionId, int newDifficultyLevel)
        {
            sessionId = newSessionId;
            difficultyLevel = newDifficultyLevel;
        }

        private void SubscribeToSimulation()
        {
            if (simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted -= HandleTaskStarted;
            simManager.LogicalTaskEnded -= HandleTaskEnded;
            simManager.LogicalTaskFailed -= HandleTaskFailed;
            simManager.LogicalTaskStarted += HandleTaskStarted;
            simManager.LogicalTaskEnded += HandleTaskEnded;
            simManager.LogicalTaskFailed += HandleTaskFailed;
        }

        private void UnsubscribeFromSimulation()
        {
            if (simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted -= HandleTaskStarted;
            simManager.LogicalTaskEnded -= HandleTaskEnded;
            simManager.LogicalTaskFailed -= HandleTaskFailed;
        }

        private void HandleTaskStarted(SimTask task)
        {
            ApplyDifficultyLevelToCurrentDriver();

            if (!startAdaptiveExecutionOnTaskStart)
            {
                return;
            }

            if (adaptiveApi == null || task == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(task.TaskId))
            {
                return;
            }

            StartCoroutine(adaptiveApi.StartTaskExecutionAsync(
                sessionId,
                task.TaskId,
                difficultyLevel,
                task.StartedAtUtc,
                OnAdaptiveTaskStarted));
        }

        private void ApplyDifficultyLevelToCurrentDriver()
        {
            if (!applyDifficultyLevelToLocalTaskDriver || simManager == null)
            {
                return;
            }

            if (simManager.CurrentTaskDriver is SimTaskDriver taskDriver)
            {
                taskDriver.DifficultyLevel = difficultyLevel;
            }
        }

        private void HandleTaskEnded(SimTask task)
        {
            EndAdaptiveExecution(task != null ? task.EndedAtUtc : string.Empty);
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            EndAdaptiveExecution(task != null ? task.EndedAtUtc : string.Empty);
        }

        private void EndAdaptiveExecution(string endTimeUtc)
        {
            if (!endAdaptiveExecutionOnTaskEnd)
            {
                return;
            }

            if (adaptiveApi == null || string.IsNullOrWhiteSpace(_taskExecutionId))
            {
                return;
            }

            StartCoroutine(adaptiveApi.EndTaskExecutionAsync(
                _taskExecutionId,
                endTimeUtc,
                OnAdaptiveTaskEnded));
        }

        private void OnAdaptiveTaskStarted(ApiResult<TaskStartResponse> result)
        {
            if (result == null || !result.IsSuccess || result.data == null)
            {
                Debug.LogWarning("Failed to start adaptive task execution.", this);
                return;
            }

            _taskExecutionId = result.data.task_execution_id;
        }

        private void OnAdaptiveTaskEnded(ApiResult<TaskEndResponse> result)
        {
            if (result == null || !result.IsSuccess || result.data == null)
            {
                Debug.LogWarning("Failed to end adaptive task execution.", this);
                return;
            }

            _taskExecutionId = result.data.task_execution_id;
        }
    }
}
