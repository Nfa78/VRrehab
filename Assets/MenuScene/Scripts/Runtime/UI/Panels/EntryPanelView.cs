using System;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class EntryPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button signUpButton;

        public event Action LoginSelected;
        public event Action SignUpSelected;

        private void Awake()
        {
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(HandleLoginClicked);
            }

            if (signUpButton != null)
            {
                signUpButton.onClick.AddListener(HandleSignUpClicked);
            }
        }

        private void OnDestroy()
        {
            if (loginButton != null)
            {
                loginButton.onClick.RemoveListener(HandleLoginClicked);
            }

            if (signUpButton != null)
            {
                signUpButton.onClick.RemoveListener(HandleSignUpClicked);
            }
        }

        public void Show(bool shouldShow)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(shouldShow);
        }

        public bool HasRequiredReferences(out string message)
        {
            if (loginButton == null)
            {
                message = "Login button is not assigned.";
                return false;
            }

            if (signUpButton == null)
            {
                message = "Sign Up button is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleLoginClicked()
        {
            if (LoginSelected != null)
            {
                LoginSelected.Invoke();
            }
        }

        private void HandleSignUpClicked()
        {
            if (SignUpSelected != null)
            {
                SignUpSelected.Invoke();
            }
        }
    }
}
