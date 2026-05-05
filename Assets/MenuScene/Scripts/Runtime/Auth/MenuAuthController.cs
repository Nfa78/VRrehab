using System;
using System.Collections;
using AdaptiveSystem.Api;
using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    public class MenuAuthController : MonoBehaviour
    {
        [SerializeField] private AdaptiveApiClient apiClient;
        [SerializeField] private LoginPanelView loginPanel;
        [SerializeField] private SignupPanelView signupPanel;
        [SerializeField] private MenuFeedbackController feedbackController;

        private bool isBusy;

        public event Action<string> Authenticated;
        public event Action<string> SignUpCompletedRequiresLogin;
        public event Action SignedOut;

        public AdaptiveApiClient ApiClient
        {
            get { return apiClient; }
        }

        public LoginPanelView LoginPanel
        {
            get { return loginPanel; }
        }

        public SignupPanelView SignupPanel
        {
            get { return signupPanel; }
        }

        public bool HasAuthenticatedSession
        {
            get { return apiClient != null && apiClient.HasAccessToken; }
        }

        private void Awake()
        {
            if (loginPanel != null)
            {
                loginPanel.SubmitRequested += HandleLoginSubmitted;
            }

            if (signupPanel != null)
            {
                signupPanel.SubmitRequested += HandleSignUpSubmitted;
            }
        }

        private void OnDestroy()
        {
            if (loginPanel != null)
            {
                loginPanel.SubmitRequested -= HandleLoginSubmitted;
            }

            if (signupPanel != null)
            {
                signupPanel.SubmitRequested -= HandleSignUpSubmitted;
            }
        }

        public void SignOut()
        {
            if (apiClient != null)
            {
                apiClient.ClearAuthSession();
            }

            if (feedbackController != null)
            {
                feedbackController.ShowInfo("Signed out.");
            }

            if (SignedOut != null)
            {
                SignedOut.Invoke();
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (apiClient == null)
            {
                message = "AdaptiveApiClient is not assigned.";
                return false;
            }

            if (loginPanel == null)
            {
                message = "Login panel is not assigned.";
                return false;
            }

            if (signupPanel == null)
            {
                message = "Signup panel is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleLoginSubmitted(string email, string password)
        {
            if (isBusy)
            {
                return;
            }

            if (!ValidateCredentials(email, password, loginPanel))
            {
                return;
            }

            StartCoroutine(SignInRoutine(email, password));
        }

        private void HandleSignUpSubmitted(string email, string password, string confirmPassword)
        {
            if (isBusy)
            {
                return;
            }

            if (!ValidateCredentials(email, password, signupPanel))
            {
                return;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                signupPanel.SetStatus("Passwords do not match.");
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Passwords do not match.");
                }

                return;
            }

            StartCoroutine(SignUpRoutine(email, password));
        }

        private IEnumerator SignInRoutine(string email, string password)
        {
            isBusy = true;
            SetPanelsInteractable(false);
            loginPanel.ClearStatus();
            if (feedbackController != null)
            {
                feedbackController.ShowInfo("Signing in...");
            }

            ApiResult<AdaptiveSystem.Models.AuthSessionResponse> signInResult = null;
            yield return apiClient.SignInAsync(email, password, result => signInResult = result);

            isBusy = false;
            SetPanelsInteractable(true);

            if (signInResult != null && signInResult.IsSuccess && signInResult.data != null && !string.IsNullOrWhiteSpace(signInResult.data.access_token))
            {
                if (feedbackController != null)
                {
                    feedbackController.ShowSuccess("Authentication successful.");
                }

                if (Authenticated != null)
                {
                    Authenticated.Invoke(email);
                }

                yield break;
            }

            var message = ExtractErrorMessage(signInResult, "Sign in failed.");
            loginPanel.SetStatus(message);
            if (feedbackController != null)
            {
                feedbackController.ShowError(message);
            }
        }

        private IEnumerator SignUpRoutine(string email, string password)
        {
            isBusy = true;
            SetPanelsInteractable(false);
            signupPanel.ClearStatus();
            if (feedbackController != null)
            {
                feedbackController.ShowInfo("Creating account...");
            }

            ApiResult<AuthSignUpResult> signUpResult = null;
            yield return apiClient.SignUpAsync(email, password, result => signUpResult = result);

            isBusy = false;
            SetPanelsInteractable(true);

            if (signUpResult != null && signUpResult.IsSuccess)
            {
                if (signUpResult.data != null && signUpResult.data.HasSession)
                {
                    if (feedbackController != null)
                    {
                        feedbackController.ShowSuccess("Account created and authenticated.");
                    }

                    if (Authenticated != null)
                    {
                        Authenticated.Invoke(email);
                    }

                    yield break;
                }

                var infoMessage = "Account created. Please sign in to continue.";
                signupPanel.SetStatus(infoMessage);
                if (feedbackController != null)
                {
                    feedbackController.ShowInfo(infoMessage);
                }

                if (SignUpCompletedRequiresLogin != null)
                {
                    SignUpCompletedRequiresLogin.Invoke(email);
                }

                yield break;
            }

            var message = ExtractErrorMessage(signUpResult, "Sign up failed.");
            signupPanel.SetStatus(message);
            if (feedbackController != null)
            {
                feedbackController.ShowError(message);
            }
        }

        private bool ValidateCredentials(string email, string password, LoginPanelView panel)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                panel.SetStatus("Email is required.");
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Email is required.");
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                panel.SetStatus("Password is required.");
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Password is required.");
                }

                return false;
            }

            return true;
        }

        private bool ValidateCredentials(string email, string password, SignupPanelView panel)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                panel.SetStatus("Email is required.");
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Email is required.");
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                panel.SetStatus("Password is required.");
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Password is required.");
                }

                return false;
            }

            return true;
        }

        private void SetPanelsInteractable(bool interactable)
        {
            if (loginPanel != null)
            {
                loginPanel.SetInteractable(interactable);
            }

            if (signupPanel != null)
            {
                signupPanel.SetInteractable(interactable);
            }
        }

        private static string ExtractErrorMessage<T>(ApiResult<T> result, string fallback)
        {
            if (result == null)
            {
                return fallback;
            }

            if (result.error != null && !string.IsNullOrWhiteSpace(result.error.message))
            {
                return result.error.message;
            }

            if (!string.IsNullOrWhiteSpace(result.transport_error))
            {
                return result.transport_error;
            }

            return fallback;
        }
    }
}
