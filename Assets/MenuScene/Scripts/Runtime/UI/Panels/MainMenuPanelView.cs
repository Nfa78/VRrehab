using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class MainMenuPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button playButton;
        [SerializeField] private Button signOutButton;
        [SerializeField] private TMP_Text authenticatedUserLabel;

        public event Action PlayRequested;
        public event Action SignOutRequested;

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(HandlePlayClicked);
            }

            if (signOutButton != null)
            {
                signOutButton.onClick.AddListener(HandleSignOutClicked);
            }
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(HandlePlayClicked);
            }

            if (signOutButton != null)
            {
                signOutButton.onClick.RemoveListener(HandleSignOutClicked);
            }
        }

        public void Show(bool shouldShow)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(shouldShow);
        }

        public void SetPlayInteractable(bool interactable)
        {
            if (playButton != null)
            {
                playButton.interactable = interactable;
            }
        }

        public void SetAuthenticatedUser(string email)
        {
            if (authenticatedUserLabel != null)
            {
                authenticatedUserLabel.text = email;
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (playButton == null)
            {
                message = "Play button is not assigned.";
                return false;
            }

            if (signOutButton == null)
            {
                message = "Sign Out button is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandlePlayClicked()
        {
            if (PlayRequested != null)
            {
                PlayRequested.Invoke();
            }
        }

        private void HandleSignOutClicked()
        {
            if (SignOutRequested != null)
            {
                SignOutRequested.Invoke();
            }
        }
    }
}
