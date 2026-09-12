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
    public sealed class RakeLeavesTaskAdaptiveReporter : MonoBehaviour
    {
        private const string RecorderObjectName = "RakeLeavesTrajectoryRecorder";
        private const string ApiClientObjectName = "RakeLeavesAdaptiveApiClient";

        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private RakeLeavesTaskDriver taskDriver;
        [SerializeField] private RakeLeavesTaskTracker taskTracker;
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
        [SerializeField] private bool logAdaptiveFlow = true;

        [Header("Patient Defaults")]
        [SerializeField] private string fallbackPatientCode = "P001";
        [SerializeField] private string derivedPatientCodePrefix = "PAT";
        [SerializeField] private string fallbackDominantHand = "right";
        [SerializeField] [TextArea] private string patientNotes = "Rake leaves task runtime session";

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

        public bool LastMetricsSubmitSucceeded => lastMetricsSubmitSucceeded;
        public string LastMetricsError => lastMetricsError;

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

        private void HandleTaskRunStarted(RakeLeavesTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
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

        private void HandleTaskRunCompleted(RakeLeavesTaskTracker.TaskRunReport report)
        {
            if (!adaptiveReportingEnabled || !IsOwnedTaskReport(report))
            {
                return;
            }

            StopTrajectoryRecording();
            AdaptiveTaskAttemptContext completedAttempt = currentAttempt;
            currentAttempt = null;
            StartCoroutine(SubmitMetricsForAttempt(report, completedAttempt));
        }

        private IEnumerator BeginAdaptiveAttempt(AdaptiveTaskAttemptContext attempt)
        {
            yield return AdaptiveTaskReporterFlow.BeginAdaptiveAttempt(CreateFlowBindings(), attempt);
        }

        private IEnumerator SubmitMetricsForAttempt(
            RakeLeavesTaskTracker.TaskRunReport report,
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
                ApplyBackendTimeout = applyBackendTimeout,
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

        private TaskMetricsRequest BuildMetricsRequest(RakeLeavesTaskTracker.TaskRunReport report)
        {
            int errorCount = Mathf.Max(0, report.failedObjectiveCount);
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
            metrics.spatial_accuracy = Mathf.Clamp01(report.rakeObjective.normalizedProgress);
            metrics.steps_completed = report.completedObjectiveCount;
            metrics.step_errors = report.failedObjectiveCount;

            return new TaskMetricsRequest
            {
                global_metrics = metrics,
                scene_metrics = BuildSceneMetrics(report)
            };
        }

        private static SceneMetric[] BuildSceneMetrics(RakeLeavesTaskTracker.TaskRunReport report)
        {
            var metrics = new List<SceneMetric>(28);
            AddSceneMetric(metrics, "rake_leaves_attempt_index", report.attemptIndex, "count");
            AddSceneMetric(metrics, "rake_leaves_difficulty_level", report.difficultyProfileLevel, "level");
            AddSceneMetric(metrics, "rake_leaves_outcome_failed", IsFailed(report) ? 1f : 0f, "flag");
            AddSceneMetric(metrics, "rake_leaves_hoe_pickup_count", report.hoePickupCount, "count");
            AddSceneMetric(metrics, "rake_leaves_hoe_release_count", report.hoeReleaseCount, "count");
            AddSceneMetric(metrics, "rake_leaves_hoe_return_count", report.hoeReturnCount, "count");
            AddSceneMetric(metrics, "rake_leaves_hoe_held_seconds", report.hoeHeldSeconds, "seconds");
            AddSceneMetric(metrics, "rake_leaves_hoe_displacement_m", report.totalHoeDisplacementMeters, "meters");
            AddSceneMetric(metrics, "rake_leaves_step_hoe_displacement_m", report.rakeStepHoeDisplacementMeters, "meters");
            AddSceneMetric(metrics, "rake_leaves_spawned_fallen_leaf_count", report.spawnedFallenLeafCount, "count");
            AddSceneMetric(metrics, "rake_leaves_hoe_leaf_impulse_count", report.hoeLeafImpulseCount, "count");
            AddSceneMetric(metrics, "rake_leaves_total_hoe_leaf_impulse", report.totalHoeLeafImpulse, "impulse");
            AddSceneMetric(metrics, "rake_leaves_average_hoe_leaf_impulse", report.averageHoeLeafImpulse, "impulse");
            AddSceneMetric(metrics, "rake_leaves_time_to_first_contact_seconds", report.timeToFirstHoeLeafContactSeconds, "seconds");
            AddSceneMetric(metrics, "rake_leaves_progress_event_count", report.rakeProgressEventCount, "count");
            AddSceneMetric(metrics, "rake_leaves_raked_leaf_count", report.rakedLeafCount, "count");
            AddSceneMetric(metrics, "rake_leaves_accepted_progress", report.totalAcceptedRakeProgress, "progress");
            AddSceneMetric(metrics, "rake_leaves_progress_per_second", report.rakeProgressPerSecond, "rate");
            AddSceneMetric(metrics, "rake_leaves_conversion_ratio", report.rakeConversionRatio, "ratio");
            AddSceneMetric(metrics, "rake_leaves_time_to_first_progress_seconds", report.timeToFirstRakeProgressSeconds, "seconds");
            AddSceneMetric(metrics, "rake_leaves_return_threshold_m", report.returnDistanceThresholdMeters, "meters");
            AddSceneMetric(metrics, "rake_leaves_return_distance_m", report.returnDistanceFromStartMeters, "meters");
            AddSceneMetric(metrics, "rake_leaves_pickup_elapsed_seconds", report.pickupObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "rake_leaves_action_elapsed_seconds", report.rakeObjective.elapsedSeconds, "seconds");
            AddSceneMetric(metrics, "rake_leaves_return_elapsed_seconds", report.returnObjective.elapsedSeconds, "seconds");
            return metrics.ToArray();
        }

        private int ResolveDifficultyLevelForAttempt(RakeLeavesTaskTracker.TaskRunReport report)
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

            return taskDriver != null ? taskDriver.HoeTransform : null;
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<RakeLeavesTaskDriver>() ?? FindSceneComponent<RakeLeavesTaskDriver>();
            }

            if (taskTracker == null)
            {
                taskTracker = GetComponent<RakeLeavesTaskTracker>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (trajectoryTarget == null && taskDriver != null)
            {
                trajectoryTarget = taskDriver.HoeTransform;
            }

            if (adaptiveApi == null)
            {
                adaptiveApi = FindSceneComponent<AdaptiveApiClient>();
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

        private bool IsOwnedTaskReport(RakeLeavesTaskTracker.TaskRunReport report)
        {
            return report != null &&
                   taskDriver != null &&
                   string.Equals(report.taskId, taskDriver.TaskId, StringComparison.Ordinal);
        }

        private static bool IsFailed(RakeLeavesTaskTracker.TaskRunReport report)
        {
            return report != null && AdaptiveTaskReporterFlow.IsFailed(report.outcome);
        }

        private static string ResolveTaskEndTime(RakeLeavesTaskTracker.TaskRunReport report)
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
                Debug.Log($"[RakeLeavesTaskAdaptiveReporter] {message}", this);
            }
        }

        private void LogWarning(string message)
        {
            if (logAdaptiveFlow && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[RakeLeavesTaskAdaptiveReporter] {message}", this);
            }
        }
    }
}
