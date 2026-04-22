using System;
using System.Collections;
using AdaptiveSystem.Models;
using UnityEngine;

namespace AdaptiveSystem.Api
{
    public class AdaptiveApiSmokeTest : MonoBehaviour
    {
        [SerializeField] private AdaptiveApiClient apiClient;
        [SerializeField] private bool trySignUpFirst = true;
        [SerializeField] private string email = "patient@example.com";
        [SerializeField] private string password = "password123";
        [SerializeField] private string patientCode = "P001";
        [SerializeField] private string dominantHand = "right";
        [SerializeField] [TextArea] private string notes = "Unity smoke test";
        [SerializeField] private string device = "unity-editor";
        [SerializeField] private string version = "0.1.0";

        [ContextMenu("Run Smoke Test")]
        public void RunSmokeTest()
        {
            StartCoroutine(RunSmokeTestCoroutine());
        }

        private IEnumerator RunSmokeTestCoroutine()
        {
            if (apiClient == null)
            {
                Debug.LogError("[AdaptiveApiSmokeTest] Missing AdaptiveApiClient reference.");
                yield break;
            }

            if (trySignUpFirst)
            {
                ApiResult<AuthSignUpResult> signUpResult = null;
                yield return apiClient.SignUpAsync(email, password, result => signUpResult = result);
                if (signUpResult != null && signUpResult.IsSuccess)
                {
                    if (signUpResult.data != null && signUpResult.data.HasSession)
                    {
                        Debug.Log("[AdaptiveApiSmokeTest] Signup returned an authenticated session.");
                    }
                    else
                    {
                        Debug.Log("[AdaptiveApiSmokeTest] Signup returned a user payload. Continuing with sign-in.");
                    }
                }
                else if (signUpResult != null)
                {
                    Debug.LogWarning("[AdaptiveApiSmokeTest] Signup failed. Falling back to sign-in. " + DescribeError(signUpResult));
                }
            }

            if (!apiClient.HasAccessToken)
            {
                ApiResult<AuthSessionResponse> signInResult = null;
                yield return apiClient.SignInAsync(email, password, result => signInResult = result);
                if (signInResult == null || !signInResult.IsSuccess)
                {
                    Debug.LogError("[AdaptiveApiSmokeTest] Sign-in failed. " + DescribeError(signInResult));
                    yield break;
                }
            }

            ApiResult<PatientProfile> patientResult = null;
            yield return apiClient.PutMyPatientAsync(patientCode, dominantHand, notes, result => patientResult = result);
            if (patientResult == null || !patientResult.IsSuccess)
            {
                Debug.LogError("[AdaptiveApiSmokeTest] Put patient failed. " + DescribeError(patientResult));
                yield break;
            }

            string startTime = DateTime.UtcNow.ToString("o");
            ApiResult<SessionStartResponse> sessionResult = null;
            yield return apiClient.StartSessionAsync(device, version, startTime, result => sessionResult = result);
            if (sessionResult == null || !sessionResult.IsSuccess)
            {
                Debug.LogError("[AdaptiveApiSmokeTest] Start session failed. " + DescribeError(sessionResult));
                yield break;
            }

            ApiResult<TaskListResponse> tasksResult = null;
            yield return apiClient.ListTasksAsync(result => tasksResult = result);
            if (tasksResult == null || !tasksResult.IsSuccess)
            {
                Debug.LogError("[AdaptiveApiSmokeTest] List tasks failed. " + DescribeError(tasksResult));
                yield break;
            }

            int taskCount = tasksResult.data != null && tasksResult.data.items != null ? tasksResult.data.items.Length : 0;
            Debug.Log(string.Format(
                "[AdaptiveApiSmokeTest] Completed flow. session_id={0}, tasks={1}",
                sessionResult.data != null ? sessionResult.data.session_id : "(none)",
                taskCount));
        }

        private static string DescribeError<T>(ApiResult<T> result)
        {
            if (result == null)
            {
                return "No result returned.";
            }

            if (!string.IsNullOrEmpty(result.transport_error))
            {
                return result.transport_error;
            }

            if (result.error != null && !string.IsNullOrEmpty(result.error.message))
            {
                return string.Format("HTTP {0}: {1}", result.status_code, result.error.message);
            }

            return string.Format("HTTP {0}", result.status_code);
        }
    }
}
