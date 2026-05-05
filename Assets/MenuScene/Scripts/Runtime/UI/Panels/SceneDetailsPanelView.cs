using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class SceneDetailsPanelView : MonoBehaviour
    {
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;

        public void ShowScene(SceneMenuItemData sceneItem)
        {
            if (previewImage != null)
            {
                previewImage.sprite = sceneItem != null ? sceneItem.PreviewImage : null;
                previewImage.enabled = sceneItem != null && sceneItem.PreviewImage != null;
            }

            if (titleLabel != null)
            {
                titleLabel.text = sceneItem != null ? sceneItem.SceneTitle : "No Scene Selected";
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = sceneItem != null ? sceneItem.ShortDescription : string.Empty;
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (titleLabel == null)
            {
                message = "Title label is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
