using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class SceneConfigurationPanelView : MonoBehaviour
    {
        [SerializeField] private Button leftHandButton;
        [SerializeField] private Button rightHandButton;
        [SerializeField] private TMP_Text selectedHandLabel;
        [SerializeField] private string leftLabel = "Left";
        [SerializeField] private string rightLabel = "Right";

        public event Action<MenuHandUsed> HandSelected;

        private void Awake()
        {
            if (leftHandButton != null)
            {
                leftHandButton.onClick.AddListener(HandleLeftSelected);
            }

            if (rightHandButton != null)
            {
                rightHandButton.onClick.AddListener(HandleRightSelected);
            }
        }

        private void OnDestroy()
        {
            if (leftHandButton != null)
            {
                leftHandButton.onClick.RemoveListener(HandleLeftSelected);
            }

            if (rightHandButton != null)
            {
                rightHandButton.onClick.RemoveListener(HandleRightSelected);
            }
        }

        public void SetSelectedHand(MenuHandUsed handUsed)
        {
            if (selectedHandLabel != null)
            {
                selectedHandLabel.text = handUsed == MenuHandUsed.Left ? leftLabel : rightLabel;
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (leftHandButton == null)
            {
                message = "Left hand button is not assigned.";
                return false;
            }

            if (rightHandButton == null)
            {
                message = "Right hand button is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleLeftSelected()
        {
            if (HandSelected != null)
            {
                HandSelected.Invoke(MenuHandUsed.Left);
            }
        }

        private void HandleRightSelected()
        {
            if (HandSelected != null)
            {
                HandSelected.Invoke(MenuHandUsed.Right);
            }
        }
    }
}
