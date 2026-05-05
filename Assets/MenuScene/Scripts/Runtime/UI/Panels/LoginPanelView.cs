using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class LoginPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text statusLabel;

        public event Action<string, string> SubmitRequested;
        public event Action BackRequested;

        private void Awake()
        {
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(HandleSubmitClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(HandleBackClicked);
            }
        }

        private void OnDestroy()
        {
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(HandleSubmitClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
            }
        }

        public void Show(bool shouldShow)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(shouldShow);
            if (shouldShow)
            {
                RefreshInputDisplays();
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (emailInput != null)
            {
                emailInput.interactable = interactable;
            }

            if (passwordInput != null)
            {
                passwordInput.interactable = interactable;
            }

            if (submitButton != null)
            {
                submitButton.interactable = interactable;
            }

            if (backButton != null)
            {
                backButton.interactable = interactable;
            }
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        public void ClearStatus()
        {
            SetStatus(string.Empty);
        }

        public void PrefillEmail(string email)
        {
            if (emailInput != null)
            {
                emailInput.text = email;
                RefreshInputFieldDisplay(emailInput);
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (emailInput == null)
            {
                message = "Email input is not assigned.";
                return false;
            }

            if (passwordInput == null)
            {
                message = "Password input is not assigned.";
                return false;
            }

            if (submitButton == null)
            {
                message = "Submit button is not assigned.";
                return false;
            }

            if (backButton == null)
            {
                message = "Back button is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleSubmitClicked()
        {
            if (SubmitRequested != null)
            {
                SubmitRequested.Invoke(GetTrimmedText(emailInput), GetTrimmedText(passwordInput));
            }
        }

        private void HandleBackClicked()
        {
            if (BackRequested != null)
            {
                BackRequested.Invoke();
            }
        }

        private void RefreshInputDisplays()
        {
            RefreshInputFieldDisplay(emailInput);
            RefreshInputFieldDisplay(passwordInput);
        }

        private void RefreshInputFieldDisplay(TMP_InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.SetTextWithoutNotify(inputField.text);
            inputField.ForceLabelUpdate();

            if (inputField.textComponent != null)
            {
                inputField.textComponent.ForceMeshUpdate();
            }

            if (inputField.placeholder is TMP_Text placeholderText)
            {
                placeholderText.ForceMeshUpdate();
            }

            var overlayCanvas = inputField.GetComponentInParent<OVROverlayCanvas>(true);
            if (overlayCanvas != null)
            {
                overlayCanvas.SetFrameDirty();
            }
        }

        private static string GetTrimmedText(TMP_InputField inputField)
        {
            return inputField != null && inputField.text != null ? inputField.text.Trim() : string.Empty;
        }
    }
}
