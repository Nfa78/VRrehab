using System;
using System.IO;
using AdaptiveSystem.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SeedsTaskTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private SeedsTaskDriver taskDriver;
        [SerializeField] private SeedPickupState seedPickupState;
        [SerializeField] private SeedPickupThrowSystem seedPickupThrowSystem;
        [SerializeField] private SeedGateSequence seedGateSequence;
        [SerializeField] private bool autoResolveReferences = true;

        [Header("JSON Export")]
        [SerializeField] private bool exportJsonOnTaskEnd = true;
        [SerializeField] private string exportFolderName = "TaskTracking";
        [SerializeField] private bool createDateSubfolder = true;
        [SerializeField] private bool logExports = true;

        [Header("Runtime")]
        [SerializeField] private bool taskRunActive;
        [SerializeField] private string activeObjectiveId = string.Empty;
        [SerializeField] private int currentAttemptIndex;
        [SerializeField] private string lastExportFilePath = string.Empty;
        [SerializeField] [TextArea(4, 12)] private string lastExportJson = string.Empty;
        [SerializeField] private TaskRunReport currentTaskReport = new TaskRunReport();
        [SerializeField] private TaskRunReport lastExportedTaskReport = new TaskRunReport();
        [SerializeField] private TaskRunReport lastCompletedTaskReport = new TaskRunReport();

        private bool subscribedToSimManager;
        private bool subscribedToPickupState;
        private bool subscribedToThrowSystem;
        private bool subscribedToGateSequence;
        private float currentAttemptStartTaskSeconds;

        public event Action<TaskRunReport> TaskRunStarted;
        public event Action<TaskRunReport> TaskRunCompleted;

        public string LastExportFilePath => lastExportFilePath;

        public string LastExportJson => lastExportJson;

        private void Awake()
        {
            EnsureReferences();
            ResetTaskRunReport(currentTaskReport);
            ResetTaskRunReport(lastExportedTaskReport);
            ResetTaskRunReport(lastCompletedTaskReport);
        }

        private void OnEnable()
        {
            EnsureReferences();
            SubscribeToSimManager();
            SubscribeToSeedSystems();
            SyncWithCurrentTaskState();
        }

        private void OnDisable()
        {
            UnsubscribeFromSimManager();
            UnsubscribeFromSeedSystems();
        }

        private void Update()
        {
            if (!autoResolveReferences || !NeedsReferenceResolution())
            {
                return;
            }

            EnsureReferences();
            SubscribeToSeedSystems();
        }

        public bool TryGetLastCompletedReport(out TaskRunReport report)
        {
            report = HasMeaningfulReport(lastCompletedTaskReport)
                ? CloneTaskRunReport(lastCompletedTaskReport)
                : null;
            return report != null;
        }

        private void HandleTaskStarted(SimTask task)
        {
            if (!IsOwnedSeedsTask(task))
            {
                return;
            }

            StartTaskRunIfNeeded(task, task.CurrentObjective);
        }

        private void HandleTaskEnded(SimTask task)
        {
            if (!IsOwnedSeedsTask(task) || !taskRunActive)
            {
                return;
            }

            EndTaskRun(task, "completed", string.Empty, false);
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            if (!IsOwnedSeedsTask(task) || !taskRunActive)
            {
                return;
            }

            EndTaskRun(task, "failed", failureReason, true);
        }

        private void HandleTaskStepChanged(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedSeedsTask(task) || objective == null)
            {
                return;
            }

            StartTaskRunIfNeeded(task, objective);
            BeginObjectiveTracking(objective);
        }

        private void HandleTaskObjectiveProgressChanged(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedSeedsTask(task) || objective == null)
            {
                return;
            }

            StartTaskRunIfNeeded(task, objective);
            BeginObjectiveTracking(objective);

            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            UpdateObjectiveProgressSnapshot(report, objective);
        }

        private void HandleTaskObjectiveCompleted(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedSeedsTask(task) || objective == null)
            {
                return;
            }

            StartTaskRunIfNeeded(task, objective);
            BeginObjectiveTracking(objective);

            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report == null)
            {
                return;
            }

            FinalizeObjectiveReport(task, objective, report, completed: true, failed: false, string.Empty, CurrentClockOrZero());
            if (string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal))
            {
                activeObjectiveId = string.Empty;
            }
        }

        private void HandleSeedsPickedUp()
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = ResolveRelevantObjectiveReportForPickup();
            if (report == null)
            {
                return;
            }

            report.pickupEvents++;
        }

        private void HandleThrowBurstSpawned(int spawnedCount, float releaseSpeed)
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = currentTaskReport.throwObjective;
            if (report == null)
            {
                return;
            }

            report.throwReleaseCount++;
            report.spawnedSeedCount += Mathf.Max(0, spawnedCount);

            float clampedReleaseSpeed = Mathf.Max(0f, releaseSpeed);
            report.totalReleaseSpeed += clampedReleaseSpeed;
            report.averageReleaseSpeed = report.throwReleaseCount > 0
                ? report.totalReleaseSpeed / report.throwReleaseCount
                : 0f;
            report.peakReleaseSpeed = Mathf.Max(report.peakReleaseSpeed, clampedReleaseSpeed);
        }

        private void HandleGateSequenceProgressed(int currentGateDepth, int activeGateCount)
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = currentTaskReport.throwObjective;
            if (report == null)
            {
                return;
            }

            report.gatePassCount++;
            report.peakGateSequenceDepth = Mathf.Max(report.peakGateSequenceDepth, Mathf.Max(0, currentGateDepth));
            report.sequenceGateCountTarget = Mathf.Max(0, activeGateCount);
        }

        private void HandleThrowSequenceSucceeded(int activeGateCount)
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = currentTaskReport.throwObjective;
            if (report == null)
            {
                return;
            }

            report.successfulThrowCount++;
            report.sequenceGateCountTarget = Mathf.Max(0, activeGateCount);

            if (report.firstSuccessfulThrowCaptured)
            {
                return;
            }

            report.firstSuccessfulThrowCaptured = true;
            float currentTaskSeconds = simManager != null && simManager.CurrentTask != null
                ? simManager.CurrentTask.GetElapsedSeconds(simManager.CurrentClock)
                : currentAttemptStartTaskSeconds;
            report.firstSuccessfulThrowAtAttemptSeconds = ToAttemptSeconds(currentTaskSeconds);
            report.firstSuccessfulThrowSecondsFromStart = Mathf.Max(
                0f,
                report.firstSuccessfulThrowAtAttemptSeconds - report.startedAtAttemptSeconds);
        }

        private void HandleThrowSequenceWrongGateRejected()
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = currentTaskReport.throwObjective;
            if (report == null)
            {
                return;
            }

            report.wrongGateRejectCount++;
        }

        private void HandleThrowSequenceReset(string reason, int previousGateDepth, int activeGateCount)
        {
            if (!taskRunActive)
            {
                return;
            }

            ObjectiveReport report = currentTaskReport.throwObjective;
            if (report == null)
            {
                return;
            }

            report.peakGateSequenceDepth = Mathf.Max(report.peakGateSequenceDepth, Mathf.Max(0, previousGateDepth));
            report.sequenceGateCountTarget = Mathf.Max(0, activeGateCount);

            switch ((reason ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "wrong_gate":
                    report.failedSequenceResetCount++;
                    break;
                case "timeout":
                    report.failedSequenceResetCount++;
                    report.timeoutResetCount++;
                    break;
                case "objective_hidden":
                    report.objectiveHiddenResetCount++;
                    break;
                case "difficulty":
                    report.difficultyResetCount++;
                    break;
            }
        }

        private void StartTaskRunIfNeeded(SimTask task, SimTaskObjective seedObjective)
        {
            if (taskRunActive || task == null)
            {
                return;
            }

            currentAttemptIndex++;
            taskRunActive = true;
            activeObjectiveId = string.Empty;
            currentAttemptStartTaskSeconds = ResolveAttemptStartTaskSeconds(task, seedObjective, CurrentClockOrZero());

            ResetTaskRunReport(currentTaskReport);

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            currentTaskReport.reportFormatVersion = 1;
            currentTaskReport.sceneName = gameObject.scene.name;
            currentTaskReport.taskDateLocal = startedAt.ToLocalTime().ToString("yyyy-MM-dd");
            currentTaskReport.taskId = task.TaskId;
            currentTaskReport.taskTitle = task.Title;
            currentTaskReport.taskDescription = task.Description;
            currentTaskReport.attemptIndex = currentAttemptIndex;
            int effectiveDifficultyLevel = ResolveEffectiveDifficultyLevel(task);
            currentTaskReport.difficultyProfileLevel = effectiveDifficultyLevel;
            currentTaskReport.difficultyLabel = ResolveDifficultyLabel(effectiveDifficultyLevel);
            currentTaskReport.attemptStartedAtUtc = startedAt.ToString("o");
            currentTaskReport.attemptStartedAtLocal = startedAt.ToLocalTime().ToString("o");

            InitializeObjectiveReports(task);
            currentTaskReport.throwObjective.sequenceGateCountTarget = ResolveGateCountTarget();

            TaskRunStarted?.Invoke(CloneTaskRunReport(currentTaskReport));
        }

        private void EndTaskRun(SimTask task, string outcome, string failureReason, bool objectiveFailed)
        {
            FinalizeActiveObjective(task, completed: false, failed: objectiveFailed, failureReason, CurrentClockOrZero());
            FinalizeTaskRun(task, outcome, failureReason, CurrentClockOrZero());
            lastCompletedTaskReport = CloneTaskRunReport(currentTaskReport) ?? new TaskRunReport();
            TaskRunCompleted?.Invoke(CloneTaskRunReport(lastCompletedTaskReport));
            ExportCurrentTaskReport();
            ClearRunState();
        }

        private void BeginObjectiveTracking(SimTaskObjective objective)
        {
            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report == null)
            {
                return;
            }

            float startedAtAttemptSeconds = ToAttemptSeconds(objective.StartedAtSeconds);
            bool sameActiveObjective = string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal);
            bool objectiveRestarted = report.started &&
                                      (!Mathf.Approximately(report.startedAtAttemptSeconds, startedAtAttemptSeconds) ||
                                       report.completed ||
                                       report.failed);

            if (sameActiveObjective && !objectiveRestarted)
            {
                report.targetValue = objective.MaxValue;
                UpdateObjectiveProgressSnapshot(report, objective);
                return;
            }

            report.ResetForRun(
                objective.ObjectiveId,
                ResolveObjectiveTitle(objective, objective.ObjectiveId),
                objective.MaxValue);
            report.started = true;
            report.startedAtAttemptSeconds = startedAtAttemptSeconds;

            if (string.Equals(objective.ObjectiveId, taskDriver != null ? taskDriver.ThrowStepId : string.Empty, StringComparison.Ordinal))
            {
                report.sequenceGateCountTarget = ResolveGateCountTarget();
            }

            UpdateObjectiveProgressSnapshot(report, objective);
            activeObjectiveId = objective.ObjectiveId;
        }

        private void FinalizeActiveObjective(
            SimTask task,
            bool completed,
            bool failed,
            string failureReason,
            float currentClock)
        {
            if (task == null || string.IsNullOrWhiteSpace(activeObjectiveId))
            {
                activeObjectiveId = string.Empty;
                return;
            }

            SimTaskObjective objective = task.GetObjective(activeObjectiveId);
            ObjectiveReport report = ResolveObjectiveReport(activeObjectiveId);
            if (objective == null || report == null)
            {
                activeObjectiveId = string.Empty;
                return;
            }

            FinalizeObjectiveReport(task, objective, report, completed, failed, failureReason, currentClock);
            activeObjectiveId = string.Empty;
        }

        private void FinalizeObjectiveReport(
            SimTask task,
            SimTaskObjective objective,
            ObjectiveReport report,
            bool completed,
            bool failed,
            string failureReason,
            float currentClock)
        {
            if (task == null || objective == null || report == null)
            {
                return;
            }

            UpdateObjectiveProgressSnapshot(report, objective);

            report.completed = completed || objective.IsCompleted;
            report.failed = failed || objective.IsFailed;
            report.failureReason = report.failed ? failureReason : string.Empty;

            float completedAtTaskSeconds = report.completed && objective.CompletedAtSeconds > 0f
                ? objective.CompletedAtSeconds
                : task.GetElapsedSeconds(currentClock);
            report.completedAtAttemptSeconds = ToAttemptSeconds(completedAtTaskSeconds);
            report.elapsedSeconds = Mathf.Max(0f, report.completedAtAttemptSeconds - report.startedAtAttemptSeconds);

            if (IsThrowObjective(report.objectiveId))
            {
                report.averageReleaseSpeed = report.throwReleaseCount > 0
                    ? report.totalReleaseSpeed / report.throwReleaseCount
                    : 0f;
            }
        }

        private void FinalizeTaskRun(SimTask task, string outcome, string failureReason, float currentClock)
        {
            if (task == null)
            {
                return;
            }

            DateTimeOffset endedAt = DateTimeOffset.UtcNow;
            currentTaskReport.outcome = outcome;
            currentTaskReport.failureReason = failureReason;
            currentTaskReport.attemptEndedAtUtc = endedAt.ToString("o");
            currentTaskReport.attemptEndedAtLocal = endedAt.ToLocalTime().ToString("o");
            currentTaskReport.attemptElapsedSeconds = ToAttemptSeconds(task.GetElapsedSeconds(currentClock));
            currentTaskReport.exportedAtUtc = endedAt.ToString("o");
            currentTaskReport.exportedAtLocal = endedAt.ToLocalTime().ToString("o");

            AggregateTaskTotals();
        }

        private void AggregateTaskTotals()
        {
            ObjectiveReport pickObjective = currentTaskReport.pickObjective;
            ObjectiveReport throwObjective = currentTaskReport.throwObjective;

            currentTaskReport.totalPickupEvents =
                pickObjective.pickupEvents +
                throwObjective.pickupEvents;
            currentTaskReport.totalThrowReleaseCount = throwObjective.throwReleaseCount;
            currentTaskReport.totalSpawnedSeedCount = throwObjective.spawnedSeedCount;
            currentTaskReport.totalSuccessfulThrowCount = throwObjective.successfulThrowCount;
            currentTaskReport.totalGatePassCount = throwObjective.gatePassCount;
            currentTaskReport.totalWrongGateRejectCount = throwObjective.wrongGateRejectCount;
            currentTaskReport.totalFailedSequenceResetCount = throwObjective.failedSequenceResetCount;
            currentTaskReport.totalTimeoutResetCount = throwObjective.timeoutResetCount;
            currentTaskReport.totalObjectiveHiddenResetCount = throwObjective.objectiveHiddenResetCount;
            currentTaskReport.totalDifficultyResetCount = throwObjective.difficultyResetCount;
            currentTaskReport.averageReleaseSpeed = throwObjective.averageReleaseSpeed;
            currentTaskReport.peakReleaseSpeed = throwObjective.peakReleaseSpeed;
            currentTaskReport.successPerReleaseRate = CalculateRate(
                currentTaskReport.totalSuccessfulThrowCount,
                currentTaskReport.totalThrowReleaseCount);
            currentTaskReport.gatePassesPerRelease = CalculateRate(
                currentTaskReport.totalGatePassCount,
                currentTaskReport.totalThrowReleaseCount);
        }

        private void ExportCurrentTaskReport()
        {
            if (!exportJsonOnTaskEnd)
            {
                return;
            }

            try
            {
                string exportDirectory = BuildExportDirectory();
                Directory.CreateDirectory(exportDirectory);

                string filePath = Path.Combine(exportDirectory, BuildExportFileName());
                string json = JsonUtility.ToJson(currentTaskReport, true);
                File.WriteAllText(filePath, json);

                lastExportFilePath = filePath;
                lastExportJson = json;
                lastExportedTaskReport = JsonUtility.FromJson<TaskRunReport>(json);

                if (logExports)
                {
                    Debug.Log($"[SeedsTaskTracker] Exported seeds task report to {filePath}", this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SeedsTaskTracker] Failed to export seeds task report: {ex.Message}", this);
            }
        }

        private string BuildExportDirectory()
        {
            string baseDirectory = Path.Combine(Application.persistentDataPath, exportFolderName, "SeedsTask");
            return createDateSubfolder
                ? Path.Combine(baseDirectory, DateTime.Now.ToString("yyyy-MM-dd"))
                : baseDirectory;
        }

        private string BuildExportFileName()
        {
            string outcome = string.IsNullOrWhiteSpace(currentTaskReport.outcome) ? "unknown" : currentTaskReport.outcome;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return $"SeedsTask_{stamp}_attempt{currentTaskReport.attemptIndex}_{outcome}.json";
        }

        private void InitializeObjectiveReports(SimTask task)
        {
            InitializeObjectiveReport(currentTaskReport.pickObjective, task, taskDriver != null ? taskDriver.PickupStepId : "pick_seeds");
            InitializeObjectiveReport(currentTaskReport.throwObjective, task, taskDriver != null ? taskDriver.ThrowStepId : "throw_seeds");
            InitializeObjectiveReport(currentTaskReport.returnObjective, task, taskDriver != null ? taskDriver.ReturnStepId : "return_bucket");
        }

        private static void InitializeObjectiveReport(ObjectiveReport report, SimTask task, string objectiveId)
        {
            if (report == null)
            {
                return;
            }

            SimTaskObjective objective = task != null ? task.GetObjective(objectiveId) : null;
            report.ResetForRun(objectiveId, ResolveObjectiveTitle(objective, objectiveId), objective != null ? objective.MaxValue : 1f);
        }

        private ObjectiveReport ResolveObjectiveReport(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId) || taskDriver == null)
            {
                return null;
            }

            if (string.Equals(objectiveId, taskDriver.PickupStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.pickObjective;
            }

            if (string.Equals(objectiveId, taskDriver.ThrowStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.throwObjective;
            }

            if (string.Equals(objectiveId, taskDriver.ReturnStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.returnObjective;
            }

            return null;
        }

        private ObjectiveReport ResolveRelevantObjectiveReportForPickup()
        {
            if (taskDriver == null)
            {
                return null;
            }

            if (string.Equals(activeObjectiveId, taskDriver.ThrowStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.throwObjective;
            }

            if (string.Equals(activeObjectiveId, taskDriver.PickupStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.pickObjective;
            }

            return currentTaskReport.throwObjective.started ? currentTaskReport.throwObjective : currentTaskReport.pickObjective;
        }

        private void UpdateObjectiveProgressSnapshot(ObjectiveReport report, SimTaskObjective objective)
        {
            if (report == null || objective == null)
            {
                return;
            }

            report.targetValue = objective.MaxValue;
            report.currentProgressValue = objective.CurrentValue;
            report.currentNormalizedProgress = objective.NormalizedProgress;
            report.peakProgressValue = Mathf.Max(report.peakProgressValue, objective.CurrentValue);
            report.peakNormalizedProgress = Mathf.Max(report.peakNormalizedProgress, objective.NormalizedProgress);

            if (IsThrowObjective(report.objectiveId))
            {
                report.sequenceGateCountTarget = Mathf.Max(report.sequenceGateCountTarget, ResolveGateCountTarget());
            }
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<SeedsTaskDriver>();
            }

            if (taskDriver == null)
            {
                taskDriver = FindSceneComponent<SeedsTaskDriver>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (seedPickupState == null)
            {
                seedPickupState = FindSceneComponent<SeedPickupState>();
            }

            if (seedPickupThrowSystem == null)
            {
                seedPickupThrowSystem = FindSceneComponent<SeedPickupThrowSystem>();
            }

            if (seedGateSequence == null)
            {
                seedGateSequence = FindSceneComponent<SeedGateSequence>();
            }
        }

        private bool NeedsReferenceResolution()
        {
            return simManager == null ||
                   taskDriver == null ||
                   seedPickupState == null ||
                   seedPickupThrowSystem == null ||
                   seedGateSequence == null;
        }

        private void SubscribeToSimManager()
        {
            if (subscribedToSimManager || simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted += HandleTaskStarted;
            simManager.LogicalTaskEnded += HandleTaskEnded;
            simManager.LogicalTaskFailed += HandleTaskFailed;
            simManager.LogicalTaskStepChanged += HandleTaskStepChanged;
            simManager.LogicalTaskObjectiveProgressChanged += HandleTaskObjectiveProgressChanged;
            simManager.LogicalTaskObjectiveCompleted += HandleTaskObjectiveCompleted;
            subscribedToSimManager = true;
        }

        private void UnsubscribeFromSimManager()
        {
            if (!subscribedToSimManager || simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted -= HandleTaskStarted;
            simManager.LogicalTaskEnded -= HandleTaskEnded;
            simManager.LogicalTaskFailed -= HandleTaskFailed;
            simManager.LogicalTaskStepChanged -= HandleTaskStepChanged;
            simManager.LogicalTaskObjectiveProgressChanged -= HandleTaskObjectiveProgressChanged;
            simManager.LogicalTaskObjectiveCompleted -= HandleTaskObjectiveCompleted;
            subscribedToSimManager = false;
        }

        private void SubscribeToSeedSystems()
        {
            if (!subscribedToPickupState && seedPickupState != null)
            {
                seedPickupState.PickedUp += HandleSeedsPickedUp;
                subscribedToPickupState = true;
            }

            if (!subscribedToThrowSystem && seedPickupThrowSystem != null)
            {
                seedPickupThrowSystem.ThrowBurstSpawned += HandleThrowBurstSpawned;
                subscribedToThrowSystem = true;
            }

            if (!subscribedToGateSequence && seedGateSequence != null)
            {
                seedGateSequence.GateSequenceProgressed += HandleGateSequenceProgressed;
                seedGateSequence.ThrowSequenceSucceeded += HandleThrowSequenceSucceeded;
                seedGateSequence.ThrowSequenceWrongGateRejected += HandleThrowSequenceWrongGateRejected;
                seedGateSequence.ThrowSequenceReset += HandleThrowSequenceReset;
                subscribedToGateSequence = true;
            }
        }

        private void UnsubscribeFromSeedSystems()
        {
            if (subscribedToPickupState && seedPickupState != null)
            {
                seedPickupState.PickedUp -= HandleSeedsPickedUp;
                subscribedToPickupState = false;
            }

            if (subscribedToThrowSystem && seedPickupThrowSystem != null)
            {
                seedPickupThrowSystem.ThrowBurstSpawned -= HandleThrowBurstSpawned;
                subscribedToThrowSystem = false;
            }

            if (subscribedToGateSequence && seedGateSequence != null)
            {
                seedGateSequence.GateSequenceProgressed -= HandleGateSequenceProgressed;
                seedGateSequence.ThrowSequenceSucceeded -= HandleThrowSequenceSucceeded;
                seedGateSequence.ThrowSequenceWrongGateRejected -= HandleThrowSequenceWrongGateRejected;
                seedGateSequence.ThrowSequenceReset -= HandleThrowSequenceReset;
                subscribedToGateSequence = false;
            }
        }

        private void SyncWithCurrentTaskState()
        {
            if (simManager == null || taskDriver == null || !IsOwnedSeedsTask(simManager.CurrentTask))
            {
                return;
            }

            StartTaskRunIfNeeded(simManager.CurrentTask, simManager.CurrentObjective);
            if (simManager.CurrentObjective != null)
            {
                BeginObjectiveTracking(simManager.CurrentObjective);
            }
        }

        private bool IsOwnedSeedsTask(SimTask task)
        {
            return task != null &&
                   taskDriver != null &&
                   string.Equals(task.TaskId, taskDriver.TaskId, StringComparison.Ordinal) &&
                   (simManager == null || simManager.CurrentTaskDriver == null || ReferenceEquals(simManager.CurrentTaskDriver, taskDriver));
        }

        private float ResolveAttemptStartTaskSeconds(SimTask task, SimTaskObjective seedObjective, float currentClock)
        {
            if (seedObjective != null)
            {
                return seedObjective.StartedAtSeconds;
            }

            if (task != null && task.CurrentObjective != null)
            {
                return task.CurrentObjective.StartedAtSeconds;
            }

            return task != null ? task.GetElapsedSeconds(currentClock) : 0f;
        }

        private float ToAttemptSeconds(float taskElapsedSeconds)
        {
            return Mathf.Max(0f, taskElapsedSeconds - currentAttemptStartTaskSeconds);
        }

        private float CurrentClockOrZero()
        {
            return simManager != null ? simManager.CurrentClock : 0f;
        }

        private int ResolveEffectiveDifficultyLevel(SimTask task)
        {
            if (task != null &&
                !string.IsNullOrWhiteSpace(task.TaskId) &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(task.TaskId, out int storedDifficultyLevel))
            {
                return taskDriver != null
                    ? taskDriver.ClampDifficultyLevel(storedDifficultyLevel)
                    : Mathf.Max(1, storedDifficultyLevel);
            }

            if (taskDriver != null)
            {
                return Mathf.Max(1, taskDriver.DifficultyLevel);
            }

            return task != null ? Mathf.Max(1, task.DifficultyProfileLevel) : 1;
        }

        private int ResolveGateCountTarget()
        {
            return seedGateSequence != null ? Mathf.Max(0, seedGateSequence.ActiveGateCount) : 0;
        }

        private bool IsThrowObjective(string objectiveId)
        {
            return taskDriver != null &&
                   string.Equals(objectiveId, taskDriver.ThrowStepId, StringComparison.Ordinal);
        }

        private void ClearRunState()
        {
            taskRunActive = false;
            activeObjectiveId = string.Empty;
            currentAttemptStartTaskSeconds = 0f;
        }

        private T FindSceneComponent<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Scene targetScene = gameObject.scene;
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == targetScene)
                {
                    return component;
                }
            }

            return components.Length > 0 ? components[0] : null;
        }

        private static string ResolveObjectiveTitle(SimTaskObjective objective, string fallbackObjectiveId)
        {
            if (objective != null && !string.IsNullOrWhiteSpace(objective.Title))
            {
                return objective.Title;
            }

            return fallbackObjectiveId;
        }

        private static string ResolveDifficultyLabel(int difficultyLevel)
        {
            return $"Level{Mathf.Max(0, difficultyLevel - 1)}";
        }

        private static float CalculateRate(int numerator, int denominator)
        {
            return denominator > 0 ? Mathf.Max(0, numerator) / (float)denominator : 0f;
        }

        private static bool HasMeaningfulReport(TaskRunReport report)
        {
            return report != null &&
                   !string.IsNullOrWhiteSpace(report.taskId) &&
                   report.attemptIndex > 0;
        }

        private static TaskRunReport CloneTaskRunReport(TaskRunReport source)
        {
            if (source == null)
            {
                return null;
            }

            string json = JsonUtility.ToJson(source);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<TaskRunReport>(json);
        }

        private static void ResetTaskRunReport(TaskRunReport report)
        {
            if (report == null)
            {
                return;
            }

            report.reportFormatVersion = 1;
            report.sceneName = string.Empty;
            report.taskDateLocal = string.Empty;
            report.exportedAtUtc = string.Empty;
            report.exportedAtLocal = string.Empty;
            report.taskId = string.Empty;
            report.taskTitle = string.Empty;
            report.taskDescription = string.Empty;
            report.attemptStartedAtUtc = string.Empty;
            report.attemptStartedAtLocal = string.Empty;
            report.attemptEndedAtUtc = string.Empty;
            report.attemptEndedAtLocal = string.Empty;
            report.outcome = string.Empty;
            report.failureReason = string.Empty;
            report.attemptIndex = 0;
            report.difficultyProfileLevel = 0;
            report.difficultyLabel = string.Empty;
            report.attemptElapsedSeconds = 0f;
            report.totalPickupEvents = 0;
            report.totalThrowReleaseCount = 0;
            report.totalSpawnedSeedCount = 0;
            report.totalSuccessfulThrowCount = 0;
            report.totalGatePassCount = 0;
            report.totalWrongGateRejectCount = 0;
            report.totalFailedSequenceResetCount = 0;
            report.totalTimeoutResetCount = 0;
            report.totalObjectiveHiddenResetCount = 0;
            report.totalDifficultyResetCount = 0;
            report.averageReleaseSpeed = 0f;
            report.peakReleaseSpeed = 0f;
            report.successPerReleaseRate = 0f;
            report.gatePassesPerRelease = 0f;
            report.pickObjective.ResetForRun(string.Empty, string.Empty, 1f);
            report.throwObjective.ResetForRun(string.Empty, string.Empty, 1f);
            report.returnObjective.ResetForRun(string.Empty, string.Empty, 1f);
        }

        [Serializable]
        public sealed class TaskRunReport
        {
            public int reportFormatVersion = 1;
            public string sceneName = string.Empty;
            public string taskDateLocal = string.Empty;
            public string exportedAtUtc = string.Empty;
            public string exportedAtLocal = string.Empty;
            public string taskId = string.Empty;
            public string taskTitle = string.Empty;
            public string taskDescription = string.Empty;
            public string attemptStartedAtUtc = string.Empty;
            public string attemptStartedAtLocal = string.Empty;
            public string attemptEndedAtUtc = string.Empty;
            public string attemptEndedAtLocal = string.Empty;
            public string outcome = string.Empty;
            public string failureReason = string.Empty;
            public int attemptIndex;
            public int difficultyProfileLevel;
            public string difficultyLabel = string.Empty;
            public float attemptElapsedSeconds;
            public int totalPickupEvents;
            public int totalThrowReleaseCount;
            public int totalSpawnedSeedCount;
            public int totalSuccessfulThrowCount;
            public int totalGatePassCount;
            public int totalWrongGateRejectCount;
            public int totalFailedSequenceResetCount;
            public int totalTimeoutResetCount;
            public int totalObjectiveHiddenResetCount;
            public int totalDifficultyResetCount;
            public float averageReleaseSpeed;
            public float peakReleaseSpeed;
            public float successPerReleaseRate;
            public float gatePassesPerRelease;
            public ObjectiveReport pickObjective = new ObjectiveReport();
            public ObjectiveReport throwObjective = new ObjectiveReport();
            public ObjectiveReport returnObjective = new ObjectiveReport();
        }

        [Serializable]
        public sealed class ObjectiveReport
        {
            public string objectiveId = string.Empty;
            public string objectiveTitle = string.Empty;
            public bool started;
            public bool completed;
            public bool failed;
            public string failureReason = string.Empty;
            public float targetValue = 1f;
            public float startedAtAttemptSeconds;
            public float completedAtAttemptSeconds;
            public float elapsedSeconds;
            public float currentProgressValue;
            public float peakProgressValue;
            public float currentNormalizedProgress;
            public float peakNormalizedProgress;
            public int pickupEvents;
            public int throwReleaseCount;
            public int spawnedSeedCount;
            public int successfulThrowCount;
            public int gatePassCount;
            public int wrongGateRejectCount;
            public int failedSequenceResetCount;
            public int timeoutResetCount;
            public int objectiveHiddenResetCount;
            public int difficultyResetCount;
            public int peakGateSequenceDepth;
            public int sequenceGateCountTarget;
            public bool firstSuccessfulThrowCaptured;
            public float firstSuccessfulThrowAtAttemptSeconds = -1f;
            public float firstSuccessfulThrowSecondsFromStart = -1f;
            public float totalReleaseSpeed;
            public float averageReleaseSpeed;
            public float peakReleaseSpeed;

            public void ResetForRun(string newObjectiveId, string newObjectiveTitle, float newTargetValue)
            {
                objectiveId = newObjectiveId;
                objectiveTitle = newObjectiveTitle;
                started = false;
                completed = false;
                failed = false;
                failureReason = string.Empty;
                targetValue = Mathf.Max(1f, newTargetValue);
                startedAtAttemptSeconds = 0f;
                completedAtAttemptSeconds = 0f;
                elapsedSeconds = 0f;
                currentProgressValue = 0f;
                peakProgressValue = 0f;
                currentNormalizedProgress = 0f;
                peakNormalizedProgress = 0f;
                pickupEvents = 0;
                throwReleaseCount = 0;
                spawnedSeedCount = 0;
                successfulThrowCount = 0;
                gatePassCount = 0;
                wrongGateRejectCount = 0;
                failedSequenceResetCount = 0;
                timeoutResetCount = 0;
                objectiveHiddenResetCount = 0;
                difficultyResetCount = 0;
                peakGateSequenceDepth = 0;
                sequenceGateCountTarget = 0;
                firstSuccessfulThrowCaptured = false;
                firstSuccessfulThrowAtAttemptSeconds = -1f;
                firstSuccessfulThrowSecondsFromStart = -1f;
                totalReleaseSpeed = 0f;
                averageReleaseSpeed = 0f;
                peakReleaseSpeed = 0f;
            }
        }
    }
}
