using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AdaptiveSystem.Api;
using AdaptiveSystem.Models;
using UnityEngine;
using VRStrokeRehab.MenuScene;

namespace TaskSystem
{
    [Serializable]
    public sealed class AdaptiveTaskAttemptContext
    {
        public string taskId = string.Empty;
        public int attemptIndex;
        public int difficultyLevel = 1;
        public string startedAtUtc = string.Empty;
        public string taskExecutionId = string.Empty;
        public int nextProgressSequenceNumber = 1;
        public bool startPending;
        public bool startSucceeded;
        public bool finalizationStarted;
        public bool progressUploadInFlight;
        public string startError = string.Empty;
    }

    public static class AdaptiveTaskReporterFlow
    {
        public sealed class Bindings
        {
            public AdaptiveApiClient AdaptiveApi;
            public bool AutoCreatePatientProfile;
            public bool AutoStartSession;
            public bool ApplyBackendTimeout;
            public bool AutoApplyAdaptiveDecisionToNextRun;
            public float TaskExecutionStartWaitSeconds = 5f;
            public float ProgressSnapshotIntervalSeconds = 3f;
            public string TaskId = string.Empty;
            public int CurrentDifficultyLevel = 1;
            public string TaskEndTimeUtc = string.Empty;
            public string TaskOutcome = "completed";
            public string TaskFailureReason = string.Empty;
            public Func<TaskMetricsRequest> BuildMetricsRequest;
            public Func<TaskProgressRequest> BuildProgressRequest;
            public Func<string> GetActiveSessionId;
            public Action<string> SetActiveSessionId;
            public Func<string> GetActiveTaskExecutionId;
            public Action<string> SetActiveTaskExecutionId;
            public Action<string> SetLastTaskExecutionId;
            public Action<int> SetLastBackendTimeoutSeconds;
            public Action<int> SetLastExpectedTimeSeconds;
            public Action<string> SetLastDecision;
            public Action<float> SetLastDecisionConfidence;
            public Action<bool> SetLastMetricsSubmitSucceeded;
            public Action<string> SetLastMetricsError;
            public Action<string> SetLastMetricsPayloadJson;
            public Action<string> SetLastMetricsResponseJson;
            public Func<string> ResolvePatientCode;
            public Func<string> ResolveDominantHand;
            public Func<string> ResolvePatientNotes;
            public Func<string> ResolveDevice;
            public Func<string> ResolveVersion;
            public Action BeforeTaskExecutionStart;
            public Action TaskExecutionStartFailed;
            public Action<int> ApplyBackendTimeoutSeconds;
            public Action<string, int, string> ApplyAdaptiveDecision;
            public Action<string> LogInfo;
            public Action<string> LogWarning;
        }

