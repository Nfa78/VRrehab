namespace VRStrokeRehab.MenuScene
{
    public class MenuContext
    {
        public bool isAuthenticated;
        public string authenticatedEmail = string.Empty;
        public SceneMenuItemData selectedScene;
        public SceneSessionConfiguration sceneConfiguration = new SceneSessionConfiguration();
        public AppSettingsModel appSettings = new AppSettingsModel();

        public void ClearAuthentication()
        {
            isAuthenticated = false;
            authenticatedEmail = string.Empty;
            selectedScene = null;
            sceneConfiguration = new SceneSessionConfiguration();
        }
    }
}
