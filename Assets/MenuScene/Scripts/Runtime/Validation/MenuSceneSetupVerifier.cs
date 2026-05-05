using System.Collections.Generic;
using AdaptiveSystem.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class MenuSceneSetupVerifier : MonoBehaviour
    {
        [SerializeField] private MenuFlowController menuFlowController;
        [SerializeField] private MenuAuthController menuAuthController;
        [SerializeField] private SceneCarouselController sceneCarouselController;
        [SerializeField] private SceneConfigurationController sceneConfigurationController;
        [SerializeField] private AppSettingsController appSettingsController;
        [SerializeField] private SceneLauncher sceneLauncher;
        [SerializeField] private EntryPanelView entryPanel;
        [SerializeField] private LoginPanelView loginPanel;
        [SerializeField] private SignupPanelView signupPanel;
        [SerializeField] private MainMenuPanelView mainMenuPanel;
        [SerializeField] private MenuFeedbackController feedbackController;
        [SerializeField] private AdaptiveApiClient adaptiveApiClient;
        [SerializeField] private bool logValidationOnStartInDevelopment = true;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void Start()
        {
            if (logValidationOnStartInDevelopment && Debug.isDebugBuild)
            {
                ValidateAndLog();
            }
        }

        [ContextMenu("Validate Menu Scene Setup")]
        public void ValidateAndLog()
        {
            var issues = CollectIssues();
            if (issues.Count == 0)
            {
                Debug.Log("MenuSceneSetupVerifier: setup validation passed with no issues.", this);
                return;
            }

            var errorCount = 0;
            var warningCount = 0;

            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == MenuValidationSeverity.Error)
                {
                    errorCount++;
                }
                else
                {
                    warningCount++;
                }
            }

            var summary = "MenuSceneSetupVerifier found " + errorCount + " error(s) and " + warningCount + " warning(s).";
            if (errorCount > 0)
            {
                Debug.LogError(summary + "\n" + FormatIssues(issues), this);
            }
            else
            {
                Debug.LogWarning(summary + "\n" + FormatIssues(issues), this);
            }
        }

        public List<MenuValidationIssue> CollectIssues()
        {
            AutoAssignReferences();

            var issues = new List<MenuValidationIssue>();

            Require(menuFlowController, "MenuFlowController", issues);
            Require(menuAuthController, "MenuAuthController", issues);
            Require(sceneCarouselController, "SceneCarouselController", issues);
            Require(sceneConfigurationController, "SceneConfigurationController", issues);
            Require(appSettingsController, "AppSettingsController", issues);
            Require(sceneLauncher, "SceneLauncher", issues);
            Require(entryPanel, "EntryPanelView", issues);
            Require(loginPanel, "LoginPanelView", issues);
            Require(signupPanel, "SignupPanelView", issues);
            Require(mainMenuPanel, "MainMenuPanelView", issues);

            ValidateComponent(menuFlowController, "MenuFlowController", issues);
            ValidateComponent(menuAuthController, "MenuAuthController", issues);
            ValidateComponent(sceneCarouselController, "SceneCarouselController", issues);
            ValidateComponent(sceneConfigurationController, "SceneConfigurationController", issues);
            ValidateComponent(appSettingsController, "AppSettingsController", issues);
            ValidateComponent(entryPanel, "EntryPanelView", issues);
            ValidateComponent(loginPanel, "LoginPanelView", issues);
            ValidateComponent(signupPanel, "SignupPanelView", issues);
            ValidateComponent(mainMenuPanel, "MainMenuPanelView", issues);
            ValidateComponent(feedbackController, "MenuFeedbackController", issues, false);

            ValidateCrossWiring(issues);
            ValidateSceneCatalog(issues);
            ValidateEventSystem(issues);
            ValidateCanvases(issues);
            ValidateAdaptiveApiClient(issues);

            return issues;
        }

        private void AutoAssignReferences()
        {
            if (menuFlowController == null)
            {
                menuFlowController = GetComponentInChildren<MenuFlowController>(true);
            }

            if (menuAuthController == null)
            {
                menuAuthController = GetComponentInChildren<MenuAuthController>(true);
            }

            if (sceneCarouselController == null)
            {
                sceneCarouselController = GetComponentInChildren<SceneCarouselController>(true);
            }

            if (sceneConfigurationController == null)
            {
                sceneConfigurationController = GetComponentInChildren<SceneConfigurationController>(true);
            }

            if (appSettingsController == null)
            {
                appSettingsController = GetComponentInChildren<AppSettingsController>(true);
            }

            if (sceneLauncher == null)
            {
                sceneLauncher = GetComponentInChildren<SceneLauncher>(true);
            }

            if (entryPanel == null)
            {
                entryPanel = GetComponentInChildren<EntryPanelView>(true);
            }

            if (loginPanel == null)
            {
                loginPanel = GetComponentInChildren<LoginPanelView>(true);
            }

            if (signupPanel == null)
            {
                signupPanel = GetComponentInChildren<SignupPanelView>(true);
            }

            if (mainMenuPanel == null)
            {
                mainMenuPanel = GetComponentInChildren<MainMenuPanelView>(true);
            }

            if (feedbackController == null)
            {
                feedbackController = GetComponentInChildren<MenuFeedbackController>(true);
            }

            if (adaptiveApiClient == null)
            {
                adaptiveApiClient = GetComponentInChildren<AdaptiveApiClient>(true);
            }
        }

        private void ValidateCrossWiring(List<MenuValidationIssue> issues)
        {
            if (menuFlowController != null)
            {
                if (entryPanel != null && menuFlowController.EntryPanel != entryPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "EntryPanelView reference does not match the verifier reference."));
                }

                if (loginPanel != null && menuFlowController.LoginPanel != loginPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "LoginPanelView reference does not match the verifier reference."));
                }

                if (signupPanel != null && menuFlowController.SignupPanel != signupPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "SignupPanelView reference does not match the verifier reference."));
                }

                if (mainMenuPanel != null && menuFlowController.MainMenuPanel != mainMenuPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "MainMenuPanelView reference does not match the verifier reference."));
                }

                if (sceneCarouselController != null && menuFlowController.SceneCarouselController != sceneCarouselController)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "SceneCarouselController reference does not match the verifier reference."));
                }

                if (sceneConfigurationController != null && menuFlowController.SceneConfigurationController != sceneConfigurationController)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "SceneConfigurationController reference does not match the verifier reference."));
                }

                if (appSettingsController != null && menuFlowController.AppSettingsController != appSettingsController)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "AppSettingsController reference does not match the verifier reference."));
                }

                if (menuAuthController != null && menuFlowController.AuthController != menuAuthController)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "MenuAuthController reference does not match the verifier reference."));
                }

                if (sceneLauncher != null && menuFlowController.SceneLauncher != sceneLauncher)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuFlowController", "SceneLauncher reference does not match the verifier reference."));
                }
            }

            if (menuAuthController != null)
            {
                if (adaptiveApiClient != null && menuAuthController.ApiClient != adaptiveApiClient)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuAuthController", "AdaptiveApiClient reference does not match the verifier reference."));
                }

                if (loginPanel != null && menuAuthController.LoginPanel != loginPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuAuthController", "LoginPanelView reference does not match the verifier reference."));
                }

                if (signupPanel != null && menuAuthController.SignupPanel != signupPanel)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "MenuAuthController", "SignupPanelView reference does not match the verifier reference."));
                }
            }
        }

        private void ValidateSceneCatalog(List<MenuValidationIssue> issues)
        {
            if (sceneCarouselController == null)
            {
                return;
            }

            if (sceneCarouselController.ConfiguredSceneCount == 0)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "SceneCarouselController", "No scene catalog items or serialized scene entries are configured."));
            }
        }

        private void ValidateAdaptiveApiClient(List<MenuValidationIssue> issues)
        {
            if (adaptiveApiClient == null)
            {
                return;
            }

            string connectionSettingsError;
            if (!adaptiveApiClient.TryGetConnectionSettingsError(out connectionSettingsError))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "AdaptiveApiClient", connectionSettingsError));
                return;
            }

            if (adaptiveApiClient.LoadConnectionSettingsFromJson)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(adaptiveApiClient.PublishableKey))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "AdaptiveApiClient", "Publishable key is empty. Auth requests will fail until this is configured."));
            }

            if (string.IsNullOrWhiteSpace(adaptiveApiClient.AuthBaseUrl))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "AdaptiveApiClient", "Auth base URL is empty."));
            }

            if (string.IsNullOrWhiteSpace(adaptiveApiClient.ApiBaseUrl))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "AdaptiveApiClient", "API base URL is empty."));
            }
        }

        private void ValidateEventSystem(List<MenuValidationIssue> issues)
        {
            var eventSystem = FindObjectOfType<EventSystem>(true);
            if (eventSystem == null)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "Scene", "No EventSystem was found in the scene."));
                return;
            }

            var hasOvrInputModule = false;
            var ovrInputModule = eventSystem.GetComponent<OVRInputModule>();
            if (ovrInputModule != null)
            {
                hasOvrInputModule = true;
            }

            var hasCompatibleModule = false;
            var behaviours = eventSystem.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                {
                    continue;
                }

                var typeName = behaviours[i].GetType().Name;
                if (typeName == "XRUIInputModule" || typeName == "InputSystemUIInputModule" || typeName == "OVRInputModule" || typeName == "StandaloneInputModule")
                {
                    hasCompatibleModule = true;
                    break;
                }
            }

            if (!hasCompatibleModule)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "EventSystem", "No recognized UI input module was found on the EventSystem."));
            }

            if (!hasOvrInputModule)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "EventSystem", "OVRInputModule was not found. For this VR menu, OVRInputModule is the preferred input path."));
                return;
            }

            if (ovrInputModule.rayTransform == null)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "OVRInputModule", "rayTransform is not assigned. Pointer-based VR UI interaction may not work correctly."));
            }
        }

        private void ValidateCanvases(List<MenuValidationIssue> issues)
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            if (canvases == null || canvases.Length == 0)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "Scene", "No Canvas was found in the scene."));
                return;
            }

            var hasWorldSpaceCanvas = false;
            var hasGraphicRaycaster = false;
            var hasOverlayCanvas = false;
            var hasOvrRaycaster = false;
            var overlayCanvasMissingWorldCamera = false;

            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null)
                {
                    continue;
                }

                if (canvases[i].renderMode == RenderMode.WorldSpace)
                {
                    hasWorldSpaceCanvas = true;
                }

                if (canvases[i].GetComponent<GraphicRaycaster>() != null)
                {
                    hasGraphicRaycaster = true;
                }

                if (canvases[i].GetComponent<OVROverlayCanvas>() != null)
                {
                    hasOverlayCanvas = true;
                    if (canvases[i].worldCamera == null)
                    {
                        overlayCanvasMissingWorldCamera = true;
                    }
                }

                if (canvases[i].GetComponent<OVRRaycaster>() != null)
                {
                    hasOvrRaycaster = true;
                }
            }

            if (!hasWorldSpaceCanvas)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "Canvas", "No world-space Canvas was found. VR menus typically require world-space canvases."));
            }

            if (!hasGraphicRaycaster)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, "Canvas", "No GraphicRaycaster was found on any Canvas."));
            }

            if (!hasOverlayCanvas)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "Canvas", "No OVROverlayCanvas was found. For this VR menu, OVROverlayCanvas is the preferred rendering path for clarity."));
            }

            if (!hasOvrRaycaster)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "Canvas", "No OVRRaycaster was found. For this VR menu, OVRRaycaster is the preferred interaction raycaster."));
            }

            if (overlayCanvasMissingWorldCamera)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, "OVROverlayCanvas", "An OVROverlayCanvas is present but its Canvas.worldCamera is not assigned."));
            }
        }

        private static void Require(Object obj, string source, List<MenuValidationIssue> issues)
        {
            if (obj == null)
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
            }
        }

        private static void ValidateComponent(MenuFlowController component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(MenuAuthController component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(SceneCarouselController component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(SceneConfigurationController component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(AppSettingsController component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(EntryPanelView component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(LoginPanelView component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(SignupPanelView component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(MainMenuPanelView component, string source, List<MenuValidationIssue> issues)
        {
            ValidateComponentInternal(component, source, issues, true);
        }

        private static void ValidateComponent(MenuFeedbackController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            ValidateComponentInternal(component, source, issues, required);
        }

        private static void ValidateComponentInternal(MenuFlowController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(MenuAuthController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(SceneCarouselController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(SceneConfigurationController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(AppSettingsController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(EntryPanelView component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(LoginPanelView component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(SignupPanelView component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(MainMenuPanelView component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, message));
            }
        }

        private static void ValidateComponentInternal(MenuFeedbackController component, string source, List<MenuValidationIssue> issues, bool required)
        {
            if (component == null)
            {
                if (required)
                {
                    issues.Add(new MenuValidationIssue(MenuValidationSeverity.Error, source, source + " is missing."));
                }

                return;
            }

            string message;
            if (!component.HasRequiredReferences(out message))
            {
                issues.Add(new MenuValidationIssue(MenuValidationSeverity.Warning, source, message));
            }
        }

        private static string FormatIssues(List<MenuValidationIssue> issues)
        {
            var lines = new System.Text.StringBuilder();
            for (var i = 0; i < issues.Count; i++)
            {
                lines.Append("- [");
                lines.Append(issues[i].Severity);
                lines.Append("] ");
                lines.Append(issues[i].Source);
                lines.Append(": ");
                lines.Append(issues[i].Message);
                if (i < issues.Count - 1)
                {
                    lines.AppendLine();
                }
            }

            return lines.ToString();
        }
    }
}