        public static IEnumerator BeginAdaptiveAttempt(Bindings bindings, AdaptiveTaskAttemptContext attempt)
        {
            if (bindings == null || attempt == null)
            {
                yield break;
            }

            attempt.startPending = true;
            attempt.startSucceeded = false;
            attempt.taskExecutionId = string.Empty;
            attempt.nextProgressSequenceNumber = 1;
            attempt.finalizationStarted = false;
            bindings.LogInfo?.Invoke($"Task reporting begin: taskId={attempt.taskId}, attempt={attempt.attemptIndex}, difficulty={attempt.difficultyLevel}.");

            if (bindings.AdaptiveApi == null)
            {
                bindings.LogWarning?.Invoke("Task reporting stopped before API call: AdaptiveApiClient reference is missing.");
                CompleteAttemptStart(bindings, attempt, false, "AdaptiveApiClient reference is missing.");
                yield break;
            }

            bindings.AdaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
            bindings.LogInfo?.Invoke($"Task reporting API resolved: object={bindings.AdaptiveApi.name}, authenticated={bindings.AdaptiveApi.HasAccessToken}.");
            if (!bindings.AdaptiveApi.HasAccessToken)
            {
                CompleteAttemptStart(bindings, attempt, false, "No authenticated adaptive API session is available.");
                yield break;
            }

            if (bindings.AutoCreatePatientProfile)
            {
                string patientCode = bindings.ResolvePatientCode != null ? bindings.ResolvePatientCode() : "P001";
                string dominantHand = bindings.ResolveDominantHand != null ? bindings.ResolveDominantHand() : "right";
                string notes = bindings.ResolvePatientNotes != null ? bindings.ResolvePatientNotes() : string.Empty;
                AdaptiveRuntimeContext.SetPatientProfile(patientCode, dominantHand, notes);
                bindings.LogInfo?.Invoke($"Ensuring patient profile: patientCode={patientCode}, dominantHand={dominantHand}.");

                ApiResult<PatientProfile> patientResult = null;
                yield return bindings.AdaptiveApi.PutMyPatientAsync(
                    patientCode,
                    dominantHand,
                    notes,
                    result => patientResult = result);

                if (patientResult == null || !patientResult.IsSuccess)
                {
                    CompleteAttemptStart(bindings, attempt, false, DescribeError(patientResult, "Failed to create adaptive patient profile."));
                    yield break;
                }
                bindings.LogInfo?.Invoke("Patient profile request succeeded.");
            }

            string activeSessionId = bindings.GetActiveSessionId != null ? bindings.GetActiveSessionId() : string.Empty;
            if (bindings.AutoStartSession)
            {
                if (string.IsNullOrWhiteSpace(activeSessionId))
                {
                    activeSessionId = AdaptiveRuntimeContext.ActiveSessionId;
                    bindings.SetActiveSessionId?.Invoke(activeSessionId);
                }

                if (string.IsNullOrWhiteSpace(activeSessionId))
                {
                    bindings.LogInfo?.Invoke("No active session found; starting a new adaptive session.");
                    ApiResult<SessionStartResponse> sessionResult = null;
                    yield return bindings.AdaptiveApi.StartSessionAsync(
                        bindings.ResolveDevice != null ? bindings.ResolveDevice() : ResolveDevice(string.Empty),
                        bindings.ResolveVersion != null ? bindings.ResolveVersion() : ResolveVersion(string.Empty),
                        DateTime.UtcNow.ToString("o"),
                        result => sessionResult = result);

                    if (sessionResult == null ||
                        !sessionResult.IsSuccess ||
                        sessionResult.data == null ||
                        string.IsNullOrWhiteSpace(sessionResult.data.session_id))
                    {
                        CompleteAttemptStart(bindings, attempt, false, DescribeError(sessionResult, "Failed to start adaptive rehab session."));
                        yield break;
                    }

                    activeSessionId = sessionResult.data.session_id;
                    bindings.SetActiveSessionId?.Invoke(activeSessionId);
                    AdaptiveRuntimeContext.SetActiveSessionId(activeSessionId);
                    bindings.LogInfo?.Invoke($"Started adaptive rehab session {activeSessionId}.");
                }
                else
                {
                    bindings.LogInfo?.Invoke($"Using existing adaptive session {activeSessionId}.");
                }
            }
            else
            {
                activeSessionId = AdaptiveRuntimeContext.ActiveSessionId;
                bindings.SetActiveSessionId?.Invoke(activeSessionId);
            }

            if (string.IsNullOrWhiteSpace(activeSessionId))
            {
                CompleteAttemptStart(bindings, attempt, false, "Adaptive session id is unavailable.");
                yield break;
            }

            bindings.BeforeTaskExecutionStart?.Invoke();
            bindings.LogInfo?.Invoke($"Starting task execution: sessionId={activeSessionId}, taskId={attempt.taskId}, difficulty={attempt.difficultyLevel}.");

            ApiResult<TaskStartResponse> taskStartResult = null;
            yield return bindings.AdaptiveApi.StartTaskExecutionAsync(
                activeSessionId,
                attempt.taskId,
                attempt.difficultyLevel,
                attempt.startedAtUtc,
                result => taskStartResult = result);

            if (taskStartResult == null ||
                !taskStartResult.IsSuccess ||
                taskStartResult.data == null ||
                string.IsNullOrWhiteSpace(taskStartResult.data.task_execution_id))
            {
                bindings.LogWarning?.Invoke($"Task execution request failed: {DescribeError(taskStartResult, "no response or task_execution_id")}");
                bindings.TaskExecutionStartFailed?.Invoke();
                CompleteAttemptStart(bindings, attempt, false, DescribeError(taskStartResult, "Failed to start adaptive task execution."));
                yield break;
            }

            attempt.taskExecutionId = taskStartResult.data.task_execution_id;
            if (!string.IsNullOrWhiteSpace(taskStartResult.data.status) &&
                !string.Equals(taskStartResult.data.status, "running", StringComparison.OrdinalIgnoreCase))
            {
                CompleteAttemptStart(bindings, attempt, false, $"Task execution started with unexpected status '{taskStartResult.data.status}'.");
                yield break;
            }
            bindings.LogInfo?.Invoke($"Task execution created: taskExecutionId={attempt.taskExecutionId}, timeout={taskStartResult.data.timeout_seconds}s, expected={taskStartResult.data.expected_time_seconds}s.");
            bindings.SetActiveTaskExecutionId?.Invoke(attempt.taskExecutionId);
            bindings.SetLastTaskExecutionId?.Invoke(attempt.taskExecutionId);
            bindings.SetLastBackendTimeoutSeconds?.Invoke(taskStartResult.data.timeout_seconds);
            bindings.SetLastExpectedTimeSeconds?.Invoke(taskStartResult.data.expected_time_seconds);

            if (bindings.ApplyBackendTimeout)
            {
                bindings.ApplyBackendTimeoutSeconds?.Invoke(taskStartResult.data.timeout_seconds);
            }

            CompleteAttemptStart(bindings, attempt, true, string.Empty);
            bindings.LogInfo?.Invoke(
                $"Started adaptive task execution {attempt.taskExecutionId} for attempt {attempt.attemptIndex} " +
                $"at difficulty {attempt.difficultyLevel}.");
        }

