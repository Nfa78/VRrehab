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
    public sealed class SeedsTaskAdaptiveReporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private SeedsTaskDriver taskDriver;
        [SerializeField] private SeedsTaskTracker taskTracker;
        [SerializeField] private SeedPickupState seedPickupState;
        [SerializeField] private AdaptiveApiClient adaptiveApi;
        [SerializeField] private TrajectoryRecorder trajectoryRecorder;
        [SerializeField] private Transform trajectoryTarget;
        [SerializeField] private bool preferActiveHandForTrajectory = true;
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool autoCreateTrajectoryRecorder = true;

        [Header("Adaptive Flow")]
        [SerializeField] private bool adaptiveReportingEnabled = true;
        [SerializeField] private bool autoCreatePatientProfile = true;
        [SerializeField] private bool autoStartSession = true;
        [SerializeField] private bool autoApplyAdaptiveDecisionToNextRun = true;
        [SerializeField] [Min(0.1f)] private float taskExecutionStartWaitSeconds = 5f;
        [SerializeField] private bool logAdaptiveFlow = true;

        [Header("Patient Defaults")]
        [SerializeField] private string fallbackPatientCode = "P001";
        [SerializeField] private string derivedPatientCodePrefix = "PAT";
        [SerializeField] private string fallbackDominantHand = "right";
        [SerializeField] [TextArea] private string patientNotes = "Seeds task runtime session";

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

        private void Awake()
        {
            EnsureReferences();
            RestoreRuntimeState();
            RefreshTrajectoryTarget();
        }

        private void Reset()
        {
            EnsureReferences();
            RefreshTrajectoryTarget();
        }

        private void OnEnable()
        {
            EnsureReferences();
            RestoreRuntimeState();
            RefreshTrajectoryTarget();
            SubscribeToTracker();
        }

        private void OnDisable()
        {
            UnsubscribeFromTracker();
        }

        private void Update()
        {
            if (!autoResolveReferences || !NeedsReferenceResolution())
            {
                RefreshTrajectoryTarget();
                return;
            }

            EnsureReferences();
            RestoreRuntimeState();
            RefreshTrajectoryTarget();
            SubscribeToTracker();
        }

        private void HandleTaskRunStarted(SeedsTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

            EnsureReferences();
            RestoreRuntimeState();
            RefreshTrajectoryTarget();
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

        private void HandleTaskRunCompleted(SeedsTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

            AdaptiveTaskAttemptContext completedAttempt = currentAttempt;
            currentAttempt = null;
            StartCoroutine(SubmitMetricsForAttempt(report, completedAttempt));
        }

        private IEnumerator BeginAdaptiveAttempt(AdaptiveTaskAttemptContext attempt)
        {
            yield return AdaptiveTaskReporterFlow.BeginAdaptiveAttempt(CreateFlowBindings(), attempt);
        }

        private IEnumerator SubmitMetricsForAttempt(
            SeedsTaskTracker.TaskRunReport report,
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
                    ResolveTaskEndTime(report)),
                attempt);
        }

        private AdaptiveTaskReporterFlow.Bindings CreateFlowBindings(
            Func<TaskMetricsRequest> buildMetricsRequest = null,
            string taskId = "",
            int currentDifficultyLevel = 1,
            string taskEndTimeUtc = "")
        {
            return new AdaptiveTaskReporterFlow.Bindings
            {
                AdaptiveApi = adaptiveApi,
                AutoCreatePatientProfile = autoCreatePatientProfile,
                AutoStartSession = autoStartSession,
                AutoApplyAdaptiveDecisionToNextRun = autoApplyAdaptiveDecisionToNextRun,
                TaskExecutionStartWaitSeconds = taskExecutionStartWaitSeconds,
                TaskId = taskId,
                CurrentDifficultyLevel = currentDifficultyLevel,
                TaskEndTimeUtc = taskEndTimeUtc,
                BuildMetricsRequest = buildMetricsRequest,
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

        private TaskMetricsRequest BuildMetricsRequest(SeedsTaskTracker.TaskRunReport report)
        {
            int errorCount = Mathf.Max(
                0,
                Mathf.Max(report.totalFailedSequenceResetCount, report.totalWrongGateRejectCount));
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
            metrics.spatial_accuracy = Mathf.Clamp01(report.successPerReleaseRate);
            metrics.steps_completed = CountCompletedObjectives(report);
            metrics.step_errors = CountFailedObjectives(report);

            return new TaskMetricsRequest
            {
                global_metrics = metrics,
                scene_metrics = BuildSceneMetrics(report)
            };
        }

        private SceneMetric[] BuildSceneMetrics(SeedsTaskTracker.TaskRunReport report)
        {
            var metrics = new List<SceneMetric>(20);
            AddSceneMetric(metrics, "seeds_attempt_index", report.attemptIndex, "count");
            AddSceneMetric(metrics, "seeds_difficulty_level", report.difficultyProfileLevel, "level");
            AddSceneMetric(metrics, "seeds_outcome_failed", string.Equals(report.outcome, "failed", StringComparison.OrdinalIgnoreCase) ? 1f : 0f, "flag");
            AddSceneMetric(metrics, "seeds_pickup_events", report.totalPickupEvents, "count");
            AddSceneMetric(metrics, "seeds_throw_release_count", report.totalThrowReleaseCount, "count");
            AddSceneMetric(metrics, "seeds_spawned_seed_count", report.totalSpawnedSeedCount, "count");
            AddSceneMetric(metrics, "seeds_successful_throw_count", report.totalSuccessfulThrowCount, "count");
            AddSceneMetric(metrics, "seeds_gate_pass_count", report.totalGatePassCount, "count");
            AddSceneMetric(metrics, "seeds_wrong_gate_reject_count", report.totalWrongGateRejectCount, "count");
            AddSceneMetric(metrics, "seeds_failed_sequence_reset_count", report.totalFailedSequenceResetCount, "count");
            AddSceneMetric(metrics, "seeds_timeout_reset_count", report.totalTimeoutResetCount, "count");
            AddSceneMetric(metrics, "seeds_objective_hidden_reset_count", report.totalObjectiveHiddenResetCount, "count");
            AddSceneMetric(metrics, "seeds_difficulty_reset_count", report.totalDifficultyResetCount, "count");
            AddSceneMetric(metrics, "seeds_average_release_speed", report.averageReleaseSpeed, "mps");
            AddSceneMetric(metrics, "seeds_peak_release_speed", report.peakReleaseSpeed, "mps");
            AddSceneMetric(metrics, "seeds_success_per_release_rate", report.successPerReleaseRate, "ratio");
            AddSceneMetric(metrics, "seeds_gate_passes_per_release", report.gatePassesPerRelease, "ratio");
            AddSceneMetric(metrics, "seeds_pick_elapsed_seconds", report.pickObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "seeds_throw_elapsed_seconds", report.throwObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "seeds_return_elapsed_seconds", report.returnObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "seeds_throw_first_success_seconds", report.throwObjective.firstSuccessfulThrowSecondsFromStart, "seconds");
            AddSceneMetric(metrics, "seeds_throw_peak_gate_depth", report.throwObjective.peakGateSequenceDepth, "count");
            AddSceneMetric(metrics, "seeds_throw_gate_target_count", report.throwObjective.sequenceGateCountTarget, "count");
            return metrics.ToArray();
        }

        private int ResolveDifficultyLevelForAttempt(SeedsTaskTracker.TaskRunReport report)
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

        private int CountCompletedObjectives(SeedsTaskTracker.TaskRunReport report)
        {
            if (report == null)
            {
                return 0;
            }

            int count = 0;
            count += report.pickObjective != null && report.pickObjective.completed ? 1 : 0;
            count += report.throwObjective != null && report.throwObjective.completed ? 1 : 0;
            count += report.returnObjective != null && report.returnObjective.completed ? 1 : 0;
            return count;
        }

        private int CountFailedObjectives(SeedsTaskTracker.TaskRunReport report)
        {
            if (report == null)
            {
                return 0;
            }

            int count = 0;
            count += report.pickObjective != null && report.pickObjective.failed ? 1 : 0;
            count += report.throwObjective != null && report.throwObjective.failed ? 1 : 0;
            count += report.returnObjective != null && report.returnObjective.failed ? 1 : 0;
            return count;
        }

        private string ResolveTaskEndTime(SeedsTaskTracker.TaskRunReport report)
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

        private bool IsOwnedTaskReport(SeedsTaskTracker.TaskRunReport report)
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
                   seedPickupState == null ||
                   adaptiveApi == null ||
                   trajectoryRecorder == null;
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<SeedsTaskDriver>();
            }

            if (taskTracker == null)
            {
                taskTracker = GetComponent<SeedsTaskTracker>();
            }

            if (seedPickupState == null)
            {
                seedPickupState = GetComponent<SeedPickupState>() ?? GetComponentInChildren<SeedPickupState>(true);
            }

            if (seedPickupState == null)
            {
                seedPickupState = FindSceneComponent<SeedPickupState>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (adaptiveApi == null)
            {
                adaptiveApi = FindSceneComponent<AdaptiveApiClient>();
            }

            if (trajectoryRecorder == null && autoCreateTrajectoryRecorder)
            {
                trajectoryRecorder = GetComponent<TrajectoryRecorder>();
                if (trajectoryRecorder == null)
                {
                    trajectoryRecorder = gameObject.AddComponent<TrajectoryRecorder>();
                }
            }
        }

        private void RefreshTrajectoryTarget()
        {
            if (trajectoryRecorder == null)
            {
                return;
            }

            trajectoryRecorder.Target = ResolveTrajectoryTarget();
        }

        private void BeginTrajectoryRecording()
        {
            if (trajectoryRecorder == null)
            {
                return;
            }

            RefreshTrajectoryTarget();
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
            if (preferActiveHandForTrajectory && seedPickupState != null && seedPickupState.ActiveHand != null)
            {
                return seedPickupState.ActiveHand;
            }

            return trajectoryTarget;
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
                Debug.Log($"[SeedsTaskAdaptiveReporter] {message}", this);
            }
        }

        private void LogWarning(string message)
        {
            if (logAdaptiveFlow && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[SeedsTaskAdaptiveReporter] {message}", this);
            }
        }
    }
}
