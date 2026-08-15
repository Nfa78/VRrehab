using TMPro;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SimManagerDebugView : MonoBehaviour
    {
        [SerializeField] private SimManager simManager;
        [SerializeField] private SimManagerObjectiveCommandService objectiveCommands;
        [SerializeField] private bool showRuntimeOverlay = true;
        [SerializeField] private Vector2 overlayPosition = new Vector2(16f, 16f);
        [SerializeField] private float overlayWidth = 360f;
        [SerializeField] private TMP_Text statusText;

        private SimManagerDiagnostics _diagnostics;
        private SimObjectiveInteraction[] _objectiveInteractions;
        private float _nextObjectiveInteractionRefreshTime;
        private const float ObjectiveInteractionRefreshIntervalSeconds = 1f;

        private void Awake()
        {
            if (simManager == null)
            {
                simManager = GetComponent<SimManager>();
            }

            if (objectiveCommands == null)
            {
                objectiveCommands = GetComponent<SimManagerObjectiveCommandService>();
            }

            if (objectiveCommands == null && simManager != null)
            {
                objectiveCommands = simManager.GetComponent<SimManagerObjectiveCommandService>();
            }
        }

        private void OnEnable()
        {
            _diagnostics ??= new SimManagerDiagnostics(statusText);
            _diagnostics.StatusText = statusText;
            RefreshObjectiveInteractions();
            RefreshStatusText();
        }

        private void Update()
        {
            RefreshObjectiveInteractionsIfNeeded();
            RefreshStatusText();
        }

        private void OnGUI()
        {
            if (simManager == null || _diagnostics == null)
            {
                return;
            }

            bool completeCurrentObjective = _diagnostics.DrawRuntimeOverlay(
                showRuntimeOverlay,
                overlayPosition,
                overlayWidth,
                simManager.State,
                simManager.CurrentTaskIndex,
                simManager.Tasks.Count,
                simManager.CurrentTask,
                simManager.CurrentObjective,
                simManager.CurrentTaskCycle,
                simManager.CurrentTaskTargetCycles,
                simManager.IsTaskTransitionPending,
                simManager.TaskTransitionDelayRemainingSeconds,
                simManager.CurrentClock,
                _objectiveInteractions);

            if (completeCurrentObjective)
            {
                CompleteCurrentObjectiveFromDebugView();
            }
        }

        public void LogCurrentState()
        {
            if (simManager == null)
            {
                return;
            }

            Debug.Log(
                _diagnostics.BuildStateSnapshot(
                    simManager.State,
                    simManager.CurrentTaskIndex,
                    simManager.Tasks.Count,
                    simManager.CurrentTask,
                    simManager.CurrentObjective,
                    simManager.CurrentTaskCycle,
                    simManager.CurrentTaskTargetCycles,
                    simManager.IsTaskTransitionPending,
                    simManager.TaskTransitionDelayRemainingSeconds,
                    simManager.CurrentClock,
                    _objectiveInteractions),
                this);
        }

        private void RefreshStatusText()
        {
            if (simManager == null || _diagnostics == null)
            {
                return;
            }

            _diagnostics.RefreshStatusText(
                simManager.CurrentTask,
                simManager.CurrentObjective,
                simManager.CurrentTaskTargetCycles > 1,
                simManager.CurrentTaskCycle,
                simManager.CurrentTaskTargetCycles);
        }

        private void CompleteCurrentObjectiveFromDebugView()
        {
            if (simManager == null || simManager.CurrentTask == null || simManager.CurrentObjective == null)
            {
                return;
            }

            if (objectiveCommands != null && objectiveCommands.CompleteCurrentObjective(string.Empty))
            {
                return;
            }

            bool completed = simManager.CurrentTask.CompleteObjective(string.Empty, simManager.CurrentClock);
            if (!completed)
            {
                Debug.LogWarning("SimManager debug view could not complete the current objective.", this);
            }
        }

        private void RefreshObjectiveInteractionsIfNeeded()
        {
            if (Time.unscaledTime < _nextObjectiveInteractionRefreshTime)
            {
                return;
            }

            RefreshObjectiveInteractions();
        }

        private void RefreshObjectiveInteractions()
        {
            _nextObjectiveInteractionRefreshTime = Time.unscaledTime + ObjectiveInteractionRefreshIntervalSeconds;
            _objectiveInteractions = FindObjectsByType<SimObjectiveInteraction>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }
    }
}