        public static IEnumerator SubmitProgressSnapshots(Bindings bindings, AdaptiveTaskAttemptContext attempt)
        {
            if (bindings == null || attempt == null || bindings.AdaptiveApi == null)
            {
                yield break;
            }

            float intervalSeconds = Mathf.Max(0.5f, bindings.ProgressSnapshotIntervalSeconds);
            while (attempt.startSucceeded &&
                   !attempt.finalizationStarted &&
                   !string.IsNullOrWhiteSpace(attempt.taskExecutionId))
            {
                yield return new WaitForSecondsRealtime(intervalSeconds);

                if (attempt.finalizationStarted || !attempt.startSucceeded)
                {
                    yield break;
                }

                TaskProgressRequest request = bindings.BuildProgressRequest != null
                    ? bindings.BuildProgressRequest()
                    : null;
                if (request == null)
                {
                    bindings.LogWarning?.Invoke("Progress snapshot skipped: no active task report is available.");
                    continue;
                }

                request.sequence_number = attempt.nextProgressSequenceNumber;
                request.captured_at = DateTime.UtcNow.ToString("o");

                ApiResult<TaskProgressResponse> progressResult = null;
                attempt.progressUploadInFlight = true;
                yield return bindings.AdaptiveApi.SubmitTaskProgressAsync(
                    attempt.taskExecutionId,
                    request,
                    result => progressResult = result);
                attempt.progressUploadInFlight = false;

                if (progressResult != null && progressResult.IsSuccess)
                {
                    attempt.nextProgressSequenceNumber++;
                    bindings.LogInfo?.Invoke($"Progress snapshot {request.sequence_number} submitted for taskExecutionId={attempt.taskExecutionId}.");
                    continue;
                }

                if (progressResult != null && progressResult.status_code == 409)
                {
                    bindings.LogWarning?.Invoke("Progress reporting stopped because the task execution is no longer running.");
                    yield break;
                }

                bindings.LogWarning?.Invoke(
                    $"Progress snapshot {request.sequence_number} failed and will be retried: " +
                    DescribeError(progressResult, "no response"));
            }
        }

