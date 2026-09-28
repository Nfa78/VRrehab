using System;
using System.Collections;
using System.Collections.Generic;
using AdaptiveSystem.Api;
using AdaptiveSystem.Models;
using AdaptiveSystem.Processing;
using AdaptiveSystem.Raw;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class CatchLeafsTaskAdaptiveReporter : MonoBehaviour
    {
        private const string RecorderObjectName = "CatchLeafsTrajectoryRecorder";
        private const string ApiClientObjectName = "CatchLeafsAdaptiveApiClient";

        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private CatchLeafsTaskDriver taskDriver;
        [SerializeField] private CatchLeafsTaskTracker taskTracker;
        [SerializeField] private BucketLeafCatchSystem catchSystem;
        [SerializeField] private AdaptiveApiClient adaptiveApi;
        [SerializeField] private TrajectoryRecorder trajectoryRecorder;
        [SerializeField] private Transform trajectoryTarget;
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool autoCreateAdaptiveApiClient = true;
        [SerializeField] private bool autoCreateTrajectoryRecorder = true;

        [Header("Adaptive Flow")]
        [SerializeField] private bool adaptiveReportingEnabled = true;
        [SerializeField] private bool autoCreatePatientProfile = true;
        [SerializeField] private bool autoStartSession = true;
        [SerializeField] private bool applyBackendTimeout = true;
        [SerializeField] private bool autoApplyAdaptiveDecisionToNextRun = true;
        [SerializeField] [Min(0.1f)] private float taskExecutionStartWaitSeconds = 10f;
        [SerializeField] [Min(0.5f)] private float progressSnapshotIntervalSeconds = 3f;
        [SerializeField] private bool logAdaptiveFlow = true;

        [Header("Patient Defaults")]
        [SerializeField] private string fallbackPatientCode = "P001";
        [SerializeField] private string derivedPatientCodePrefix = "PAT";
        [SerializeField] private string fallbackDominantHand = "right";
        [SerializeField] [TextArea] private string patientNotes = "Catch leaves task runtime session";

        [Header("Session Defaults")]
        [SerializeField] private string device = "unity-editor";
        [SerializeField] private string versionOverride = string.Empty;

        [Header("Runtime")]
        [SerializeField] private int effectiveDifficultyLevel = 1;
        [SerializeField] private string activeSessionId = string.Empty;
        [SerializeField] private string activeTaskExecutionId = string.Empty;
        [SerializeField] private string lastTaskExecutionId = string.Empty;
        [SerializeField] private int lastBackendTimeoutSeconds;
        [SerializeField] private int lastExpectedTimeSeconds;
        [SerializeField] private string lastDecision = string.Empty;
        [SerializeField] private float lastDecisionConfidence;
        [SerializeField] private bool lastMetricsSubmitSucceeded;
        [SerializeField] private string lastMetricsError = string.Empty;
        [SerializeField] [TextArea(4, 16)] private string lastMetricsPayloadJson = string.Empty;
        [SerializeField] [TextArea(4, 12)] private string lastMetricsResponseJson = string.Empty;

        private bool subscribedToTracker;
        private AdaptiveTaskAttemptContext currentAttempt;
        private Coroutine progressSnapshotCoroutine;

        private void Awake()
        {
            EnsureReferences();
            RestoreRuntimeState();
        }

        private void OnEnable()
        {
            EnsureReferences();
            RestoreRuntimeState();
            SubscribeToTracker();
        }

        private void OnDisable()
        {
            StopProgressSnapshotReporting();
            UnsubscribeFromTracker();
            StopTrajectoryRecording();
        }

        private void Update()
        {
            if (!autoResolveReferences || !NeedsReferenceResolution())
            {
                return;
            }

            EnsureReferences();
            RestoreRuntimeState();
            SubscribeToTracker();
        }

        private void HandleTaskRunStarted(CatchLeafsTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

            if (currentAttempt != null)
            {
                LogWarning($"Ignoring duplicate task-run start for '{report.taskId}' while attempt '{currentAttempt.taskId}' is still active.");
                return;
            }

            EnsureReferences();
            RestoreRuntimeState();
            activeTaskExecutionId = string.Empty;

            int difficultyLevel = ResolveDifficultyLevelForAttempt(report);
            ApplyDifficultyLevelToDriver(difficultyLevel);
            BeginTrajectoryRecording();

            currentAttempt = new AdaptiveTaskAttemptContext
            {
                taskId = report.taskId,
                attemptIndex = report.attemptIndex,
                difficultyLevel = difficultyLevel,
                startedAtUtc = !string.IsNullOrWhiteSpace(report.attemptStartedAtUtc)
                    ? report.attemptStartedAtUtc
                    : DateTime.UtcNow.ToString("o"),
                startPending = true
            };

            StartCoroutine(BeginAdaptiveAttempt(currentAttempt));
        }

        private void HandleTaskRunCompleted(CatchLeafsTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

            StopTrajectoryRecording();
            AdaptiveTaskAttemptContext completedAttempt = currentAttempt;
            if (completedAttempt != null)
            {
                completedAttempt.finalizationStarted = true;
            }
            currentAttempt = null;
            StartCoroutine(SubmitMetricsForAttempt(report, completedAttempt));
        }

        private IEnumerator BeginAdaptiveAttempt(AdaptiveTaskAttemptContext attempt)
        {
            yield return AdaptiveTaskReporterFlow.BeginAdaptiveAttempt(CreateFlowBindings(), attempt);
            if (attempt == currentAttempt && attempt.startSucceeded && !attempt.finalizationStarted)
            {
                progressSnapshotCoroutine = StartCoroutine(SubmitProgressSnapshots(attempt));
            }
        }

        private IEnumerator SubmitProgressSnapshots(AdaptiveTaskAttemptContext attempt)
        {
            yield return AdaptiveTaskReporterFlow.SubmitProgressSnapshots(
                CreateFlowBindings(buildProgressRequest: BuildProgressRequest),
                attempt);
            progressSnapshotCoroutine = null;
        }

        private IEnumerator SubmitMetricsForAttempt(
            CatchLeafsTaskTracker.TaskRunReport report,
            AdaptiveTaskAttemptContext attempt)
        {
            if (report == null)
            {
                yield break;
            }

            yield return AdaptiveTaskReporterFlow.SubmitMetricsForAttempt(
                CreateFlowBindings(
                    () => BuildMetricsRequest(report),
                    report.taskId,
                    attempt != null ? attempt.difficultyLevel : ResolveDifficultyLevelForAttempt(report),
                    ResolveTaskEndTime(report),
                    report.outcome,
                    report.failureReason),
                attempt);
        }

        private AdaptiveTaskReporterFlow.Bindings CreateFlowBindings(
            Func<TaskMetricsRequest> buildMetricsRequest = null,
            string taskId = "",
            int currentDifficultyLevel = 1,
            string taskEndTimeUtc = "",
            string taskOutcome = "completed",
            string taskFailureReason = "",
            Func<TaskProgressRequest> buildProgressRequest = null)
        {
            return new AdaptiveTaskReporterFlow.Bindings
            {
                AdaptiveApi = adaptiveApi,
                AutoCreatePatientProfile = autoCreatePatientProfile,
                AutoStartSession = autoStartSession,
                ApplyBackendTimeout = applyBackendTimeout,
                AutoApplyAdaptiveDecisionToNextRun = autoApplyAdaptiveDecisionToNextRun,
                TaskExecutionStartWaitSeconds = taskExecutionStartWaitSeconds,
                ProgressSnapshotIntervalSeconds = progressSnapshotIntervalSeconds,
                TaskId = taskId,
                CurrentDifficultyLevel = currentDifficultyLevel,
                TaskEndTimeUtc = taskEndTimeUtc,
                TaskOutcome = taskOutcome,
                TaskFailureReason = taskFailureReason,
                BuildMetricsRequest = buildMetricsRequest,
                BuildProgressRequest = buildProgressRequest,
                GetActiveSessionId = () => activeSessionId,
                SetActiveSessionId = value => activeSessionId = value ?? string.Empty,
                GetActiveTaskExecutionId = () => activeTaskExecutionId,
                SetActiveTaskExecutionId = value => activeTaskExecutionId = value ?? string.Empty,
                SetLastTaskExecutionId = value => lastTaskExecutionId = value ?? string.Empty,
                SetLastBackendTimeoutSeconds = value => lastBackendTimeoutSeconds = value,
                SetLastExpectedTimeSeconds = value => lastExpectedTimeSeconds = value,
                SetLastDecision = value => lastDecision = value ?? string.Empty,
                SetLastDecisionConfidence = value => lastDecisionConfidence = value,
                SetLastMetricsSubmitSucceeded = value => lastMetricsSubmitSucceeded = value,
                SetLastMetricsError = value => lastMetricsError = value ?? string.Empty,
                SetLastMetricsPayloadJson = value => lastMetricsPayloadJson = value ?? string.Empty,
                SetLastMetricsResponseJson = value => lastMetricsResponseJson = value ?? string.Empty,
                ResolvePatientCode = ResolvePatientCode,
                ResolveDominantHand = ResolveDominantHand,
                ResolvePatientNotes = ResolvePatientNotes,
                ResolveDevice = ResolveDevice,
                ResolveVersion = ResolveVersion,
                ApplyBackendTimeoutSeconds = value =>
                {
                    if (taskDriver != null)
                    {
                        taskDriver.ApplyBackendTimeout(value);
                    }
                },
                ApplyAdaptiveDecision = ApplyAdaptiveDecision,
                LogInfo = LogInfo,
                LogWarning = LogWarning
            };
        }

        private TaskProgressRequest BuildProgressRequest()
        {
            if (taskTracker == null || !taskTracker.TryGetCurrentTaskReport(out CatchLeafsTaskTracker.TaskRunReport report))
            {
                return null;
            }

            TaskMetricsRequest metrics = BuildMetricsRequest(report);
            float elapsedSeconds = simManager != null
                ? Mathf.Max(0f, simManager.GetCurrentTaskElapsedSeconds())
                : Mathf.Max(0f, report.attemptElapsedSeconds);
            metrics.global_metrics.completion_time = elapsedSeconds;
            return new TaskProgressRequest
            {
                elapsed_seconds = elapsedSeconds,
                global_metrics = metrics.global_metrics,
                scene_metrics = metrics.scene_metrics,
                task_details = AdaptiveTaskReporterFlow.BuildTaskDetails(simManager)
            };
        }

        private void StopProgressSnapshotReporting()
        {
            if (progressSnapshotCoroutine != null)
            {
                StopCoroutine(progressSnapshotCoroutine);
                progressSnapshotCoroutine = null;
            }
        }

        private TaskMetricsRequest BuildMetricsRequest(CatchLeafsTaskTracker.TaskRunReport report)
        {
            int errorCount = Mathf.Max(0, report.missedLeafCount + report.rejectedCatchCount + report.failedObjectiveCount);
            const int promptCount = 0;

            GlobalMetrics metrics = trajectoryRecorder != null
                ? MetricsAggregator.Aggregate(
                    trajectoryRecorder.Buffer.Samples,
                    Mathf.Max(0f, report.attemptElapsedSeconds),
                    errorCount,
                    promptCount)
                : new GlobalMetrics();

            metrics.completion_time = Mathf.Max(0f, report.attemptElapsedSeconds);
            metrics.error_count = errorCount;
            metrics.prompt_count = promptCount;
            metrics.spatial_accuracy = Mathf.Clamp01(report.catchAccuracy);
            metrics.steps_completed = report.completedObjectiveCount;
            metrics.step_errors = report.failedObjectiveCount;

            return new TaskMetricsRequest
            {
                global_metrics = metrics,
                scene_metrics = BuildSceneMetrics(report)
            };
        }

        private static SceneMetric[] BuildSceneMetrics(CatchLeafsTaskTracker.TaskRunReport report)
        {
            var metrics = new List<SceneMetric>(24);
            AddSceneMetric(metrics, "catch_leaves_attempt_index", report.attemptIndex, "count");
            AddSceneMetric(metrics, "catch_leaves_difficulty_level", report.difficultyProfileLevel, "level");
            AddSceneMetric(metrics, "catch_leaves_outcome_failed", IsFailed(report) ? 1f : 0f, "flag");
            AddSceneMetric(metrics, "catch_leaves_configured_leaf_count", report.configuredLeafCount, "count");
            AddSceneMetric(metrics, "catch_leaves_bucket_pickup_count", report.bucketPickupCount, "count");
            AddSceneMetric(metrics, "catch_leaves_bucket_release_count", report.bucketReleaseCount, "count");
            AddSceneMetric(metrics, "catch_leaves_bucket_held_seconds", report.bucketHeldSeconds, "seconds");
            AddSceneMetric(metrics, "catch_leaves_bucket_displacement_m", report.totalBucketDisplacementMeters, "meters");
            AddSceneMetric(metrics, "catch_leaves_catch_step_bucket_displacement_m", report.catchBucketDisplacementMeters, "meters");
            AddSceneMetric(metrics, "catch_leaves_catch_checks", report.totalCatchChecks, "count");
            AddSceneMetric(metrics, "catch_leaves_caught_count", report.caughtLeafCount, "count");
            AddSceneMetric(metrics, "catch_leaves_missed_count", report.missedLeafCount, "count");
            AddSceneMetric(metrics, "catch_leaves_rejected_catch_count", report.rejectedCatchCount, "count");
            AddSceneMetric(metrics, "catch_leaves_respawn_count", report.leafRespawnCount, "count");
            AddSceneMetric(metrics, "catch_leaves_accuracy", report.catchAccuracy, "ratio");
            AddSceneMetric(metrics, "catch_leaves_per_second", report.catchesPerSecond, "rate");
            AddSceneMetric(metrics, "catch_leaves_time_to_first_catch_seconds", report.timeToFirstCatchSeconds, "seconds");
            AddSceneMetric(metrics, "catch_leaves_return_distance_from_start_m", report.returnDistanceFromStartMeters, "meters");
            AddSceneMetric(metrics, "catch_leaves_pickup_elapsed_seconds", report.pickupObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "catch_leaves_catch_elapsed_seconds", report.catchObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "catch_leaves_return_elapsed_seconds", report.returnObjective.elapsedSeconds, "seconds");
            return metrics.ToArray();
        }

        private int ResolveDifficultyLevelForAttempt(CatchLeafsTaskTracker.TaskRunReport report)
        {
            int level = report != null && report.difficultyProfileLevel > 0
                ? report.difficultyProfileLevel
                : ResolveDriverDifficultyLevel();

            if (report != null && AdaptiveRuntimeContext.TryGetTaskDifficulty(report.taskId, out int storedLevel))
            {
                level = storedLevel;
            }

            effectiveDifficultyLevel = taskDriver != null
                ? taskDriver.ClampDifficultyLevel(level)
                : Mathf.Max(1, level);
            if (report != null && !string.IsNullOrWhiteSpace(report.taskId))
            {
                AdaptiveRuntimeContext.SetTaskDifficulty(report.taskId, effectiveDifficultyLevel);
            }

            return effectiveDifficultyLevel;
        }

        private void ApplyDifficultyLevelToDriver(int difficultyLevel)
        {
            if (taskDriver == null)
            {
                return;
            }

            int clampedLevel = taskDriver.ClampDifficultyLevel(difficultyLevel);
            effectiveDifficultyLevel = clampedLevel;
            taskDriver.DifficultyLevel = clampedLevel;
            taskDriver.ApplyDifficulty(clampedLevel);
        }

        private void ApplyAdaptiveDecision(string taskId, int currentDifficultyLevel, string decision)
        {
            int nextLevel = currentDifficultyLevel;
            switch ((decision ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "increase":
                    nextLevel++;
                    break;
                case "decrease":
                    nextLevel--;
                    break;
            }

            if (taskDriver != null)
            {
                nextLevel = taskDriver.ClampDifficultyLevel(nextLevel);
                taskDriver.DifficultyLevel = nextLevel;
            }
            else
            {
                nextLevel = Mathf.Max(1, nextLevel);
            }

            effectiveDifficultyLevel = nextLevel;
            if (!string.IsNullOrWhiteSpace(taskId))
            {
                AdaptiveRuntimeContext.SetTaskDifficulty(taskId, nextLevel);
            }
        }

        private void BeginTrajectoryRecording()
        {
            if (trajectoryRecorder == null)
            {
                return;
            }

            trajectoryRecorder.Target = ResolveTrajectoryTarget();
            trajectoryRecorder.Begin();
        }

        private void StopTrajectoryRecording()
        {
            if (trajectoryRecorder != null && trajectoryRecorder.Buffer.IsRecording)
            {
                trajectoryRecorder.End();
            }
        }

        private Transform ResolveTrajectoryTarget()
        {
            if (trajectoryTarget != null)
            {
                return trajectoryTarget;
            }

            return catchSystem != null ? catchSystem.BucketTransform : null;
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<CatchLeafsTaskDriver>() ?? FindSceneComponent<CatchLeafsTaskDriver>();
            }

            if (taskTracker == null)
            {
                taskTracker = GetComponent<CatchLeafsTaskTracker>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (catchSystem == null)
            {
                catchSystem = FindSceneComponent<BucketLeafCatchSystem>();
            }

            if (trajectoryTarget == null && catchSystem != null)
            {
                trajectoryTarget = catchSystem.BucketTransform;
            }

            if (adaptiveApi == null)
            {
                adaptiveApi = AdaptiveApiClient.Instance;
                if (adaptiveApi == null)
                {
                    adaptiveApi = FindSceneComponent<AdaptiveApiClient>();
                }
            }

            if (adaptiveApi == null && autoCreateAdaptiveApiClient)
            {
                Transform apiClientTransform = transform.Find(ApiClientObjectName);
                if (apiClientTransform == null)
                {
                    var apiClientObject = new GameObject(ApiClientObjectName);
                    apiClientTransform = apiClientObject.transform;
                    apiClientTransform.SetParent(transform, false);
                }

                adaptiveApi = apiClientTransform.GetComponent<AdaptiveApiClient>();
                if (adaptiveApi == null)
                {
                    adaptiveApi = apiClientTransform.gameObject.AddComponent<AdaptiveApiClient>();
                }
            }

            if (trajectoryRecorder == null && autoCreateTrajectoryRecorder)
            {
                Transform recorderTransform = transform.Find(RecorderObjectName);
                if (recorderTransform == null)
                {
                    var recorderObject = new GameObject(RecorderObjectName);
                    recorderTransform = recorderObject.transform;
                    recorderTransform.SetParent(transform, false);
                }

                trajectoryRecorder = recorderTransform.GetComponent<TrajectoryRecorder>();
                if (trajectoryRecorder == null)
                {
                    trajectoryRecorder = recorderTransform.gameObject.AddComponent<TrajectoryRecorder>();
                }
            }

            if (trajectoryRecorder != null)
            {
                trajectoryRecorder.Target = ResolveTrajectoryTarget();
            }
        }

        private bool NeedsReferenceResolution()
        {
            return taskDriver == null ||
                   taskTracker == null ||
                   simManager == null ||
                   catchSystem == null ||
                   (adaptiveApi == null && autoCreateAdaptiveApiClient) ||
                   trajectoryRecorder == null ||
                   ResolveTrajectoryTarget() == null;
        }

        private void SubscribeToTracker()
        {
            if (subscribedToTracker || taskTracker == null)
            {
                return;
            }

            taskTracker.TaskRunStarted += HandleTaskRunStarted;
            taskTracker.TaskRunCompleted += HandleTaskRunCompleted;
            subscribedToTracker = true;
            LogInfo($"Subscribed to task tracker. adaptiveReportingEnabled={adaptiveReportingEnabled}, taskDriver={(taskDriver != null ? taskDriver.TaskId : "<missing>")}.");

            if (taskTracker.TryGetCurrentTaskReport(out CatchLeafsTaskTracker.TaskRunReport activeReport))
            {
                LogInfo($"Recovered active task run after late subscription: taskId={activeReport.taskId}, attempt={activeReport.attemptIndex}.");
                HandleTaskRunStarted(activeReport);
            }
            else
            {
                LogInfo("No active task run to recover at subscription time.");
            }
        }

        private void UnsubscribeFromTracker()
        {
            if (!subscribedToTracker || taskTracker == null)
            {
                return;
            }

            taskTracker.TaskRunStarted -= HandleTaskRunStarted;
            taskTracker.TaskRunCompleted -= HandleTaskRunCompleted;
            subscribedToTracker = false;
        }

        private void RestoreRuntimeState()
        {
            if (adaptiveApi != null)
            {
                adaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
            }

            activeSessionId = AdaptiveRuntimeContext.ActiveSessionId;
            if (taskDriver != null &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(taskDriver.TaskId, out int storedLevel))
            {
                effectiveDifficultyLevel = taskDriver.ClampDifficultyLevel(storedLevel);
            }
            else
            {
                effectiveDifficultyLevel = ResolveDriverDifficultyLevel();
            }
        }

        private int ResolveDriverDifficultyLevel()
        {
            return taskDriver != null
                ? taskDriver.ClampDifficultyLevel(taskDriver.DifficultyLevel)
                : Mathf.Max(1, effectiveDifficultyLevel);
        }

        private string ResolvePatientCode()
        {
            return AdaptiveTaskReporterFlow.ResolvePatientCode(fallbackPatientCode, derivedPatientCodePrefix);
        }

        private string ResolveDominantHand()
        {
            return AdaptiveTaskReporterFlow.ResolveDominantHand(fallbackDominantHand);
        }

        private string ResolvePatientNotes()
        {
            return AdaptiveTaskReporterFlow.ResolvePatientNotes(patientNotes);
        }

        private string ResolveDevice()
        {
            return AdaptiveTaskReporterFlow.ResolveDevice(device);
        }

        private string ResolveVersion()
        {
            return AdaptiveTaskReporterFlow.ResolveVersion(versionOverride);
        }

        private bool IsOwnedTaskReport(CatchLeafsTaskTracker.TaskRunReport report)
        {
            return report != null &&
                   taskDriver != null &&
                   string.Equals(report.taskId, taskDriver.TaskId, StringComparison.Ordinal);
        }

        private static bool IsFailed(CatchLeafsTaskTracker.TaskRunReport report)
        {
            return report != null && AdaptiveTaskReporterFlow.IsFailed(report.outcome);
        }

        private static string ResolveTaskEndTime(CatchLeafsTaskTracker.TaskRunReport report)
        {
            return report != null && !string.IsNullOrWhiteSpace(report.attemptEndedAtUtc)
                ? report.attemptEndedAtUtc
                : DateTime.UtcNow.ToString("o");
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

        private static void AddSceneMetric(ICollection<SceneMetric> metrics, string name, float value, string unit)
        {
            metrics.Add(new SceneMetric
            {
                feature_name = name,
                feature_value = value,
                feature_unit = unit ?? string.Empty
            });
        }

        private void LogInfo(string message)
        {
            if (logAdaptiveFlow && !string.IsNullOrWhiteSpace(message))
            {
                Debug.Log($"[CatchLeafsTaskAdaptiveReporter] {message}", this);
            }
        }

        private void LogWarning(string message)
        {
            if (logAdaptiveFlow && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[CatchLeafsTaskAdaptiveReporter] {message}", this);
            }
        }
    }
}
