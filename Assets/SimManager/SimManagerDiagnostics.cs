using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace TaskSystem
{
    internal sealed class SimManagerDiagnostics
    {
        private GUIStyle _overlayBoxStyle;
        private GUIStyle _overlayHeaderStyle;
        private GUIStyle _overlayBodyStyle;
        private GUIStyle _overlayButtonStyle;

        public SimManagerDiagnostics(TMP_Text statusText)
        {
            StatusText = statusText;
        }

        public TMP_Text StatusText { get; set; }

        public void LogDebug(bool enabled, bool includeSnapshot, string message, MonoBehaviour owner, Func<string> buildStateSnapshot)
        {
            if (!enabled)
            {
                return;
            }

            Debug.Log($"[SimManager] {message}", owner);

            if (includeSnapshot && buildStateSnapshot != null)
            {
                Debug.Log(buildStateSnapshot(), owner);
            }
        }

        public void RefreshStatusText(
            SimTask currentTask,
            SimTaskObjective currentObjective,
            bool repeatsEnabled,
            int currentCycle,
            int targetCycles)
        {
            if (StatusText == null)
            {
                return;
            }

            string taskName = currentTask != null
                ? (string.IsNullOrWhiteSpace(currentTask.Title) ? currentTask.TaskId : currentTask.Title)
                : "-";
            string objectiveName = currentObjective != null
                ? (string.IsNullOrWhiteSpace(currentObjective.Title) ? currentObjective.ObjectiveId : currentObjective.Title)
                : "-";
            string requiredObjectivesDone = currentTask != null
                ? $"{currentTask.CompletedRequiredObjectiveCount}/{currentTask.RequiredObjectiveCount} required objectives done"
                : "0/0 required objectives done";
            string currentObjectiveProgress = currentObjective != null
                ? BuildObjectiveProgress(currentObjective)
                : "-";
            string completionCondition = currentObjective != null
                ? BuildObjectiveCompletionCondition(currentObjective)
                : "-";

            StatusText.text =
                $"Task [{taskName}] {requiredObjectivesDone} | Current Objective [{objectiveName}] {currentObjectiveProgress} | Complete: {completionCondition}";
            if (repeatsEnabled)
            {
                StatusText.text += $" | Cycle {currentCycle}/{targetCycles}";
            }
        }

        public bool DrawRuntimeOverlay(
            bool showRuntimeOverlay,
            Vector2 overlayPosition,
            float overlayWidth,
            SimManager.SimulationState state,
            int currentTaskIndex,
            int taskCount,
            SimTask currentTask,
            SimTaskObjective currentObjective,
            int currentCycle,
            int targetCycles,
            bool isTaskTransitionPending,
            float taskTransitionDelayRemainingSeconds,
            float currentClock,
            IReadOnlyList<SimObjectiveInteraction> objectiveInteractions)
        {
            if (!showRuntimeOverlay)
            {
                return false;
            }

            EnsureOverlayStyles();
            int previousGuiDepth = GUI.depth;
            GUI.depth = -1000;

            float taskElapsed = currentTask != null ? currentTask.GetElapsedSeconds(currentClock) : 0f;
            float taskPaused = currentTask != null ? currentTask.GetPausedSeconds(currentClock) : 0f;
            string requiredObjectivesDone = currentTask != null && currentTask.RequiredObjectiveCount > 0
                ? $"{currentTask.CompletedRequiredObjectiveCount}/{currentTask.RequiredObjectiveCount}"
                : "0/0";
            string currentObjectiveProgress = currentObjective != null ? BuildObjectiveProgress(currentObjective) : "None";
            string currentObjectiveCompletion = currentObjective != null ? BuildObjectiveCompletionCondition(currentObjective) : "-";
            string currentObjectiveOrdinal = currentTask != null && currentTask.CurrentObjectiveIndex >= 0
                ? $"{currentTask.CurrentObjectiveIndex + 1}/{currentTask.Objectives.Count}"
                : "-";

            string objectiveDebugText = BuildObjectiveDebugText(currentTask, currentObjective, objectiveInteractions);
            string overlayText =
                $"Simulation: {state}\n" +
                $"Task Index: {(currentTaskIndex >= 0 ? (currentTaskIndex + 1).ToString() : "-")}/{taskCount}\n" +
                $"Current Task: {(currentTask != null ? currentTask.Title : "None")}\n" +
                $"Task Id: {(currentTask != null ? currentTask.TaskId : "-")}\n" +
                $"Task Time: {taskElapsed:0.00}s\n" +
                $"Task Cycle: {currentCycle}/{targetCycles}\n" +
                $"Task Buffer: {(isTaskTransitionPending ? $"{taskTransitionDelayRemainingSeconds:0.00}s" : "-")}\n" +
                $"Paused Time: {taskPaused:0.00}s\n" +
                $"Required Objectives Done: {requiredObjectivesDone}\n" +
                $"Current Objective: {currentObjectiveOrdinal} {(currentObjective != null ? currentObjective.Title : "None")}\n" +
                $"Objective Id: {(currentObjective != null ? currentObjective.ObjectiveId : "-")}\n" +
                $"Objective Mode: {(currentObjective != null ? currentObjective.Mode.ToString() : "-")}\n" +
                $"Current Objective Progress: {currentObjectiveProgress}\n" +
                $"Current Objective Completes When: {currentObjectiveCompletion}\n" +
                $"Task Failure: {(currentTask != null && !string.IsNullOrWhiteSpace(currentTask.LastFailureReason) ? currentTask.LastFailureReason : "-")}\n" +
                objectiveDebugText;

            float desiredBoxHeight = Mathf.Clamp(252f + CountLines(objectiveDebugText) * 18f, 300f, 560f);
            float maxVisibleBoxHeight = Mathf.Max(180f, Screen.height - overlayPosition.y - 8f);
            float boxHeight = Mathf.Min(desiredBoxHeight, maxVisibleBoxHeight);
            Rect boxRect = new Rect(overlayPosition.x, overlayPosition.y, overlayWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, _overlayBoxStyle);

            Rect headerRect = new Rect(boxRect.x + 12f, boxRect.y + 10f, boxRect.width - 172f, 24f);
            GUI.Label(headerRect, "SimManager Runtime", _overlayHeaderStyle);

            bool canCompleteCurrentObjective =
                currentTask != null &&
                currentObjective != null &&
                !currentObjective.IsCompleted;

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && canCompleteCurrentObjective;

            Rect buttonRect = new Rect(boxRect.xMax - 148f, boxRect.y + 8f, 136f, 24f);
            bool clicked = GUI.Button(buttonRect, "Complete Obj", _overlayButtonStyle);

            GUI.enabled = previousGuiEnabled;

            Rect bodyRect = new Rect(boxRect.x + 12f, boxRect.y + 42f, boxRect.width - 24f, boxRect.height - 54f);
            GUI.Label(bodyRect, overlayText, _overlayBodyStyle);
            GUI.depth = previousGuiDepth;
            return canCompleteCurrentObjective && clicked;
        }

        public string BuildStateSnapshot(
            SimManager.SimulationState state,
            int currentTaskIndex,
            int taskCount,
            SimTask currentTask,
            SimTaskObjective currentObjective,
            int currentCycle,
            int targetCycles,
            bool isTaskTransitionPending,
            float taskTransitionDelayRemainingSeconds,
            float currentClock,
            IReadOnlyList<SimObjectiveInteraction> objectiveInteractions = null)
        {
            float taskElapsed = currentTask != null ? currentTask.GetElapsedSeconds(currentClock) : 0f;
            float taskPaused = currentTask != null ? currentTask.GetPausedSeconds(currentClock) : 0f;
            string requiredObjectivesDone = currentTask != null
                ? $"{currentTask.CompletedRequiredObjectiveCount}/{currentTask.RequiredObjectiveCount}"
                : "0/0";
            string currentObjectiveProgress = currentObjective != null ? BuildObjectiveProgress(currentObjective) : "-";
            string currentObjectiveCompletion = currentObjective != null ? BuildObjectiveCompletionCondition(currentObjective) : "-";

            return
                "[SimManager State]\n" +
                $"Simulation: {state}\n" +
                $"Task Index: {(currentTaskIndex >= 0 ? (currentTaskIndex + 1).ToString() : "-")}/{taskCount}\n" +
                $"Current Task: {(currentTask != null ? GetTaskLabel(currentTask) : "-")}\n" +
                $"Task Time: {taskElapsed:0.00}s\n" +
                $"Task Cycle: {currentCycle}/{targetCycles}\n" +
                $"Task Buffer: {(isTaskTransitionPending ? $"{taskTransitionDelayRemainingSeconds:0.00}s" : "-")}\n" +
                $"Paused Time: {taskPaused:0.00}s\n" +
                $"Required Objectives Done: {requiredObjectivesDone}\n" +
                $"Current Objective: {(currentObjective != null ? GetObjectiveLabel(currentObjective) : "-")}\n" +
                $"Current Objective Progress: {currentObjectiveProgress}\n" +
                $"Current Objective Completes When: {currentObjectiveCompletion}\n" +
                $"Failure: {(currentTask != null && !string.IsNullOrWhiteSpace(currentTask.LastFailureReason) ? currentTask.LastFailureReason : "-")}\n" +
                BuildObjectiveDebugText(currentTask, currentObjective, objectiveInteractions);
        }

        private void EnsureOverlayStyles()
        {
            if (_overlayBoxStyle != null)
            {
                return;
            }

            _overlayBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 12, 12)
            };

            _overlayHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            _overlayBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };

            _overlayButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 24
            };
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

        private static string BuildObjectiveDebugText(
            SimTask currentTask,
            SimTaskObjective currentObjective,
            IReadOnlyList<SimObjectiveInteraction> objectiveInteractions)
        {
            if (currentTask == null || currentTask.Objectives == null || currentTask.Objectives.Count == 0)
            {
                return "Objectives: none";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Objectives:");

            int objectiveCount = currentTask.Objectives.Count;
            for (int index = 0; index < objectiveCount; index++)
            {
                SimTaskObjective objective = currentTask.Objectives[index];
                if (objective == null)
                {
                    continue;
                }

                string marker = objective == currentObjective ? ">" : " ";
                builder.Append(marker)
                    .Append(' ')
                    .Append(GetObjectiveShortLabel(objective))
                    .Append(" | ")
                    .Append(BuildObjectiveState(objective))
                    .Append(" | ")
                    .Append(BuildObjectiveProgress(objective))
                    .Append(" | ")
                    .Append(BuildObjectiveCompletionRule(objective))
                    .AppendLine();

                string sourceText = BuildObjectiveSourceText(objective, currentObjective, objectiveInteractions);
                if (!string.IsNullOrWhiteSpace(sourceText))
                {
                    builder.Append("    Sources: ").AppendLine(sourceText);
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetObjectiveShortLabel(SimTaskObjective objective)
        {
            if (objective == null)
            {
                return "-";
            }

            string id = string.IsNullOrWhiteSpace(objective.ObjectiveId) ? "<empty id>" : objective.ObjectiveId;
            return string.IsNullOrWhiteSpace(objective.Title) ? id : $"{objective.Title} [{id}]";
        }

        private static string BuildObjectiveState(SimTaskObjective objective)
        {
            if (objective.IsCompleted)
            {
                return "Done";
            }

            if (objective.IsFailed)
            {
                return "Failed";
            }

            return objective.IsActive ? "Active" : "Pending";
        }

        private static string BuildObjectiveProgress(SimTaskObjective objective)
        {
            if (objective.Mode == SimTaskObjective.ObjectiveMode.Boolean)
            {
                return $"Boolean={(objective.CurrentValue > 0.5f ? "true" : "false")}";
            }

            return $"Counter={objective.CurrentValue:0.##}/{objective.MaxValue:0.##} ({objective.NormalizedProgress * 100f:0}%)";
        }

        private static string BuildObjectiveCompletionRule(SimTaskObjective objective)
        {
            string configuredGoal = string.IsNullOrWhiteSpace(objective.Description)
                ? "No description"
                : objective.Description.Trim();

            string completionRule;
            completionRule = BuildObjectiveCompletionCondition(objective);

            return $"{completionRule}; goal: {configuredGoal}";
        }

        private static string BuildObjectiveCompletionCondition(SimTaskObjective objective)
        {
            if (objective.Mode == SimTaskObjective.ObjectiveMode.Boolean)
            {
                return objective.CompleteWhenTargetReached
                    ? "Boolean is set true"
                    : "CompleteObjective is called";
            }

            return objective.CompleteWhenTargetReached
                ? $"Counter reaches {objective.MaxValue:0.##}"
                : "CompleteObjective is called";
        }

        private static string BuildObjectiveSourceText(
            SimTaskObjective objective,
            SimTaskObjective currentObjective,
            IReadOnlyList<SimObjectiveInteraction> objectiveInteractions)
        {
            if (objectiveInteractions == null || objectiveInteractions.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            int matches = 0;
            for (int index = 0; index < objectiveInteractions.Count; index++)
            {
                SimObjectiveInteraction interaction = objectiveInteractions[index];
                if (interaction == null)
                {
                    continue;
                }

                bool idMatches = !string.IsNullOrWhiteSpace(interaction.ObjectiveId) &&
                    string.Equals(interaction.ObjectiveId, objective.ObjectiveId, StringComparison.Ordinal);
                bool emptyCurrentObjectiveFallback = string.IsNullOrWhiteSpace(interaction.ObjectiveId) &&
                    objective == currentObjective;

                if (!idMatches && !emptyCurrentObjectiveFallback)
                {
                    continue;
                }

                if (matches > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(interaction.name);
                if (emptyCurrentObjectiveFallback)
                {
                    builder.Append(" (current fallback)");
                }

                matches++;
                if (matches >= 4)
                {
                    builder.Append(", ...");
                    break;
                }
            }

            return builder.ToString();
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int lineCount = 1;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '\n')
                {
                    lineCount++;
                }
            }

            return lineCount;
        }
    }
}