        public static IEnumerator SubmitMetricsForAttempt(Bindings bindings, AdaptiveTaskAttemptContext attempt)
        {
            if (bindings == null)
            {
                yield break;
            }

            if (attempt != null)
            {
                attempt.finalizationStarted = true;
                while (attempt.progressUploadInFlight)
                {
                    yield return null;
                }
            }

            if (bindings.AdaptiveApi == null)
            {
                bindings.LogWarning?.Invoke("Metrics reporting stopped: AdaptiveApiClient reference is missing.");
                SetMetricsFailure(bindings, "AdaptiveApiClient reference is missing.");
                yield break;
            }

            bindings.AdaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
            bindings.LogInfo?.Invoke($"Metrics reporting begin: taskId={bindings.TaskId}, attempt={(attempt != null ? attempt.attemptIndex.ToString() : "unknown")}, authenticated={bindings.AdaptiveApi.HasAccessToken}.");
            if (!bindings.AdaptiveApi.HasAccessToken)
            {
                SetMetricsFailure(bindings, "No authenticated adaptive API session is available.");
                yield break;
            }

            float waitStartedAt = Time.realtimeSinceStartup;
            while (attempt != null &&
                   attempt.startPending &&
                   Time.realtimeSinceStartup - waitStartedAt < bindings.TaskExecutionStartWaitSeconds)
            {
                yield return null;
            }

            if (attempt == null || !attempt.startSucceeded || string.IsNullOrWhiteSpace(attempt.taskExecutionId))
            {
                bindings.LogWarning?.Invoke($"Metrics reporting skipped: task start did not succeed ({(attempt == null ? "attempt missing" : attempt.startError)}).");
                SetMetricsFailure(
                    bindings,
                    attempt != null && !string.IsNullOrWhiteSpace(attempt.startError)
                        ? attempt.startError
                        : "Adaptive task execution did not start before the report completed.");
                yield break;
            }

            bindings.SetActiveTaskExecutionId?.Invoke(attempt.taskExecutionId);
            bindings.SetLastTaskExecutionId?.Invoke(attempt.taskExecutionId);

            TaskMetricsRequest request = bindings.BuildMetricsRequest != null
                ? bindings.BuildMetricsRequest()
                : new TaskMetricsRequest();
            request.is_final = true;
            request.outcome = string.IsNullOrWhiteSpace(bindings.TaskOutcome) ? "completed" : bindings.TaskOutcome;
            request.failure_reason = string.IsNullOrWhiteSpace(bindings.TaskFailureReason) ? null : bindings.TaskFailureReason;
            bindings.SetLastMetricsPayloadJson?.Invoke(JsonUtility.ToJson(request, true));
            bindings.SetLastMetricsResponseJson?.Invoke(string.Empty);
            bindings.SetLastDecision?.Invoke(string.Empty);
            bindings.SetLastDecisionConfidence?.Invoke(0f);
            bindings.SetLastMetricsError?.Invoke(string.Empty);
            bindings.SetLastMetricsSubmitSucceeded?.Invoke(false);

            ApiResult<TaskMetricsResponse> metricsResult = null;
            yield return bindings.AdaptiveApi.SubmitTaskMetricsAsync(
                attempt.taskExecutionId,
                request,
                result => metricsResult = result);

            bool metricsSubmitted = metricsResult != null && metricsResult.IsSuccess && metricsResult.data != null;
            if (metricsSubmitted)
            {
                bindings.LogInfo?.Invoke($"Metrics request succeeded for taskExecutionId={attempt.taskExecutionId}.");
                string decision = metricsResult.data.decision ?? string.Empty;
                bindings.SetLastMetricsSubmitSucceeded?.Invoke(true);
                bindings.SetLastDecision?.Invoke(decision);
                bindings.SetLastDecisionConfidence?.Invoke(metricsResult.data.confidence);
                bindings.SetLastMetricsResponseJson?.Invoke(JsonUtility.ToJson(metricsResult.data, true));
                bindings.SetLastMetricsError?.Invoke(string.Empty);

                if (bindings.AutoApplyAdaptiveDecisionToNextRun)
                {
                    bindings.ApplyAdaptiveDecision?.Invoke(bindings.TaskId, attempt.difficultyLevel, decision);
                }

                bindings.LogInfo?.Invoke(
                    $"Submitted metrics for attempt {attempt.attemptIndex}. " +
                    $"Decision={decision}, Confidence={metricsResult.data.confidence:0.###}.");
            }
            else
            {
                bindings.LogWarning?.Invoke($"Metrics request failed for taskExecutionId={attempt.taskExecutionId}: {DescribeError(metricsResult, "no response")}");
                SetMetricsFailure(bindings, DescribeError(metricsResult, "Failed to submit adaptive task metrics."));
            }

            if (!metricsSubmitted)
            {
                yield break;
            }

            ApiResult<TaskEndResponse> endResult = null;
            yield return bindings.AdaptiveApi.EndTaskExecutionAsync(
                attempt.taskExecutionId,
                !string.IsNullOrWhiteSpace(bindings.TaskEndTimeUtc)
                    ? bindings.TaskEndTimeUtc
                    : DateTime.UtcNow.ToString("o"),
                request.outcome,
                request.failure_reason,
                result => endResult = result);

            if (endResult == null || !endResult.IsSuccess)
            {
                bindings.LogWarning?.Invoke(DescribeError(endResult, "Failed to end adaptive task execution."));
            }
            else
            {
                bindings.LogInfo?.Invoke($"Task execution ended: taskExecutionId={attempt.taskExecutionId}.");
            }

            string activeTaskExecutionId = bindings.GetActiveTaskExecutionId != null
                ? bindings.GetActiveTaskExecutionId()
                : attempt.taskExecutionId;
            if (bindings.SetActiveTaskExecutionId != null &&
                string.Equals(activeTaskExecutionId, attempt.taskExecutionId, StringComparison.Ordinal))
            {
                bindings.SetActiveTaskExecutionId(string.Empty);
            }
        }

