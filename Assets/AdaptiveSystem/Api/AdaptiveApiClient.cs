using System;
using System.Collections;
using System.Text;
using AdaptiveSystem.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace AdaptiveSystem.Api
{
    public class AdaptiveApiClient : MonoBehaviour
    {
        private enum RequestScope
        {
            PublicApi,
            Auth,
            PrivateApi
        }

        [SerializeField] private string authBaseUrl = "http://127.0.0.1:54421/auth/v1";
        [SerializeField] private string apiBaseUrl = "http://127.0.0.1:54421/functions/v1/api";
        [SerializeField] private string publishableKey = string.Empty;
        [SerializeField] private AuthSessionResponse authSession = new AuthSessionResponse();

        public string AuthBaseUrl
        {
            get { return authBaseUrl; }
            set { authBaseUrl = value; }
        }

        public string ApiBaseUrl
        {
            get { return apiBaseUrl; }
            set { apiBaseUrl = value; }
        }

        public string PublishableKey
        {
            get { return publishableKey; }
            set { publishableKey = value; }
        }

        public string AccessToken
        {
            get { return authSession != null ? authSession.access_token : string.Empty; }
        }

        public string RefreshToken
        {
            get { return authSession != null ? authSession.refresh_token : string.Empty; }
        }

        public string TokenType
        {
            get { return authSession != null ? authSession.token_type : string.Empty; }
        }

        public int ExpiresIn
        {
            get { return authSession != null ? authSession.expires_in : 0; }
        }

        public int ExpiresAt
        {
            get { return authSession != null ? authSession.expires_at : 0; }
        }

        public bool HasAccessToken
        {
            get { return !string.IsNullOrEmpty(AccessToken); }
        }

        public IEnumerator HealthAsync(Action<ApiResult<HealthResponse>> onComplete)
        {
            return SendRequest<HealthResponse>(RequestScope.PublicApi, AdaptiveApiRoutes.Health().ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator SignUpAsync(string email, string password, Action<ApiResult<AuthSignUpResult>> onComplete)
        {
            var request = new AuthSignUpRequest
            {
                email = email,
                password = password
            };

            return SendRawRequest(RequestScope.Auth, AdaptiveApiRoutes.SignUp().ToPath(), UnityWebRequest.kHttpVerbPOST, request, raw =>
            {
                var result = CreateBaseResult<AuthSignUpResult>(raw);
                if (!raw.IsHttpSuccess)
                {
                    result.error = ParseError(raw.text, raw.transportError, raw.statusCode);
                    Complete(onComplete, result);
                    return;
                }

                if (string.IsNullOrEmpty(raw.text))
                {
                    result.is_success = true;
                    Complete(onComplete, result);
                    return;
                }

                var signUpResult = new AuthSignUpResult();
                AuthSessionResponse sessionResponse;
                string parseError;
                if (TryDeserialize(raw.text, out sessionResponse, out parseError) && sessionResponse != null && !string.IsNullOrEmpty(sessionResponse.access_token))
                {
                    signUpResult.session = sessionResponse;
                    StoreAuthSession(sessionResponse);
                    result.data = signUpResult;
                    result.is_success = true;
                    Complete(onComplete, result);
                    return;
                }

                AuthUser userResponse;
                if (TryDeserialize(raw.text, out userResponse, out parseError) && userResponse != null && (!string.IsNullOrEmpty(userResponse.id) || !string.IsNullOrEmpty(userResponse.email)))
                {
                    signUpResult.user = userResponse;
                    result.data = signUpResult;
                    result.is_success = true;
                    Complete(onComplete, result);
                    return;
                }

                result.transport_error = parseError;
                result.error = new ErrorResponse
                {
                    message = "Failed to parse signup response."
                };
                Complete(onComplete, result);
            });
        }

        public IEnumerator SignInAsync(string email, string password, Action<ApiResult<AuthSessionResponse>> onComplete)
        {
            var request = new AuthSignInRequest
            {
                email = email,
                password = password
            };

            return SendRequest<AuthSessionResponse>(
                RequestScope.Auth,
                AdaptiveApiRoutes.Token().ToPath() + AdaptiveApiRoutes.PasswordGrantQuery,
                UnityWebRequest.kHttpVerbPOST,
                request,
                result =>
                {
                    if (result != null && result.IsSuccess && result.data != null && !string.IsNullOrEmpty(result.data.access_token))
                    {
                        StoreAuthSession(result.data);
                    }

                    Complete(onComplete, result);
                });
        }

        public IEnumerator GetMyPatientAsync(Action<ApiResult<PatientProfile>> onComplete)
        {
            return SendRequest<PatientProfile>(RequestScope.PrivateApi, AdaptiveApiRoutes.PatientMe().ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator PutMyPatientAsync(string patientCode, string dominantHand, string notes, Action<ApiResult<PatientProfile>> onComplete)
        {
            var request = new PatientUpsertRequest
            {
                patient_code = patientCode,
                dominant_hand = dominantHand,
                notes = notes
            };

            return PutMyPatientAsync(request, onComplete);
        }

        public IEnumerator PutMyPatientAsync(PatientUpsertRequest request, Action<ApiResult<PatientProfile>> onComplete)
        {
            return SendRequest<PatientProfile>(RequestScope.PrivateApi, AdaptiveApiRoutes.PatientMe().ToPath(), UnityWebRequest.kHttpVerbPUT, request, onComplete);
        }

        public IEnumerator PatchMyPatientAsync(PatientPatchRequest request, Action<ApiResult<PatientProfile>> onComplete)
        {
            return SendRequest<PatientProfile>(RequestScope.PrivateApi, AdaptiveApiRoutes.PatientMe().ToPath(), "PATCH", request, onComplete);
        }

        public IEnumerator PatchMyPatientAsync(string patientCode, string dominantHand, string notes, Action<ApiResult<PatientProfile>> onComplete)
        {
            var request = new PatientPatchRequest
            {
                patient_code = patientCode,
                dominant_hand = dominantHand,
                notes = notes
            };

            return PatchMyPatientAsync(request, onComplete);
        }

        public IEnumerator StartSessionAsync(string device, string version, string startTime, Action<ApiResult<SessionStartResponse>> onComplete)
        {
            var request = new SessionStartRequest
            {
                device = device,
                version = version,
                start_time = startTime
            };

            return SendRequest<SessionStartResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.SessionsStart().ToPath(), UnityWebRequest.kHttpVerbPOST, request, onComplete);
        }

        public IEnumerator GetSessionAsync(string sessionId, Action<ApiResult<Session>> onComplete)
        {
            return SendRequest<Session>(RequestScope.PrivateApi, AdaptiveApiRoutes.SessionById(sessionId).ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator ListMySessionsAsync(Action<ApiResult<SessionsListResponse>> onComplete)
        {
            return SendRequest<SessionsListResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.PatientSessions().ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator ListTasksAsync(Action<ApiResult<TaskListResponse>> onComplete)
        {
            return SendRequest<TaskListResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.Tasks().ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator ListTaskLevelsAsync(string taskId, Action<ApiResult<TaskLevelsResponse>> onComplete)
        {
            return SendRequest<TaskLevelsResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskLevels(taskId).ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator GetTaskLevelAsync(string taskId, int difficultyLevel, Action<ApiResult<TaskLevel>> onComplete)
        {
            return SendRequest<TaskLevel>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskLevel(taskId, difficultyLevel).ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator StartTaskExecutionAsync(string sessionId, string taskId, int difficultyLevel, string startTime, Action<ApiResult<TaskStartResponse>> onComplete)
        {
            var request = new TaskStartRequest
            {
                session_id = sessionId,
                task_id = taskId,
                difficulty_level = difficultyLevel,
                start_time = startTime
            };

            return SendRequest<TaskStartResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskExecutions().ToPath(), UnityWebRequest.kHttpVerbPOST, request, onComplete);
        }

        public IEnumerator GetTaskExecutionAsync(string taskExecutionId, Action<ApiResult<TaskExecution>> onComplete)
        {
            return SendRequest<TaskExecution>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskExecutionById(taskExecutionId).ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator SubmitTaskMetricsAsync(string taskExecutionId, TaskMetricsRequest request, Action<ApiResult<TaskMetricsResponse>> onComplete)
        {
            return SendRequest<TaskMetricsResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskMetrics(taskExecutionId).ToPath(), UnityWebRequest.kHttpVerbPOST, request, onComplete);
        }

        public IEnumerator GetTaskMetricsAsync(string taskExecutionId, Action<ApiResult<TaskMetricsGetResponse>> onComplete)
        {
            return SendRequest<TaskMetricsGetResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskMetrics(taskExecutionId).ToPath(), UnityWebRequest.kHttpVerbGET, null, onComplete);
        }

        public IEnumerator EndTaskExecutionAsync(string taskExecutionId, string endTime, Action<ApiResult<TaskEndResponse>> onComplete)
        {
            var request = new TaskEndRequest
            {
                end_time = endTime
            };

            return SendRequest<TaskEndResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.TaskExecutionEnd(taskExecutionId).ToPath(), UnityWebRequest.kHttpVerbPOST, request, onComplete);
        }

        public IEnumerator EndSessionAsync(string sessionId, string endTime, Action<ApiResult<SessionEndResponse>> onComplete)
        {
            var request = new SessionEndRequest
            {
                end_time = endTime
            };

            return SendRequest<SessionEndResponse>(RequestScope.PrivateApi, AdaptiveApiRoutes.SessionsEnd(sessionId).ToPath(), UnityWebRequest.kHttpVerbPOST, request, onComplete);
        }

        public void ClearAuthSession()
        {
            authSession = new AuthSessionResponse();
        }

        private IEnumerator SendRequest<T>(RequestScope scope, string relativePath, string method, object body, Action<ApiResult<T>> onComplete)
        {
            return SendRawRequest(scope, relativePath, method, body, raw =>
            {
                ApiResult<T> result = ParseApiResult<T>(raw);
                Complete(onComplete, result);
            });
        }

        private IEnumerator SendRawRequest(RequestScope scope, string relativePath, string method, object body, Action<RawResponse> onComplete)
        {
            string url = ResolveUrl(scope, relativePath);
            string jsonBody = body != null ? JsonUtility.ToJson(body) : null;

            using (var request = BuildRequest(url, method, jsonBody, scope))
            {
                yield return request.SendWebRequest();

                var response = new RawResponse
                {
                    statusCode = request.responseCode,
                    text = request.downloadHandler != null ? request.downloadHandler.text : null,
                    transportError = GetTransportError(request)
                };

                if (onComplete != null)
                {
                    onComplete(response);
                }
            }
        }

        private UnityWebRequest BuildRequest(string url, string method, string jsonBody, RequestScope scope)
        {
            var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (scope == RequestScope.Auth || scope == RequestScope.PrivateApi)
            {
                request.SetRequestHeader("apikey", publishableKey);
            }

            if (scope == RequestScope.PrivateApi && !string.IsNullOrEmpty(AccessToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            }

            return request;
        }

        private string ResolveUrl(RequestScope scope, string relativePath)
        {
            string baseUrl = scope == RequestScope.Auth ? authBaseUrl : apiBaseUrl;
            return AdaptiveApiRouter.Combine(baseUrl, relativePath);
        }

        private void StoreAuthSession(AuthSessionResponse session)
        {
            if (session == null)
            {
                return;
            }

            if (authSession == null)
            {
                authSession = new AuthSessionResponse();
            }

            authSession.access_token = session.access_token;
            authSession.token_type = session.token_type;
            authSession.expires_in = session.expires_in;
            authSession.expires_at = session.expires_at;
            authSession.refresh_token = session.refresh_token;
            authSession.user = session.user;
        }

        private static ApiResult<T> ParseApiResult<T>(RawResponse raw)
        {
            var result = CreateBaseResult<T>(raw);
            if (!raw.IsHttpSuccess)
            {
                result.error = ParseError(raw.text, raw.transportError, raw.statusCode);
                return result;
            }

            if (string.IsNullOrEmpty(raw.text))
            {
                result.is_success = true;
                return result;
            }

            T data;
            string parseError;
            if (!TryDeserialize(raw.text, out data, out parseError))
            {
                result.transport_error = parseError;
                result.error = new ErrorResponse
                {
                    message = "Failed to parse response JSON."
                };
                return result;
            }

            result.is_success = true;
            result.data = data;
            return result;
        }

        private static ApiResult<T> CreateBaseResult<T>(RawResponse raw)
        {
            return new ApiResult<T>
            {
                is_success = false,
                status_code = raw.statusCode,
                raw_text = raw.text,
                transport_error = raw.transportError
            };
        }

        private static ErrorResponse ParseError(string rawText, string transportError, long statusCode)
        {
            if (!string.IsNullOrEmpty(rawText))
            {
                ErrorResponse parsedError;
                string parseError;
                if (TryDeserialize(rawText, out parsedError, out parseError) && parsedError != null && !string.IsNullOrEmpty(parsedError.message))
                {
                    return parsedError;
                }
            }

            return new ErrorResponse
            {
                message = !string.IsNullOrEmpty(transportError) ? transportError : string.Format("HTTP {0}", statusCode)
            };
        }

        private static bool TryDeserialize<T>(string json, out T value, out string error)
        {
            error = null;
            value = default(T);

            if (typeof(T) == typeof(string))
            {
                value = (T)(object)json;
                return true;
            }

            if (string.IsNullOrEmpty(json))
            {
                return true;
            }

            try
            {
                value = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                value = default(T);
                return false;
            }
        }

        private static string GetTransportError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return request.error;
            }

            return null;
        }

        private static void Complete<T>(Action<ApiResult<T>> onComplete, ApiResult<T> result)
        {
            if (onComplete != null)
            {
                onComplete(result);
            }
        }

        private sealed class RawResponse
        {
            public long statusCode;
            public string text;
            public string transportError;

            public bool IsHttpSuccess
            {
                get { return statusCode >= 200 && statusCode < 300 && string.IsNullOrEmpty(transportError); }
            }
        }
    }
}
