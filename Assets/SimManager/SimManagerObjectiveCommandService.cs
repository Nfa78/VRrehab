using System;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SimManagerObjectiveCommandService : MonoBehaviour
    {
        [SerializeField] private SimManager simManager;
        [SerializeField] private bool logObjectiveUpdates = true;

        [Header("Object Fall Recovery")]
        [SerializeField] private bool resetTrackedObjectWhenFallen = true;
        [SerializeField] private float fallDistanceBelowStart = 0.35f;
        [SerializeField] private bool failAndRetryActiveObjectiveOnObjectFall = true;
        [SerializeField] private float objectFallResetCooldown = 0.4f;

        private readonly SimManagerTrackedObjectRecovery _trackedObjectRecovery = new SimManagerTrackedObjectRecovery();

        private void Awake()
        {
            if (simManager == null)
            {
                simManager = GetComponent<SimManager>();
            }
        }

        private void OnEnable()
        {
            _trackedObjectRecovery.Clear();
        }

        private void Update()
        {
            SimTask task = simManager != null ? simManager.CurrentTask : null;
            if (simManager == null || !simManager.IsRunning || task == null)
            {
                return;
            }

            if (resetTrackedObjectWhenFallen)
            {
                _trackedObjectRecovery.HandleFalls(
                    task,
                    simManager.CurrentClock,
                    fallDistanceBelowStart,
                    objectFallResetCooldown,
                    failAndRetryActiveObjectiveOnObjectFall,
                    Log,
                    RetryTaskFromMilestone);
            }

            EnforceActiveObjectivePrerequisites(task);
        }

        public bool CompleteCurrentObjective(string objectiveId)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                "CompleteObjective",
                (task, id, currentClock) => task.CompleteObjective(id, currentClock));
        }

        public bool FailCurrentObjective(string objectiveId)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                "FailObjective",
                (task, id, currentClock) => task.FailObjective(id));
        }

        public bool ResetCurrentObjective(string objectiveId)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                "ResetObjective",
                (task, id, currentClock) => task.ResetObjective(id, currentClock));
        }

        public bool SetCurrentObjectiveState(string objectiveId, bool value)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                $"SetObjectiveState({value})",
                (task, id, currentClock) => task.SetObjectiveState(id, value, currentClock));
        }

        public bool SetTaskObjectiveState(string objectiveId, bool value)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                $"SetTaskObjectiveState({value})",
                (task, id, currentClock) => task.SetObjectiveStateAny(id, value, currentClock));
        }

        public bool SetCurrentObjectiveProgress(string objectiveId, float value)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                $"SetObjectiveProgress({value:0.##})",
                (task, id, currentClock) => task.SetObjectiveProgress(id, value, currentClock));
        }

        public bool AddCurrentObjectiveProgress(string objectiveId, float delta = 1f)
        {
            return ExecuteObjectiveCommand(
                objectiveId,
                $"AddObjectiveProgress({delta:0.##})",
                (task, id, currentClock) => task.AddObjectiveProgress(id, delta, currentClock));
        }

        public bool ResetCurrentTrackedObject(string objectId)
        {
            return simManager != null && simManager.CurrentTask != null && simManager.CurrentTask.ResetTrackedObject(objectId);
        }

        private bool FailActiveObjective()
        {
            return FailCurrentObjective(string.Empty);
        }

        private bool ResetActiveObjective()
        {
            return ResetCurrentObjective(string.Empty);
        }

        private bool RetryTaskFromMilestone()
        {
            return simManager != null && simManager.RetryCurrentTask();
        }

        private void EnforceActiveObjectivePrerequisites(SimTask task)
        {
            SimTaskObjective activeObjective = task.CurrentObjective;
            if (activeObjective == null)
            {
                return;
            }

            var requiredObjectiveIds = activeObjective.ActiveRequiresCompletedObjectiveIds;
            if (requiredObjectiveIds == null || requiredObjectiveIds.Count == 0)
            {
                return;
            }

            for (int index = 0; index < requiredObjectiveIds.Count; index++)
            {
                string requiredId = requiredObjectiveIds[index];
                if (string.IsNullOrWhiteSpace(requiredId) || task.IsObjectiveCompleted(requiredId))
                {
                    continue;
                }

                SimTaskObjective previousObjective = task.CurrentObjective;
                bool moved = task.MoveCurrentObjectiveTo(requiredId, simManager.CurrentClock, true);
                if (moved)
                {
                    Log(
                        $"Active objective prerequisite no longer valid. Reverted to prerequisite objective. Required={requiredId} Previous={GetObjectiveLabel(previousObjective)}");
                }
                else
                {
                    Log(
                        $"Active objective prerequisite missing and could not revert. Required={requiredId} Active={GetObjectiveLabel(previousObjective)}");
                }

                return;
            }
        }

        private bool ExecuteObjectiveCommand(
            string objectiveId,
            string action,
            Func<SimTask, string, float, bool> operation)
        {
            SimTask task = simManager != null ? simManager.CurrentTask : null;
            if (simManager == null || !simManager.IsRunning || task == null)
            {
                return false;
            }

            SimTaskObjective activeObjectiveBefore = task.CurrentObjective;
            SimTaskObjective objective = task.GetObjective(objectiveId);
            bool objectiveWasCompleted = objective != null && objective.IsCompleted;
            bool objectiveWasFailed = objective != null && objective.IsFailed;
            bool result = operation(task, objectiveId, simManager.CurrentClock);

            LogObjectiveUpdate(
                action,
                result,
                task,
                objective,
                activeObjectiveBefore,
                objectiveWasCompleted,
                objectiveWasFailed);

            return result;
        }

        private void LogObjectiveUpdate(
            string action,
            bool succeeded,
            SimTask task,
            SimTaskObjective objective,
            SimTaskObjective activeObjectiveBefore,
            bool objectiveWasCompleted,
            bool objectiveWasFailed)
        {
            if (!logObjectiveUpdates || task == null)
            {
                return;
            }

            SimTaskObjective activeObjectiveAfter = task.CurrentObjective;
            string objectiveLabel = objective != null ? GetObjectiveLabel(objective) : "<none>";
            string status = succeeded ? "OK" : "Rejected";

            Log(
                $"{action}: {status}. Task={GetTaskLabel(task)} Objective={objectiveLabel} ActiveObjective={GetObjectiveLabel(activeObjectiveAfter)}");

            if (!succeeded)
            {
                return;
            }

            if (objective != null && !objectiveWasCompleted && objective.IsCompleted)
            {
                Log($"Objective completed: {GetObjectiveLabel(objective)}. Task={GetTaskLabel(task)}.");
            }

            if (objective != null && !objectiveWasFailed && objective.IsFailed)
            {
                Log($"Objective failed: {GetObjectiveLabel(objective)}. Task={GetTaskLabel(task)}.");
            }

            if (activeObjectiveBefore != activeObjectiveAfter)
            {
                Log(
                    $"Active objective changed. Task={GetTaskLabel(task)} From={GetObjectiveLabel(activeObjectiveBefore)} To={GetObjectiveLabel(activeObjectiveAfter)}");
            }
        }

        private void Log(string message)
        {
            Debug.Log($"[SimManagerObjectives] {message}", this);
        }

        private static string GetTaskLabel(SimTask task)
        {
            if (task == null)
            {
                return "-";
            }

            return string.IsNullOrWhiteSpace(task.Title)
                ? task.TaskId
                : $"{task.Title} [{task.TaskId}]";
        }

        private static string GetObjectiveLabel(SimTaskObjective objective)
        {
            if (objective == null)
            {
                return "-";
            }

            return string.IsNullOrWhiteSpace(objective.Title)
                ? objective.ObjectiveId
                : $"{objective.Title} [{objective.ObjectiveId}]";
        }
    }
}