        public static string ResolvePatientCode(string fallbackPatientCode, string derivedPatientCodePrefix)
        {
            if (!string.IsNullOrWhiteSpace(AdaptiveRuntimeContext.PatientCode))
            {
                return AdaptiveRuntimeContext.PatientCode;
            }

            string email = AdaptiveRuntimeContext.ResolveAuthenticatedEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                email = MenuLaunchContext.AuthenticatedEmail;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                string localPart = email.Split('@')[0];
                var builder = new StringBuilder(localPart.Length);
                for (int i = 0; i < localPart.Length; i++)
                {
                    char character = localPart[i];
                    if (char.IsLetterOrDigit(character))
                    {
                        builder.Append(char.ToUpperInvariant(character));
                    }
                }

                string prefix = string.IsNullOrWhiteSpace(derivedPatientCodePrefix)
                    ? "PAT"
                    : derivedPatientCodePrefix.Trim().ToUpperInvariant();
                return $"{prefix}-{(builder.Length > 0 ? builder.ToString() : "USER")}";
            }

            return string.IsNullOrWhiteSpace(fallbackPatientCode) ? "P001" : fallbackPatientCode.Trim();
        }

        public static string ResolveDominantHand(string fallbackDominantHand)
        {
            if (!string.IsNullOrWhiteSpace(AdaptiveRuntimeContext.DominantHand))
            {
                return AdaptiveRuntimeContext.DominantHand;
            }

            if (MenuLaunchContext.HasContext)
            {
                return MenuLaunchContext.HandUsed == MenuHandUsed.Left ? "left" : "right";
            }

            return string.Equals(fallbackDominantHand, "left", StringComparison.OrdinalIgnoreCase)
                ? "left"
                : "right";
        }

        public static string ResolvePatientNotes(string patientNotes)
        {
            return !string.IsNullOrWhiteSpace(AdaptiveRuntimeContext.PatientNotes)
                ? AdaptiveRuntimeContext.PatientNotes
                : patientNotes ?? string.Empty;
        }

        public static string ResolveDevice(string device)
        {
            return !string.IsNullOrWhiteSpace(device)
                ? device.Trim()
                : Application.isEditor ? "unity-editor" : Application.platform.ToString();
        }

        public static string ResolveVersion(string versionOverride)
        {
            return !string.IsNullOrWhiteSpace(versionOverride)
                ? versionOverride.Trim()
                : !string.IsNullOrWhiteSpace(Application.version) ? Application.version : "unknown";
        }

        public static bool IsFailed(string outcome)
        {
            return string.Equals(outcome, "failed", StringComparison.OrdinalIgnoreCase);
        }

        public static TaskDetails BuildTaskDetails(SimManager simManager)
        {
            SimTaskObjective objective = simManager != null ? simManager.CurrentObjective : null;
            return new TaskDetails
            {
                current_objective_id = objective != null ? objective.ObjectiveId : string.Empty,
                current_objective_progress = objective != null ? objective.CurrentValue : 0f,
                current_objective_target = objective != null ? objective.MaxValue : 0f
            };
        }

        public static void AddSceneMetric(ICollection<SceneMetric> metrics, string name, float value, string unit)
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

        public static string DescribeError<T>(ApiResult<T> result, string fallback)
        {
            if (result == null)
            {
                return fallback;
            }

            if (result.error != null && !string.IsNullOrWhiteSpace(result.error.message))
            {
                return result.error.message;
            }

            return !string.IsNullOrWhiteSpace(result.transport_error)
                ? result.transport_error
                : fallback;
        }

        private static void CompleteAttemptStart(
            Bindings bindings,
            AdaptiveTaskAttemptContext attempt,
            bool succeeded,
            string error)
        {
            attempt.startPending = false;
            attempt.startSucceeded = succeeded;
            attempt.startError = error ?? string.Empty;
            attempt.taskExecutionId = succeeded ? attempt.taskExecutionId : string.Empty;

            if (!succeeded)
            {
                bindings.SetActiveTaskExecutionId?.Invoke(string.Empty);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    bindings.SetLastMetricsError?.Invoke(error);
                    bindings.LogWarning?.Invoke(error);
                }
            }
        }

        private static void SetMetricsFailure(Bindings bindings, string message)
        {
            bindings.SetLastMetricsSubmitSucceeded?.Invoke(false);
            bindings.SetLastMetricsError?.Invoke(message ?? string.Empty);
            bindings.LogWarning?.Invoke(message);
        }
    }
}
