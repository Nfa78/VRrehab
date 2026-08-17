using UnityEngine;

namespace TaskSystem
{
    public interface ISimTaskDriver
    {
        string TaskId { get; }
        bool CanDriveTask(string candidateTaskId);
        void Bind(SimManager simManager, SimTask task);
        void Unbind();
        void OnTaskStarted();
        void OnTaskStopped();
        void OnTaskCompleted();
        void OnTaskFailed(string failureReason);
        void OnStepChanged(SimTaskObjective step, int stepIndex);
        void OnTaskResetToStep(SimTaskObjective step, int stepIndex);
    }

    public abstract class SimTaskDriver : MonoBehaviour, ISimTaskDriver
    {
        [SerializeField] private string taskId;
        [SerializeField, HideInInspector] private int difficultyLevel = 1;
        [SerializeField] private bool applyDifficultyOnTaskStarted = true;

        protected SimManager SimManager { get; private set; }
        protected SimTask SimTask { get; private set; }

        public virtual string TaskId => taskId;
        public int DifficultyLevel
        {
            get => Mathf.Max(1, difficultyLevel);
            set => difficultyLevel = Mathf.Max(1, value);
        }

        public bool ApplyDifficultyOnTaskStarted
        {
            get => applyDifficultyOnTaskStarted;
            set => applyDifficultyOnTaskStarted = value;
        }

        public virtual bool CanDriveTask(string candidateTaskId)
        {
            return !string.IsNullOrWhiteSpace(candidateTaskId) &&
                   string.Equals(TaskId, candidateTaskId, System.StringComparison.Ordinal);
        }

        public virtual void Bind(SimManager simManager, SimTask task)
        {
            SimManager = simManager;
            SimTask = task;
        }

        public virtual void Unbind()
        {
            SimManager = null;
            SimTask = null;
        }

        public virtual void OnTaskStarted()
        {
            if (applyDifficultyOnTaskStarted)
            {
                ApplyDifficulty(DifficultyLevel);
            }
        }

        public virtual void OnTaskStopped()
        {
        }

        public virtual void OnTaskCompleted()
        {
        }

        public virtual void OnTaskFailed(string failureReason)
        {
        }

        public virtual void OnStepChanged(SimTaskObjective step, int stepIndex)
        {
        }

        public virtual void OnTaskResetToStep(SimTaskObjective step, int stepIndex)
        {
        }

        public bool TryCompleteStep(string stepId = "")
        {
            return CompleteStep(stepId);
        }

        public bool TryAddStepProgress(string stepId, float delta = 1f)
        {
            return AddStepProgress(stepId, delta);
        }

        public bool TrySetStepState(string stepId, bool value)
        {
            return SetStepState(stepId, value);
        }

        public bool IsActiveStep(string stepId)
        {
            return IsCurrentStep(stepId);
        }

        public virtual void ApplyDifficulty(int level)
        {
        }

        protected bool CompleteStep(string stepId = "")
        {
            return SimTask != null && SimManager != null && SimManager.IsRunning && SimTask.CompleteObjective(stepId, SimManager.CurrentClock);
        }

        protected bool AddStepProgress(string stepId, float delta = 1f)
        {
            return SimTask != null && SimManager != null && SimManager.IsRunning && SimTask.AddObjectiveProgress(stepId, delta, SimManager.CurrentClock);
        }

        protected bool SetStepState(string stepId, bool value)
        {
            return SimTask != null && SimManager != null && SimManager.IsRunning && SimTask.SetObjectiveState(stepId, value, SimManager.CurrentClock);
        }

        protected bool IsCurrentStep(string stepId)
        {
            return SimTask != null &&
                   SimTask.CurrentObjective != null &&
                   string.Equals(SimTask.CurrentObjective.ObjectiveId, stepId, System.StringComparison.Ordinal);
        }
    }
}
