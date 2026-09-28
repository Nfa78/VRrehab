using System;
using System.Collections;
using System.Collections.Generic;
using AdaptiveSystem.Api;
using AdaptiveSystem.Models;
using AdaptiveSystem.Processing;
using AdaptiveSystem.Raw;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class WateringTaskAdaptiveReporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private WaterPlantsTaskDriver taskDriver;
        [SerializeField] private WateringTaskTracker taskTracker;
        [SerializeField] private AdaptiveApiClient adaptiveApi;
        [SerializeField] private TrajectoryRecorder trajectoryRecorder;
        [SerializeField] private Transform trajectoryTarget;
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool autoCreateTrajectoryRecorder = true;

        [Header("Adaptive Flow")]
        [SerializeField] private bool adaptiveReportingEnabled = true;
        [SerializeField] private bool autoCreatePatientProfile = true;
        [SerializeField] private bool autoStartSession = true;
        [SerializeField] private bool autoApplyAdaptiveDecisionToNextRun = true;
        [SerializeField] [Min(0.1f)] private float taskExecutionStartWaitSeconds = 5f;
        [SerializeField] [Min(0.5f)] private float progressSnapshotIntervalSeconds = 3f;
        [SerializeField] private bool logAdaptiveFlow = true;

        [Header("Patient Defaults")]
        [SerializeField] private string fallbackPatientCode = "P001";
        [SerializeField] private string derivedPatientCodePrefix = "PAT";
        [SerializeField] private string fallbackDominantHand = "right";
        [SerializeField] [TextArea] private string patientNotes = "Watering task runtime session";

        [Header("Session Defaults")]
        [SerializeField] private string device = "unity-editor";
        [SerializeField] private string versionOverride = string.Empty;

        [Header("Runtime")]
        [SerializeField] private int effectiveDifficultyLevel = 1;
        [SerializeField] private string activeSessionId = string.Empty;
        [SerializeField] private string activeTaskExecutionId = string.Empty;
        [SerializeField] private string lastTaskExecutionId = string.Empty;
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

        private void Reset()
        {
            EnsureReferences();
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

        private void HandleTaskRunStarted(WateringTaskTracker.TaskRunReport report)
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

            int resolvedDifficultyLevel = ResolveDifficultyLevelForAttempt(report);
            ApplyDifficultyLevelToDriver(resolvedDifficultyLevel);

            currentAttempt = new AdaptiveTaskAttemptContext
            {
                taskId = report.taskId,
                attemptIndex = report.attemptIndex,
                difficultyLevel = resolvedDifficultyLevel,
                startedAtUtc = !string.IsNullOrWhiteSpace(report.attemptStartedAtUtc)
                    ? report.attemptStartedAtUtc
                    : DateTime.UtcNow.ToString("o"),
                startPending = true
            };

            StartCoroutine(BeginAdaptiveAttempt(currentAttempt));
        }

        private void HandleTaskRunCompleted(WateringTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

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
            WateringTaskTracker.TaskRunReport report,
            AdaptiveTaskAttemptContext attempt)
        {
            StopTrajectoryRecording();
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
                BeforeTaskExecutionStart = BeginTrajectoryRecording,
                TaskExecutionStartFailed = StopTrajectoryRecording,
                ApplyAdaptiveDecision = ApplyAdaptiveDecision,
                LogInfo = LogInfo,
                LogWarning = LogWarning
            };
        }

        private TaskProgressRequest BuildProgressRequest()
        {
            if (taskTracker == null || !taskTracker.TryGetCurrentTaskReport(out WateringTaskTracker.TaskRunReport report))
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

        private TaskMetricsRequest BuildMetricsRequest(WateringTaskTracker.TaskRunReport report)
        {
            int errorCount = Mathf.Max(0, report.totalWrongTargetCollisionEvents);
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
            metrics.spatial_accuracy = Mathf.Clamp01(report.overallEstimatedTargetCollisionRate);
            metrics.steps_completed = CountCompletedObjectives(report);
            metrics.step_errors = CountFailedObjectives(report);

            return new TaskMetricsRequest
            {
                global_metrics = metrics,
                scene_metrics = BuildSceneMetrics(report)
            };
        }

        private SceneMetric[] BuildSceneMetrics(WateringTaskTracker.TaskRunReport report)
        {
            var metrics = new List<SceneMetric>(20);
            AddSceneMetric(metrics, "watering_attempt_index", report.attemptIndex, "count");
            AddSceneMetric(metrics, "watering_difficulty_level", report.difficultyProfileLevel, "level");
            AddSceneMetric(metrics, "watering_outcome_failed", string.Equals(report.outcome, "failed", StringComparison.OrdinalIgnoreCase) ? 1f : 0f, "flag");
            AddSceneMetric(metrics, "watering_total_displacement_m", report.totalWaterCanDisplacementMeters, "meters");
            AddSceneMetric(metrics, "watering_total_spill_seconds", report.totalSpillActiveSeconds, "seconds");
            AddSceneMetric(metrics, "watering_spill_activation_count", report.totalSpillActivationCount, "count");
            AddSceneMetric(metrics, "watering_estimated_emitted_particles", report.totalEstimatedEmittedParticles, "count");
            AddSceneMetric(metrics, "watering_scored_collision_events", report.totalScoredCollisionEvents, "count");
            AddSceneMetric(metrics, "watering_wrong_target_collision_events", report.totalWrongTargetCollisionEvents, "count");
            AddSceneMetric(metrics, "watering_total_collision_events", report.totalCollisionEvents, "count");
            AddSceneMetric(metrics, "watering_estimated_target_collision_rate", report.overallEstimatedTargetCollisionRate, "ratio");
            AddSceneMetric(metrics, "watering_estimated_tracked_collision_rate", report.overallEstimatedTrackedCollisionRate, "ratio");
            AddSceneMetric(metrics, "watering_estimated_off_target_collision_rate", report.overallEstimatedOffTargetCollisionRate, "ratio");
            AddSceneMetric(metrics, "watering_first_plant_elapsed_seconds", report.firstPlantObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "watering_second_plant_elapsed_seconds", report.secondPlantObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "watering_first_plant_first_hit_seconds", report.firstPlantObjective.firstScoredHitSecondsFromStart, "seconds");
            AddSceneMetric(metrics, "watering_second_plant_first_hit_seconds", report.secondPlantObjective.firstScoredHitSecondsFromStart, "seconds");
            AddSceneMetric(metrics, "watering_first_plant_displacement_until_first_hit_m", report.firstPlantObjective.waterCanDisplacementUntilFirstScoredHitMeters, "meters");
            AddSceneMetric(metrics, "watering_second_plant_displacement_until_first_hit_m", report.secondPlantObjective.waterCanDisplacementUntilFirstScoredHitMeters, "meters");
            return metrics.ToArray();
        }

        private int ResolveDifficultyLevelForAttempt(WateringTaskTracker.TaskRunReport report)
        {
            int fallbackDifficultyLevel = report != null && report.difficultyProfileLevel > 0
                ? report.difficultyProfileLevel
                : ResolveDriverDifficultyLevel();

            if (report != null &&
                !string.IsNullOrWhiteSpace(report.taskId) &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(report.taskId, out int storedDifficultyLevel))
            {
                fallbackDifficultyLevel = storedDifficultyLevel;
            }

            effectiveDifficultyLevel = taskDriver != null
                ? taskDriver.ClampDifficultyLevel(fallbackDifficultyLevel)
                : Mathf.Max(1, fallbackDifficultyLevel);

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

            int clampedDifficultyLevel = taskDriver.ClampDifficultyLevel(difficultyLevel);
            effectiveDifficultyLevel = clampedDifficultyLevel;
            taskDriver.DifficultyLevel = clampedDifficultyLevel;
            taskDriver.ApplyDifficulty(clampedDifficultyLevel);
        }

        private void ApplyAdaptiveDecision(string taskId, int currentDifficultyLevel, string decision)
        {
            int nextDifficultyLevel = currentDifficultyLevel;
            switch ((decision ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "increase":
                    nextDifficultyLevel++;
                    break;
                case "decrease":
                    nextDifficultyLevel--;
                    break;
            }

            if (taskDriver != null)
            {
                nextDifficultyLevel = taskDriver.ClampDifficultyLevel(nextDifficultyLevel);
                taskDriver.DifficultyLevel = nextDifficultyLevel;
            }
            else
            {
                nextDifficultyLevel = Mathf.Max(1, nextDifficultyLevel);
            }

            effectiveDifficultyLevel = nextDifficultyLevel;

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                AdaptiveRuntimeContext.SetTaskDifficulty(taskId, nextDifficultyLevel);
            }
        }

        private int CountCompletedObjectives(WateringTaskTracker.TaskRunReport report)
        {
            if (report == null)
            {
                return 0;
            }

            int count = 0;
            count += report.pickObjective != null && report.pickObjective.completed ? 1 : 0;
            count += report.firstPlantObjective != null && report.firstPlantObjective.completed ? 1 : 0;
            count += report.secondPlantObjective != null && report.secondPlantObjective.completed ? 1 : 0;
            count += report.returnObjective != null && report.returnObjective.completed ? 1 : 0;
            return count;
        }

        private int CountFailedObjectives(WateringTaskTracker.TaskRunReport report)
        {
            if (report == null)
            {
                return 0;
            }

            int count = 0;
            count += report.pickObjective != null && report.pickObjective.failed ? 1 : 0;
            count += report.firstPlantObjective != null && report.firstPlantObjective.failed ? 1 : 0;
            count += report.secondPlantObjective != null && report.secondPlantObjective.failed ? 1 : 0;
            count += report.returnObjective != null && report.returnObjective.failed ? 1 : 0;
            return count;
        }

        private string ResolveTaskEndTime(WateringTaskTracker.TaskRunReport report)
        {
            return report != null && !string.IsNullOrWhiteSpace(report.attemptEndedAtUtc)
                ? report.attemptEndedAtUtc
                : DateTime.UtcNow.ToString("o");
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

        private bool IsOwnedTaskReport(WateringTaskTracker.TaskRunReport report)
        {
            return report != null &&
                   taskDriver != null &&
                   string.Equals(report.taskId, taskDriver.TaskId, StringComparison.Ordinal);
        }

        private int ResolveDriverDifficultyLevel()
        {
            return taskDriver != null
                ? taskDriver.ClampDifficultyLevel(taskDriver.DifficultyLevel)
                : Mathf.Max(1, effectiveDifficultyLevel);
        }

        private void RestoreRuntimeState()
        {
            if (adaptiveApi != null)
            {
                adaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
            }

            activeSessionId = AdaptiveRuntimeContext.ActiveSessionId;
            if (taskDriver != null &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(taskDriver.TaskId, out int storedDifficultyLevel))
            {
                effectiveDifficultyLevel = taskDriver.ClampDifficultyLevel(storedDifficultyLevel);
            }
            else
            {
                effectiveDifficultyLevel = ResolveDriverDifficultyLevel();
            }
        }

        private bool NeedsReferenceResolution()
        {
            return simManager == null ||
                   taskDriver == null ||
                   taskTracker == null ||
                   adaptiveApi == null ||
                   trajectoryTarget == null ||
                   trajectoryRecorder == null;
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<WaterPlantsTaskDriver>();
            }

            if (taskTracker == null)
            {
                taskTracker = GetComponent<WateringTaskTracker>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (adaptiveApi == null)
            {
                adaptiveApi = AdaptiveApiClient.Instance;
                if (adaptiveApi == null)
                {
                    adaptiveApi = FindSceneComponent<AdaptiveApiClient>();
                }
            }

            if (trajectoryTarget == null && taskDriver != null)
            {
                WaterSpill waterSpill = taskDriver.GetComponentInChildren<WaterSpill>(true);
                if (waterSpill != null)
                {
                    trajectoryTarget = waterSpill.transform;
                }
            }

            if (trajectoryRecorder == null && autoCreateTrajectoryRecorder)
            {
                trajectoryRecorder = GetComponent<TrajectoryRecorder>();
                if (trajectoryRecorder == null)
                {
                    trajectoryRecorder = gameObject.AddComponent<TrajectoryRecorder>();
                }
            }

            if (trajectoryRecorder != null && trajectoryTarget != null)
            {
                trajectoryRecorder.Target = trajectoryTarget;
            }
        }

        private void BeginTrajectoryRecording()
        {
            if (trajectoryRecorder == null)
            {
                return;
            }

            if (trajectoryTarget != null)
            {
                trajectoryRecorder.Target = trajectoryTarget;
            }

            trajectoryRecorder.Begin();
        }

        private void StopTrajectoryRecording()
        {
            if (trajectoryRecorder != null && trajectoryRecorder.Buffer.IsRecording)
            {
                trajectoryRecorder.End();
            }
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

            if (taskTracker.TryGetCurrentTaskReport(out WateringTaskTracker.TaskRunReport activeReport))
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

        private static void AddSceneMetric(ICollection<SceneMetric> metrics, string name, float value, string unit)
        {
            if (metrics == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

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
                Debug.Log($"[WateringTaskAdaptiveReporter] {message}", this);
            }
        }

        private void LogWarning(string message)
        {
            if (logAdaptiveFlow && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[WateringTaskAdaptiveReporter] {message}", this);
            }
        }
    }
}
