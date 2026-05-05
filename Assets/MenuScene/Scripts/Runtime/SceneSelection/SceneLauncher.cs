using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    public class SceneLauncher : MonoBehaviour
    {
        public bool CanLaunch(SceneMenuItemData sceneItem, out string message)
        {
            if (sceneItem == null)
            {
                message = "No scene is selected.";
                return false;
            }

            if (!sceneItem.HasLoadReference)
            {
                message = "The selected scene does not have a load reference.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool Launch(MenuContext context, MenuFeedbackController feedbackController)
        {
            if (context == null)
            {
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Menu context is missing.");
                }

                return false;
            }

            string message;
            if (!CanLaunch(context.selectedScene, out message))
            {
                if (feedbackController != null)
                {
                    feedbackController.ShowError(message);
                }

                return false;
            }

            MenuLaunchContext.Capture(context);
            bool started = SceneTransitionFader.TryLoadScene(this, context.selectedScene.SceneLoadReference);
            if (!started)
            {
                if (feedbackController != null)
                {
                    feedbackController.ShowWarning("A scene transition is already in progress.");
                }

                return false;
            }

            return true;
        }
    }
}
