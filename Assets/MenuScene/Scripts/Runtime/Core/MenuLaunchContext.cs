namespace VRStrokeRehab.MenuScene
{
    public static class MenuLaunchContext
    {
        public static bool HasContext { get; private set; }
        public static string AuthenticatedEmail { get; private set; }
        public static string SelectedSceneId { get; private set; }
        public static string SelectedSceneTitle { get; private set; }
        public static string SelectedSceneLoadReference { get; private set; }
        public static MenuHandUsed HandUsed { get; private set; }
        public static float MasterVolume { get; private set; }

        public static void Capture(MenuContext context)
        {
            if (context == null || context.selectedScene == null)
            {
                Clear();
                return;
            }

            HasContext = true;
            AuthenticatedEmail = context.authenticatedEmail;
            SelectedSceneId = context.selectedScene.SceneId;
            SelectedSceneTitle = context.selectedScene.SceneTitle;
            SelectedSceneLoadReference = context.selectedScene.SceneLoadReference;
            HandUsed = context.sceneConfiguration != null ? context.sceneConfiguration.handUsed : MenuHandUsed.Right;
            MasterVolume = context.appSettings != null ? context.appSettings.masterVolume : 1f;
        }

        public static void Clear()
        {
            HasContext = false;
            AuthenticatedEmail = string.Empty;
            SelectedSceneId = string.Empty;
            SelectedSceneTitle = string.Empty;
            SelectedSceneLoadReference = string.Empty;
            HandUsed = MenuHandUsed.Right;
            MasterVolume = 1f;
        }
    }
}
