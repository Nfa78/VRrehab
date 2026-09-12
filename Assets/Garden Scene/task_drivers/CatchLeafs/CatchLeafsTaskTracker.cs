using System;
using System.IO;
using AdaptiveSystem.Api;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class CatchLeafsTaskTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private CatchLeafsTaskDriver taskDriver;
        [SerializeField] private BucketLeafCatchSystem catchSystem;
        [SerializeField] private Transform bucketTransform;
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
        private BucketLeafCatchSystem subscribedCatchSystem;
        private float currentAttemptStartTaskSeconds;
        private float lastSampleClock;
        private Vector3 lastBucketPosition;
        private bool hasLastBucketPosition;

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
            RefreshCatchSystemSubscription();
            SyncWithCurrentTaskState();
        }

        private void OnDisable()
        {
            UnsubscribeFromSimManager();
            UnsubscribeFromCatchSystem();
        }

        private void Update()
        {
            if (autoResolveReferences && NeedsReferenceResolution())
            {
                EnsureReferences();
                SubscribeToSimManager();
                RefreshCatchSystemSubscription();
            }

            if (!taskRunActive || simManager == null || !IsOwnedTask(simManager.CurrentTask))
            {
                return;
            }

            if (!simManager.IsRunning)
            {
                ResetFrameSampleState();
                return;
            }

            UpdateContinuousMetrics(simManager.CurrentClock);
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
            if (IsOwnedTask(task))
            {
                StartTaskRunIfNeeded(task, task.CurrentObjective);
            }
        }

        private void HandleTaskEnded(SimTask task)
        {
            if (IsOwnedTask(task) && taskRunActive)
            {
                EndTaskRun(task, "completed", string.Empty, false);
            }
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            if (IsOwnedTask(task) && taskRunActive)
            {
                EndTaskRun(task, "failed", failureReason, true);
            }
        }

        private void HandleTaskStepChanged(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedTask(task) || objective == null)
            {
                return;
            }

            StartTaskRunIfNeeded(task, objective);
            BeginObjectiveTracking(objective);
        }

        private void HandleTaskObjectiveProgressChanged(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedTask(task) || objective == null)
            {
                return;
            }

            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report != null)
            {
                UpdateObjectiveProgress(report, objective);
            }
        }

        private void HandleTaskObjectiveCompleted(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedTask(task) || objective == null)
            {
                return;
            }

            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report == null)
            {
                return;
            }

            FlushContinuousMetrics();
            FinalizeObjectiveReport(task, objective, report, true, false, string.Empty, CurrentClockOrZero());
            if (string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal))
            {
                activeObjectiveId = string.Empty;
            }
        }

        private void HandleBucketGrabStateChanged(bool isGrabbed)
        {
            if (!ShouldAcceptMechanicEvent())
            {
                return;
            }

            if (isGrabbed)
            {
                currentTaskReport.bucketPickupCount++;
            }
            else
            {
                currentTaskReport.bucketReleaseCount++;
            }
        }

        private void HandleLeafCatchProcessed(LeafsFallingEffect leaf, bool accepted)
        {
            if (!ShouldAcceptMechanicEvent())
            {
                return;
            }

            currentTaskReport.totalCatchChecks++;
            if (!accepted)
            {
                currentTaskReport.rejectedCatchCount++;
                return;
            }

            currentTaskReport.caughtLeafCount++;
            if (currentTaskReport.firstCatchCaptured)
            {
                return;
            }

            currentTaskReport.firstCatchCaptured = true;
            float taskSeconds = simManager.CurrentTask.GetElapsedSeconds(simManager.CurrentClock);
            currentTaskReport.timeToFirstCatchSeconds = ToAttemptSeconds(taskSeconds);
        }

        private void HandleLeafMissed(LeafsFallingEffect leaf)
        {
            if (ShouldAcceptMechanicEvent())
            {
                currentTaskReport.missedLeafCount++;
            }
        }

        private void HandleLeafRespawned(LeafsFallingEffect leaf)
        {
            if (ShouldAcceptMechanicEvent())
            {
                currentTaskReport.leafRespawnCount++;
            }
        }

        private bool ShouldAcceptMechanicEvent()
        {
            return taskRunActive &&
                   simManager != null &&
                   simManager.IsRunning &&
                   IsOwnedTask(simManager.CurrentTask);
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
            int difficultyLevel = ResolveEffectiveDifficultyLevel(task);
            currentTaskReport.difficultyProfileLevel = difficultyLevel;
            currentTaskReport.difficultyLabel = $"Level{Mathf.Max(0, difficultyLevel - 1)}";
            currentTaskReport.attemptStartedAtUtc = startedAt.ToString("o");
            currentTaskReport.attemptStartedAtLocal = startedAt.ToLocalTime().ToString("o");
            currentTaskReport.configuredLeafCount = catchSystem != null ? catchSystem.ConfiguredLeafCount : 0;

            InitializeObjectiveReports(task);
            lastSampleClock = CurrentClockOrZero();
            hasLastBucketPosition = bucketTransform != null;
            lastBucketPosition = bucketTransform != null ? bucketTransform.position : Vector3.zero;

            TaskRunStarted?.Invoke(CloneTaskRunReport(currentTaskReport));
        }

        private void EndTaskRun(SimTask task, string outcome, string failureReason, bool objectiveFailed)
        {
            float currentClock = CurrentClockOrZero();
            UpdateContinuousMetrics(currentClock);
            FinalizeActiveObjective(task, false, objectiveFailed, failureReason, currentClock);

            DateTimeOffset endedAt = DateTimeOffset.UtcNow;
            currentTaskReport.outcome = outcome;
            currentTaskReport.failureReason = failureReason ?? string.Empty;
            currentTaskReport.attemptEndedAtUtc = endedAt.ToString("o");
            currentTaskReport.attemptEndedAtLocal = endedAt.ToLocalTime().ToString("o");
            currentTaskReport.attemptElapsedSeconds = ToAttemptSeconds(task.GetElapsedSeconds(currentClock));
            currentTaskReport.exportedAtUtc = endedAt.ToString("o");
            currentTaskReport.exportedAtLocal = endedAt.ToLocalTime().ToString("o");
            currentTaskReport.completedObjectiveCount = CountCompletedObjectives();
            currentTaskReport.failedObjectiveCount = CountFailedObjectives();
            currentTaskReport.catchAccuracy = CalculateRate(
                currentTaskReport.caughtLeafCount,
                currentTaskReport.caughtLeafCount + currentTaskReport.missedLeafCount);
            currentTaskReport.catchesPerSecond = currentTaskReport.catchObjective.elapsedSeconds > 0f
                ? currentTaskReport.caughtLeafCount / currentTaskReport.catchObjective.elapsedSeconds
                : 0f;
            currentTaskReport.returnDistanceFromStartMeters = ResolveReturnDistanceFromStart(task);

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
            bool sameObjective = string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal);
            bool restarted = report.started &&
                             (!Mathf.Approximately(report.startedAtAttemptSeconds, startedAtAttemptSeconds) ||
                              report.completed ||
                              report.failed);
            if (sameObjective && !restarted)
            {
                UpdateObjectiveProgress(report, objective);
                return;
            }

            report.ResetForRun(objective.ObjectiveId, ResolveObjectiveTitle(objective), objective.MaxValue);
            report.started = true;
            report.startedAtAttemptSeconds = startedAtAttemptSeconds;
            UpdateObjectiveProgress(report, objective);
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
            if (objective != null && report != null)
            {
                FinalizeObjectiveReport(task, objective, report, completed, failed, failureReason, currentClock);
            }

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
            UpdateObjectiveProgress(report, objective);
            report.completed = completed || objective.IsCompleted;
            report.failed = failed || objective.IsFailed;
            report.failureReason = report.failed ? failureReason ?? string.Empty : string.Empty;

            float completedAtTaskSeconds = report.completed && objective.CompletedAtSeconds > 0f
                ? objective.CompletedAtSeconds
                : task.GetElapsedSeconds(currentClock);
            report.completedAtAttemptSeconds = ToAttemptSeconds(completedAtTaskSeconds);
            report.elapsedSeconds = Mathf.Max(0f, report.completedAtAttemptSeconds - report.startedAtAttemptSeconds);
        }

        private void UpdateContinuousMetrics(float currentClock)
        {
            float delta = Mathf.Max(0f, currentClock - lastSampleClock);
            lastSampleClock = currentClock;

            if (bucketTransform != null)
            {
                Vector3 currentPosition = bucketTransform.position;
                if (hasLastBucketPosition)
                {
                    float displacement = Vector3.Distance(lastBucketPosition, currentPosition);
                    currentTaskReport.totalBucketDisplacementMeters += displacement;
                    if (taskDriver != null && taskDriver.IsCatchStepActive())
                    {
                        currentTaskReport.catchBucketDisplacementMeters += displacement;
                    }
                }

                hasLastBucketPosition = true;
                lastBucketPosition = currentPosition;
            }

            if (catchSystem != null && catchSystem.IsBucketGrabbed)
            {
                currentTaskReport.bucketHeldSeconds += delta;
            }
        }

        private void FlushContinuousMetrics()
        {
            if (taskRunActive && simManager != null && simManager.IsRunning)
            {
                UpdateContinuousMetrics(simManager.CurrentClock);
            }
        }

        private void ResetFrameSampleState()
        {
            lastSampleClock = CurrentClockOrZero();
            hasLastBucketPosition = bucketTransform != null;
            lastBucketPosition = bucketTransform != null ? bucketTransform.position : Vector3.zero;
        }

        private void InitializeObjectiveReports(SimTask task)
        {
            InitializeObjectiveReport(currentTaskReport.pickupObjective, task, taskDriver != null ? taskDriver.PickupStepId : "pick_bucket");
            InitializeObjectiveReport(currentTaskReport.catchObjective, task, taskDriver != null ? taskDriver.CatchStepId : "catch_leafs");
            InitializeObjectiveReport(currentTaskReport.returnObjective, task, taskDriver != null ? taskDriver.ReturnStepId : "return_bucket");

            if (task != null && task.CurrentObjective != null)
            {
                BeginObjectiveTracking(task.CurrentObjective);
            }
        }

        private static void InitializeObjectiveReport(ObjectiveReport report, SimTask task, string objectiveId)
        {
            SimTaskObjective objective = task != null ? task.GetObjective(objectiveId) : null;
            report.ResetForRun(
                objectiveId,
                objective != null ? ResolveObjectiveTitle(objective) : objectiveId,
                objective != null ? objective.MaxValue : 1f);
        }

        private ObjectiveReport ResolveObjectiveReport(string objectiveId)
        {
            if (taskDriver == null || string.IsNullOrWhiteSpace(objectiveId))
            {
                return null;
            }

            if (string.Equals(objectiveId, taskDriver.PickupStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.pickupObjective;
            }

            if (string.Equals(objectiveId, taskDriver.CatchStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.catchObjective;
            }

            if (string.Equals(objectiveId, taskDriver.ReturnStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.returnObjective;
            }

            return null;
        }

        private int CountCompletedObjectives()
        {
            int count = 0;
            count += currentTaskReport.pickupObjective.completed ? 1 : 0;
            count += currentTaskReport.catchObjective.completed ? 1 : 0;
            count += currentTaskReport.returnObjective.completed ? 1 : 0;
            return count;
        }

        private int CountFailedObjectives()
        {
            int count = 0;
            count += currentTaskReport.pickupObjective.failed ? 1 : 0;
            count += currentTaskReport.catchObjective.failed ? 1 : 0;
            count += currentTaskReport.returnObjective.failed ? 1 : 0;
            return count;
        }

        private float ResolveReturnDistanceFromStart(SimTask task)
        {
            TrackedTaskObject trackedBucket = task != null ? task.GetTrackedObject("bucket") : null;
            if (trackedBucket != null && trackedBucket.HasCapturedStartPose)
            {
                return trackedBucket.DistanceFromStart();
            }

            return 0f;
        }

        private void ExportCurrentTaskReport()
        {
            if (!exportJsonOnTaskEnd)
            {
                return;
            }

            try
            {
                string directory = Path.Combine(
                    Application.persistentDataPath,
                    exportFolderName,
                    "CatchLeafsTask");
                if (createDateSubfolder)
                {
                    directory = Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd"));
                }

                Directory.CreateDirectory(directory);
                string outcome = string.IsNullOrWhiteSpace(currentTaskReport.outcome)
                    ? "unknown"
                    : currentTaskReport.outcome;
                string fileName = $"CatchLeafsTask_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{outcome}.json";
                string path = Path.Combine(directory, fileName);
                string json = JsonUtility.ToJson(currentTaskReport, true);
                File.WriteAllText(path, json);

                lastExportFilePath = path;
                lastExportJson = json;
                lastExportedTaskReport = JsonUtility.FromJson<TaskRunReport>(json) ?? new TaskRunReport();

                if (logExports)
                {
                    Debug.Log($"[CatchLeafsTaskTracker] Exported task report to {path}", this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CatchLeafsTaskTracker] Failed to export task report: {ex.Message}", this);
            }
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<CatchLeafsTaskDriver>() ?? FindSceneComponent<CatchLeafsTaskDriver>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (catchSystem == null)
            {
                catchSystem = FindSceneComponent<BucketLeafCatchSystem>();
            }

            if (bucketTransform == null && catchSystem != null)
            {
                bucketTransform = catchSystem.BucketTransform;
            }
        }

        private bool NeedsReferenceResolution()
        {
            return taskDriver == null || simManager == null || catchSystem == null || bucketTransform == null;
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

        private void RefreshCatchSystemSubscription()
        {
            if (ReferenceEquals(subscribedCatchSystem, catchSystem))
            {
                return;
            }

            UnsubscribeFromCatchSystem();
            if (catchSystem == null)
            {
                return;
            }

            catchSystem.BucketGrabStateChanged += HandleBucketGrabStateChanged;
            catchSystem.LeafCatchProcessed += HandleLeafCatchProcessed;
            catchSystem.LeafMissed += HandleLeafMissed;
            catchSystem.LeafRespawned += HandleLeafRespawned;
            subscribedCatchSystem = catchSystem;
        }

        private void UnsubscribeFromCatchSystem()
        {
            if (subscribedCatchSystem == null)
            {
                return;
            }

            subscribedCatchSystem.BucketGrabStateChanged -= HandleBucketGrabStateChanged;
            subscribedCatchSystem.LeafCatchProcessed -= HandleLeafCatchProcessed;
            subscribedCatchSystem.LeafMissed -= HandleLeafMissed;
            subscribedCatchSystem.LeafRespawned -= HandleLeafRespawned;
            subscribedCatchSystem = null;
        }

        private void SyncWithCurrentTaskState()
        {
            if (simManager == null || !IsOwnedTask(simManager.CurrentTask))
            {
                return;
            }

            StartTaskRunIfNeeded(simManager.CurrentTask, simManager.CurrentObjective);
            if (simManager.CurrentObjective != null)
            {
                BeginObjectiveTracking(simManager.CurrentObjective);
            }
        }

        private bool IsOwnedTask(SimTask task)
        {
            return task != null &&
                   taskDriver != null &&
                   string.Equals(task.TaskId, taskDriver.TaskId, StringComparison.Ordinal) &&
                   (simManager == null || simManager.CurrentTaskDriver == null || ReferenceEquals(simManager.CurrentTaskDriver, taskDriver));
        }

        private int ResolveEffectiveDifficultyLevel(SimTask task)
        {
            if (task != null &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(task.TaskId, out int storedDifficultyLevel))
            {
                return taskDriver != null
                    ? taskDriver.ClampDifficultyLevel(storedDifficultyLevel)
                    : Mathf.Max(1, storedDifficultyLevel);
            }

            return taskDriver != null
                ? taskDriver.ClampDifficultyLevel(taskDriver.DifficultyLevel)
                : task != null ? Mathf.Max(1, task.DifficultyProfileLevel) : 1;
        }

        private float ResolveAttemptStartTaskSeconds(SimTask task, SimTaskObjective objective, float currentClock)
        {
            if (objective != null)
            {
                return objective.StartedAtSeconds;
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

        private void ClearRunState()
        {
            taskRunActive = false;
            activeObjectiveId = string.Empty;
            currentAttemptStartTaskSeconds = 0f;
            lastSampleClock = 0f;
            hasLastBucketPosition = false;
        }

        private T FindSceneComponent<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.scene == gameObject.scene)
                {
                    return components[i];
                }
            }

            return components.Length > 0 ? components[0] : null;
        }

        private static void UpdateObjectiveProgress(ObjectiveReport report, SimTaskObjective objective)
        {
            report.currentValue = objective.CurrentValue;
            report.targetValue = objective.MaxValue;
            report.normalizedProgress = objective.NormalizedProgress;
        }

        private static string ResolveObjectiveTitle(SimTaskObjective objective)
        {
            return objective != null && !string.IsNullOrWhiteSpace(objective.Title)
                ? objective.Title
                : objective != null ? objective.ObjectiveId : string.Empty;
        }

        private static float CalculateRate(float numerator, float denominator)
        {
            return denominator > 0f ? Mathf.Clamp01(numerator / denominator) : 0f;
        }

        private static bool HasMeaningfulReport(TaskRunReport report)
        {
            return report != null && !string.IsNullOrWhiteSpace(report.taskId);
        }

        private static TaskRunReport CloneTaskRunReport(TaskRunReport report)
        {
            return report == null
                ? null
                : JsonUtility.FromJson<TaskRunReport>(JsonUtility.ToJson(report));
        }

        private static void ResetTaskRunReport(TaskRunReport report)
        {
            if (report == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(new TaskRunReport());
            JsonUtility.FromJsonOverwrite(json, report);
        }

        [Serializable]
        public sealed class TaskRunReport
        {
            public int reportFormatVersion;
            public string sceneName = string.Empty;
            public string taskDateLocal = string.Empty;
            public string taskId = string.Empty;
            public string taskTitle = string.Empty;
            public string taskDescription = string.Empty;
            public int attemptIndex;
            public int difficultyProfileLevel;
            public string difficultyLabel = string.Empty;
            public string outcome = string.Empty;
            public string failureReason = string.Empty;
            public string attemptStartedAtUtc = string.Empty;
            public string attemptStartedAtLocal = string.Empty;
            public string attemptEndedAtUtc = string.Empty;
            public string attemptEndedAtLocal = string.Empty;
            public string exportedAtUtc = string.Empty;
            public string exportedAtLocal = string.Empty;
            public float attemptElapsedSeconds;
            public int configuredLeafCount;
            public int completedObjectiveCount;
            public int failedObjectiveCount;
            public int bucketPickupCount;
            public int bucketReleaseCount;
            public float bucketHeldSeconds;
            public float totalBucketDisplacementMeters;
            public float catchBucketDisplacementMeters;
            public int totalCatchChecks;
            public int caughtLeafCount;
            public int rejectedCatchCount;
            public int missedLeafCount;
            public int leafRespawnCount;
            public float catchAccuracy;
            public float catchesPerSecond;
            public bool firstCatchCaptured;
            public float timeToFirstCatchSeconds;
            public float returnDistanceFromStartMeters;
            public ObjectiveReport pickupObjective = new ObjectiveReport();
            public ObjectiveReport catchObjective = new ObjectiveReport();
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
            public float startedAtAttemptSeconds;
            public float completedAtAttemptSeconds;
            public float elapsedSeconds;
            public float currentValue;
            public float targetValue = 1f;
            public float normalizedProgress;

            public void ResetForRun(string id, string title, float target)
            {
                objectiveId = id ?? string.Empty;
                objectiveTitle = title ?? string.Empty;
                started = false;
                completed = false;
                failed = false;
                failureReason = string.Empty;
                startedAtAttemptSeconds = 0f;
                completedAtAttemptSeconds = 0f;
                elapsedSeconds = 0f;
                currentValue = 0f;
                targetValue = Mathf.Max(1f, target);
                normalizedProgress = 0f;
            }
        }
    }
}
