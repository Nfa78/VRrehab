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
        public bool startPending;
        public bool startSucceeded;
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
            public string TaskId = string.Empty;
            public int CurrentDifficultyLevel = 1;
            public string TaskEndTimeUtc = string.Empty;
            public Func<TaskMetricsRequest> BuildMetricsRequest;
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

            if (bindings.AdaptiveApi == null)
            {
                CompleteAttemptStart(bindings, attempt, false, "AdaptiveApiClient reference is missing.");
                yield break;
            }

            bindings.AdaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
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
                bindings.TaskExecutionStartFailed?.Invoke();
                CompleteAttemptStart(bindings, attempt, false, DescribeError(taskStartResult, "Failed to start adaptive task execution."));
                yield break;
            }

            attempt.taskExecutionId = taskStartResult.data.task_execution_id;
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

        public static IEnumerator SubmitMetricsForAttempt(Bindings bindings, AdaptiveTaskAttemptContext attempt)
        {
            if (bindings == null)
            {
                yield break;
            }

            if (bindings.AdaptiveApi == null)
            {
                SetMetricsFailure(bindings, "AdaptiveApiClient reference is missing.");
                yield break;
            }

            bindings.AdaptiveApi.RestoreRuntimeAuthSessionIfNeeded();
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

            if (metricsResult != null && metricsResult.IsSuccess && metricsResult.data != null)
            {
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
                SetMetricsFailure(bindings, DescribeError(metricsResult, "Failed to submit adaptive task metrics."));
            }

            ApiResult<TaskEndResponse> endResult = null;
            yield return bindings.AdaptiveApi.EndTaskExecutionAsync(
                attempt.taskExecutionId,
                !string.IsNullOrWhiteSpace(bindings.TaskEndTimeUtc)
                    ? bindings.TaskEndTimeUtc
                    : DateTime.UtcNow.ToString("o"),
                result => endResult = result);

            if (endResult == null || !endResult.IsSuccess)
            {
                bindings.LogWarning?.Invoke(DescribeError(endResult, "Failed to end adaptive task execution."));
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
