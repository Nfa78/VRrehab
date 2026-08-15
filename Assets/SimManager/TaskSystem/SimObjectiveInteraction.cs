using UnityEngine;

namespace TaskSystem
{
    public class SimObjectiveInteraction : MonoBehaviour
    {
        [SerializeField] private SimManagerObjectiveCommandService objectiveCommands;
        [SerializeField] private string objectiveId;
        [SerializeField] private bool updateWhenObjectiveIsNotCurrent;
        private bool loggedMissingObjectiveCommands;

        public string ObjectiveId => objectiveId;

        private void Awake()
        {
            ResolveObjectiveCommands();
        }

        public bool SetObjectiveState(bool value)
        {
            if (!ResolveObjectiveCommands())
            {
                return false;
            }

            if (updateWhenObjectiveIsNotCurrent)
            {
                return objectiveCommands.SetTaskObjectiveState(objectiveId, value);
            }

            return objectiveCommands.SetCurrentObjectiveState(objectiveId, value);
        }

        public bool SetObjectiveProgress(float value)
        {
            return ResolveObjectiveCommands() && objectiveCommands.SetCurrentObjectiveProgress(objectiveId, value);
        }

        public bool AddObjectiveProgress(float delta)
        {
            return ResolveObjectiveCommands() && objectiveCommands.AddCurrentObjectiveProgress(objectiveId, delta);
        }

        public bool CompleteObjective()
        {
            return ResolveObjectiveCommands() && objectiveCommands.CompleteCurrentObjective(objectiveId);
        }

        public bool FailObjective()
        {
            return ResolveObjectiveCommands() && objectiveCommands.FailCurrentObjective(objectiveId);
        }

        public bool ResetObjective()
        {
            return ResolveObjectiveCommands() && objectiveCommands.ResetCurrentObjective(objectiveId);
        }

        public void SetTrue()
        {
            SetObjectiveState(true);
        }

        public void SetFalse()
        {
            SetObjectiveState(false);
        }

        public void Increment()
        {
            AddObjectiveProgress(1f);
        }

        private bool ResolveObjectiveCommands()
        {
            if (objectiveCommands != null)
            {
                return true;
            }

            objectiveCommands = FindFirstObjectByType<SimManagerObjectiveCommandService>();
            if (objectiveCommands != null)
            {
                return true;
            }

            SimManager simManager = FindFirstObjectByType<SimManager>();
            if (simManager != null)
            {
                objectiveCommands = simManager.GetComponent<SimManagerObjectiveCommandService>();
                if (objectiveCommands == null)
                {
                    objectiveCommands = simManager.gameObject.AddComponent<SimManagerObjectiveCommandService>();
                }

                return objectiveCommands != null;
            }

            if (!loggedMissingObjectiveCommands)
            {
                loggedMissingObjectiveCommands = true;
                Debug.LogWarning(
                    $"SimObjectiveInteraction on {name} cannot update objective '{objectiveId}' because no SimManagerObjectiveCommandService or SimManager was found.",
                    this);
            }

            return false;
        }
    }
}
