using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    public class MenuFlowController : MonoBehaviour
    {
        [SerializeField] private EntryPanelView entryPanel;
        [SerializeField] private LoginPanelView loginPanel;
        [SerializeField] private SignupPanelView signupPanel;
        [SerializeField] private MainMenuPanelView mainMenuPanel;
        [SerializeField] private SceneCarouselController sceneCarouselController;
        [SerializeField] private SceneConfigurationController sceneConfigurationController;
        [SerializeField] private AppSettingsController appSettingsController;
        [SerializeField] private MenuAuthController authController;
        [SerializeField] private SceneLauncher sceneLauncher;
        [SerializeField] private MenuFeedbackController feedbackController;
        [SerializeField] private bool startAuthenticatedForDebug;

        private readonly MenuContext context = new MenuContext();
        private MenuState currentState;

        public EntryPanelView EntryPanel
        {
            get { return entryPanel; }
        }

        public LoginPanelView LoginPanel
        {
            get { return loginPanel; }
        }

        public SignupPanelView SignupPanel
        {
            get { return signupPanel; }
        }

        public MainMenuPanelView MainMenuPanel
        {
            get { return mainMenuPanel; }
        }

        public SceneCarouselController SceneCarouselController
        {
            get { return sceneCarouselController; }
        }

        public SceneConfigurationController SceneConfigurationController
        {
            get { return sceneConfigurationController; }
        }

        public AppSettingsController AppSettingsController
        {
            get { return appSettingsController; }
        }

        public MenuAuthController AuthController
        {
            get { return authController; }
        }

        public SceneLauncher SceneLauncher
        {
            get { return sceneLauncher; }
        }

        private void Awake()
        {
            if (entryPanel != null)
            {
                entryPanel.LoginSelected += HandleLoginSelected;
                entryPanel.SignUpSelected += HandleSignUpSelected;
            }

            if (loginPanel != null)
            {
                loginPanel.BackRequested += HandleBackRequested;
            }

            if (signupPanel != null)
            {
                signupPanel.BackRequested += HandleBackRequested;
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.PlayRequested += HandlePlayRequested;
                mainMenuPanel.SignOutRequested += HandleSignOutRequested;
            }

            if (sceneCarouselController != null)
            {
                sceneCarouselController.SelectionChanged += HandleSceneSelectionChanged;
            }

            if (authController != null)
            {
                authController.Authenticated += HandleAuthenticated;
                authController.SignUpCompletedRequiresLogin += HandleSignUpRequiresLogin;
                authController.SignedOut += HandleSignedOut;
            }
        }

        private void Start()
        {
            SetState(startAuthenticatedForDebug || (authController != null && authController.HasAuthenticatedSession) ? MenuState.MainMenu : MenuState.Entry);

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetPlayInteractable(sceneCarouselController != null && sceneCarouselController.SelectedScene != null);
            }
        }

        private void OnDestroy()
        {
            if (entryPanel != null)
            {
                entryPanel.LoginSelected -= HandleLoginSelected;
                entryPanel.SignUpSelected -= HandleSignUpSelected;
            }

            if (loginPanel != null)
            {
                loginPanel.BackRequested -= HandleBackRequested;
            }

            if (signupPanel != null)
            {
                signupPanel.BackRequested -= HandleBackRequested;
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.PlayRequested -= HandlePlayRequested;
                mainMenuPanel.SignOutRequested -= HandleSignOutRequested;
            }

            if (sceneCarouselController != null)
            {
                sceneCarouselController.SelectionChanged -= HandleSceneSelectionChanged;
            }

            if (authController != null)
            {
                authController.Authenticated -= HandleAuthenticated;
                authController.SignUpCompletedRequiresLogin -= HandleSignUpRequiresLogin;
                authController.SignedOut -= HandleSignedOut;
            }
        }

        public bool HasRequiredReferences(out string message)
        {
            if (entryPanel == null)
            {
                message = "Entry panel is not assigned.";
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

            if (mainMenuPanel == null)
            {
                message = "Main menu panel is not assigned.";
                return false;
            }

            if (sceneCarouselController == null)
            {
                message = "Scene carousel controller is not assigned.";
                return false;
            }

            if (sceneConfigurationController == null)
            {
                message = "Scene configuration controller is not assigned.";
                return false;
            }

            if (appSettingsController == null)
            {
                message = "App settings controller is not assigned.";
                return false;
            }

            if (authController == null)
            {
                message = "Menu auth controller is not assigned.";
                return false;
            }

            if (sceneLauncher == null)
            {
                message = "Scene launcher is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleLoginSelected()
        {
            SetState(MenuState.Login);
        }

        private void HandleSignUpSelected()
        {
            SetState(MenuState.SignUp);
        }

        private void HandleBackRequested()
        {
            SetState(MenuState.Entry);
            if (feedbackController != null)
            {
                feedbackController.Clear();
            }
        }

        private void HandleAuthenticated(string email)
        {
            context.isAuthenticated = true;
            context.authenticatedEmail = email;

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetAuthenticatedUser(email);
            }

            SetState(MenuState.MainMenu);
        }

        private void HandleSignUpRequiresLogin(string email)
        {
            if (loginPanel != null)
            {
                loginPanel.PrefillEmail(email);
            }

            SetState(MenuState.Login);
        }

        private void HandleSceneSelectionChanged(SceneMenuItemData sceneItem)
        {
            context.selectedScene = sceneItem;
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetPlayInteractable(sceneItem != null);
            }
        }

        private void HandlePlayRequested()
        {
            if (sceneCarouselController == null || sceneConfigurationController == null || appSettingsController == null || sceneLauncher == null)
            {
                if (feedbackController != null)
                {
                    feedbackController.ShowError("Menu flow is incomplete.");
                }

                return;
            }

            context.selectedScene = sceneCarouselController.SelectedScene;
            context.sceneConfiguration = sceneConfigurationController.CreateSnapshot();
            context.appSettings = appSettingsController.CreateSnapshot();

            sceneLauncher.Launch(context, feedbackController);
        }

        private void HandleSignOutRequested()
        {
            if (authController != null)
            {
                authController.SignOut();
            }
        }

        private void HandleSignedOut()
        {
            context.ClearAuthentication();
            MenuLaunchContext.Clear();
            SetState(MenuState.Entry);
        }

        private void SetState(MenuState nextState)
        {
            currentState = nextState;

            if (entryPanel != null)
            {
                entryPanel.Show(currentState == MenuState.Entry);
            }

            if (loginPanel != null)
            {
                loginPanel.Show(currentState == MenuState.Login);
            }

            if (signupPanel != null)
            {
                signupPanel.Show(currentState == MenuState.SignUp);
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.Show(currentState == MenuState.MainMenu);
            }
        }
    }
}
