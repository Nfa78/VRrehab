using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class SceneCardView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private GameObject selectedIndicator;

        private SceneMenuItemData boundScene;

        public event Action<SceneMenuItemData> Selected;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelected);
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }
        }

        public void Bind(SceneMenuItemData sceneItem, bool isSelected)
        {
            boundScene = sceneItem;

            if (previewImage != null)
            {
                previewImage.sprite = sceneItem != null ? sceneItem.PreviewImage : null;
                previewImage.enabled = sceneItem != null && sceneItem.PreviewImage != null;
            }

            if (titleLabel != null)
            {
                titleLabel.text = sceneItem != null ? sceneItem.SceneTitle : string.Empty;
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(isSelected);
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (selectButton == null)
            {
                message = "Select button is not assigned.";
                return false;
            }

            if (titleLabel == null)
            {
                message = "Title label is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleSelected()
        {
            if (Selected != null)
            {
                Selected.Invoke(boundScene);
            }
        }
    }
}
