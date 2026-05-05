using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRStrokeRehab.MenuScene;
using AdaptiveSystem.Api;

public class MenuSceneAutoSetupWindow : EditorWindow
{
    private const string DefaultRootName = "MenuRoot";
    private const string DefaultGeneratedAssetsFolder = "Assets/MenuScene/ScriptableObjects/Generated";

    [SerializeField] private string rootName = DefaultRootName;
    [SerializeField] private string generatedAssetsFolder = DefaultGeneratedAssetsFolder;
    [SerializeField] private bool createSampleCatalog = true;
    [SerializeField] private bool createAdaptiveApiClientIfMissing = true;
    [SerializeField] private bool preferOvrOverlayCanvas = true;
    [SerializeField] private bool startAuthenticatedForDebug = false;
    [SerializeField] private bool selectCreatedRoot = true;

    [MenuItem("Tools/VR Stroke Rehab/Menu Scene/Auto Setup")]
    public static void OpenWindow()
    {
        var window = GetWindow<MenuSceneAutoSetupWindow>("Menu Scene Auto Setup");
        window.minSize = new Vector2(420f, 280f);
    }

    public static void OpenAndFocus()
    {
        OpenWindow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Menu Scene Auto Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates a default VR menu scaffold in the active scene and wires the current MenuScene runtime scripts.", MessageType.Info);

        rootName = EditorGUILayout.TextField("Root Name", rootName);
        generatedAssetsFolder = EditorGUILayout.TextField("Generated Assets Folder", generatedAssetsFolder);
        createSampleCatalog = EditorGUILayout.Toggle("Create Sample Catalog", createSampleCatalog);
        createAdaptiveApiClientIfMissing = EditorGUILayout.Toggle("Create API Client If Missing", createAdaptiveApiClientIfMissing);
        preferOvrOverlayCanvas = EditorGUILayout.Toggle("Prefer OVR Overlay Canvas", preferOvrOverlayCanvas);
        startAuthenticatedForDebug = EditorGUILayout.Toggle("Start Authenticated For Debug", startAuthenticatedForDebug);
        selectCreatedRoot = EditorGUILayout.Toggle("Select Created Root", selectCreatedRoot);

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Or Update Menu Scaffold", GUILayout.Height(32f)))
        {
            CreateOrUpdateScaffold();
        }
    }

    private void CreateOrUpdateScaffold()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Menu Scene Auto Setup", "No active loaded scene was found.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Menu Scene Auto Setup");

        try
        {
            EnsureFolderPath(generatedAssetsFolder);

            var root = FindSceneRoot(rootName);
            if (root == null)
            {
                root = CreateSceneObject(rootName, null);
            }

            var systemsRoot = GetOrCreateChild(root.transform, "Systems");
            var canvasRoot = GetOrCreateCanvas(root.transform, "MenuCanvas", preferOvrOverlayCanvas);
            var panelsRoot = GetOrCreateUiChild(canvasRoot.transform, "Panels");

            var entryPanel = CreateEntryPanel(panelsRoot.transform);
            var loginPanel = CreateLoginPanel(panelsRoot.transform);
            var signupPanel = CreateSignupPanel(panelsRoot.transform);
            var mainMenuPanel = CreateMainMenuPanel(panelsRoot.transform);
            var feedbackPanel = CreateFeedbackPanel(canvasRoot.transform);

            entryPanel.gameObject.SetActive(true);
            loginPanel.gameObject.SetActive(false);
            signupPanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(false);

            var eventSystem = EnsureEventSystem(preferOvrOverlayCanvas, canvasRoot);
            var apiClient = FindObjectOfType<AdaptiveApiClient>(true);
            if (apiClient == null && createAdaptiveApiClientIfMissing)
            {
                apiClient = GetOrAddComponent<AdaptiveApiClient>(GetOrCreateChild(systemsRoot.transform, "AdaptiveApiClient").gameObject);
            }

            var launcher = GetOrAddComponent<SceneLauncher>(GetOrCreateChild(systemsRoot.transform, "SceneLauncher").gameObject);
            var authController = GetOrAddComponent<MenuAuthController>(GetOrCreateChild(systemsRoot.transform, "MenuAuthController").gameObject);
            var sceneConfigurationController = GetOrAddComponent<SceneConfigurationController>(GetOrCreateChild(systemsRoot.transform, "SceneConfigurationController").gameObject);
            var appSettingsController = GetOrAddComponent<AppSettingsController>(GetOrCreateChild(systemsRoot.transform, "AppSettingsController").gameObject);
            var carouselController = GetOrAddComponent<SceneCarouselController>(GetOrCreateChild(systemsRoot.transform, "SceneCarouselController").gameObject);
            var flowController = GetOrAddComponent<MenuFlowController>(GetOrCreateChild(systemsRoot.transform, "MenuFlowController").gameObject);
            var verifier = GetOrAddComponent<MenuSceneSetupVerifier>(GetOrCreateChild(root.transform, "MenuSceneSetupVerifier").gameObject);

            var catalog = createSampleCatalog ? EnsureSampleCatalogAsset() : null;
            var cardTemplate = EnsureSceneCardTemplate(mainMenuPanel.transform.Find("SceneCarouselPanel/CardContainer"));

            WireEntryPanel(entryPanel);
            WireLoginPanel(loginPanel);
            WireSignupPanel(signupPanel);
            WireMainMenuPanel(mainMenuPanel);
            WireSceneDetailsPanel(mainMenuPanel.transform.Find("SceneDetailsPanel").GetComponent<SceneDetailsPanelView>());
            WireSceneConfigurationPanel(mainMenuPanel.transform.Find("SceneConfigurationPanel").GetComponent<SceneConfigurationPanelView>());
            WireAppSettingsPanel(mainMenuPanel.transform.Find("AppSettingsPanel").GetComponent<AppSettingsPanelView>());
            WireFeedbackPanel(feedbackPanel);

            WireAuthController(authController, apiClient, loginPanel, signupPanel, feedbackPanel);
            WireSceneConfigurationController(sceneConfigurationController, mainMenuPanel.transform.Find("SceneConfigurationPanel").GetComponent<SceneConfigurationPanelView>());
            WireAppSettingsController(appSettingsController, mainMenuPanel.transform.Find("AppSettingsPanel").GetComponent<AppSettingsPanelView>());
            WireCarouselController(
                carouselController,
                catalog,
                mainMenuPanel.transform.Find("SceneCarouselPanel/CardContainer"),
                cardTemplate,
                mainMenuPanel.transform.Find("SceneCarouselPanel/PreviousButton").GetComponent<Button>(),
                mainMenuPanel.transform.Find("SceneCarouselPanel/NextButton").GetComponent<Button>(),
                mainMenuPanel.transform.Find("SceneDetailsPanel").GetComponent<SceneDetailsPanelView>());
            WireFlowController(
                flowController,
                entryPanel,
                loginPanel,
                signupPanel,
                mainMenuPanel,
                carouselController,
                sceneConfigurationController,
                appSettingsController,
                authController,
                launcher,
                feedbackPanel,
                startAuthenticatedForDebug);
            WireVerifier(verifier, flowController, authController, carouselController, sceneConfigurationController, appSettingsController, launcher, entryPanel, loginPanel, signupPanel, mainMenuPanel, feedbackPanel, apiClient);

            if (eventSystem != null)
            {
                Undo.RecordObject(eventSystem, "Mark EventSystem");
                EditorUtility.SetDirty(eventSystem);
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (selectCreatedRoot)
            {
                Selection.activeGameObject = root;
            }

            verifier.ValidateAndLog();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private SceneMenuCatalog EnsureSampleCatalogAsset()
    {
        var catalogPath = CombineAssetPath(generatedAssetsFolder, "SceneMenuCatalog_Generated.asset");
        var itemPath = CombineAssetPath(generatedAssetsFolder, "SceneMenuItem_Generated.asset");

        var catalog = AssetDatabase.LoadAssetAtPath<SceneMenuCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SceneMenuCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        var item = AssetDatabase.LoadAssetAtPath<SceneMenuItemData>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<SceneMenuItemData>();
            AssetDatabase.CreateAsset(item, itemPath);
        }

        SetSerializedString(item, "sceneId", "generated-placeholder-scene");
        SetSerializedString(item, "sceneTitle", "Placeholder Scene");
        SetSerializedString(item, "sceneName", string.Empty);
        SetSerializedString(item, "scenePath", string.Empty);
        SetSerializedString(item, "shortDescription", "Replace this generated entry with a real playable scene.");

        var catalogObject = new SerializedObject(catalog);
        var itemsProperty = catalogObject.FindProperty("items");
        itemsProperty.arraySize = 1;
        itemsProperty.GetArrayElementAtIndex(0).objectReferenceValue = item;
        catalogObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        return catalog;
    }

    private static void WireEntryPanel(EntryPanelView view)
    {
        SetObjectReference(view, "root", view.gameObject);
        SetObjectReference(view, "loginButton", view.transform.Find("LoginButton").GetComponent<Button>());
        SetObjectReference(view, "signUpButton", view.transform.Find("SignUpButton").GetComponent<Button>());
    }

    private static void WireLoginPanel(LoginPanelView view)
    {
        SetObjectReference(view, "root", view.gameObject);
        SetObjectReference(view, "emailInput", view.transform.Find("EmailInput").GetComponent<TMP_InputField>());
        SetObjectReference(view, "passwordInput", view.transform.Find("PasswordInput").GetComponent<TMP_InputField>());
        SetObjectReference(view, "submitButton", view.transform.Find("SubmitButton").GetComponent<Button>());
        SetObjectReference(view, "backButton", view.transform.Find("BackButton").GetComponent<Button>());
        SetObjectReference(view, "statusLabel", view.transform.Find("StatusLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireSignupPanel(SignupPanelView view)
    {
        SetObjectReference(view, "root", view.gameObject);
        SetObjectReference(view, "emailInput", view.transform.Find("EmailInput").GetComponent<TMP_InputField>());
        SetObjectReference(view, "passwordInput", view.transform.Find("PasswordInput").GetComponent<TMP_InputField>());
        SetObjectReference(view, "confirmPasswordInput", view.transform.Find("ConfirmPasswordInput").GetComponent<TMP_InputField>());
        SetObjectReference(view, "submitButton", view.transform.Find("SubmitButton").GetComponent<Button>());
        SetObjectReference(view, "backButton", view.transform.Find("BackButton").GetComponent<Button>());
        SetObjectReference(view, "statusLabel", view.transform.Find("StatusLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireMainMenuPanel(MainMenuPanelView view)
    {
        SetObjectReference(view, "root", view.gameObject);
        SetObjectReference(view, "playButton", view.transform.Find("PlayButton").GetComponent<Button>());
        SetObjectReference(view, "signOutButton", view.transform.Find("SignOutButton").GetComponent<Button>());
        SetObjectReference(view, "authenticatedUserLabel", view.transform.Find("AuthenticatedUserLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireSceneDetailsPanel(SceneDetailsPanelView view)
    {
        SetObjectReference(view, "previewImage", view.transform.Find("PreviewImage").GetComponent<Image>());
        SetObjectReference(view, "titleLabel", view.transform.Find("TitleLabel").GetComponent<TextMeshProUGUI>());
        SetObjectReference(view, "descriptionLabel", view.transform.Find("DescriptionLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireSceneConfigurationPanel(SceneConfigurationPanelView view)
    {
        SetObjectReference(view, "leftHandButton", view.transform.Find("LeftHandButton").GetComponent<Button>());
        SetObjectReference(view, "rightHandButton", view.transform.Find("RightHandButton").GetComponent<Button>());
        SetObjectReference(view, "selectedHandLabel", view.transform.Find("SelectedHandLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireAppSettingsPanel(AppSettingsPanelView view)
    {
        SetObjectReference(view, "volumeSlider", view.transform.Find("VolumeSlider").GetComponent<Slider>());
        SetObjectReference(view, "volumeValueLabel", view.transform.Find("VolumeValueLabel").GetComponent<TextMeshProUGUI>());
    }

    private static void WireFeedbackPanel(MenuFeedbackController controller)
    {
        SetObjectReference(controller, "root", controller.gameObject);
        SetObjectReference(controller, "messageLabel", controller.transform.Find("MessageLabel").GetComponent<TextMeshProUGUI>());
        var closeTransform = controller.transform.Find("Close");
        SetObjectReference(controller, "closeButton", closeTransform != null ? closeTransform.GetComponent<Button>() : null);
    }

    private static void WireAuthController(MenuAuthController controller, AdaptiveApiClient apiClient, LoginPanelView loginPanel, SignupPanelView signupPanel, MenuFeedbackController feedback)
    {
        SetObjectReference(controller, "apiClient", apiClient);
        SetObjectReference(controller, "loginPanel", loginPanel);
        SetObjectReference(controller, "signupPanel", signupPanel);
        SetObjectReference(controller, "feedbackController", feedback);
    }

    private static void WireSceneConfigurationController(SceneConfigurationController controller, SceneConfigurationPanelView view)
    {
        SetObjectReference(controller, "view", view);
    }

    private static void WireAppSettingsController(AppSettingsController controller, AppSettingsPanelView view)
    {
        SetObjectReference(controller, "view", view);
    }

    private static void WireCarouselController(SceneCarouselController controller, SceneMenuCatalog catalog, Transform cardContainer, SceneCardView cardTemplate, Button previousButton, Button nextButton, SceneDetailsPanelView detailsPanel)
    {
        SetObjectReference(controller, "catalog", catalog);
        SetObjectReference(controller, "cardContainer", cardContainer);
        SetObjectReference(controller, "cardPrefab", cardTemplate);
        SetObjectReference(controller, "previousButton", previousButton);
        SetObjectReference(controller, "nextButton", nextButton);
        SetObjectReference(controller, "detailsPanel", detailsPanel);
    }

    private static void WireFlowController(
        MenuFlowController controller,
        EntryPanelView entryPanel,
        LoginPanelView loginPanel,
        SignupPanelView signupPanel,
        MainMenuPanelView mainMenuPanel,
        SceneCarouselController carouselController,
        SceneConfigurationController sceneConfigurationController,
        AppSettingsController appSettingsController,
        MenuAuthController authController,
        SceneLauncher launcher,
        MenuFeedbackController feedbackController,
        bool startAuthenticatedForDebug)
    {
        SetObjectReference(controller, "entryPanel", entryPanel);
        SetObjectReference(controller, "loginPanel", loginPanel);
        SetObjectReference(controller, "signupPanel", signupPanel);
        SetObjectReference(controller, "mainMenuPanel", mainMenuPanel);
        SetObjectReference(controller, "sceneCarouselController", carouselController);
        SetObjectReference(controller, "sceneConfigurationController", sceneConfigurationController);
        SetObjectReference(controller, "appSettingsController", appSettingsController);
        SetObjectReference(controller, "authController", authController);
        SetObjectReference(controller, "sceneLauncher", launcher);
        SetObjectReference(controller, "feedbackController", feedbackController);
        SetSerializedBool(controller, "startAuthenticatedForDebug", startAuthenticatedForDebug);
    }

    private static void WireVerifier(
        MenuSceneSetupVerifier verifier,
        MenuFlowController flowController,
        MenuAuthController authController,
        SceneCarouselController carouselController,
        SceneConfigurationController sceneConfigurationController,
        AppSettingsController appSettingsController,
        SceneLauncher launcher,
        EntryPanelView entryPanel,
        LoginPanelView loginPanel,
        SignupPanelView signupPanel,
        MainMenuPanelView mainMenuPanel,
        MenuFeedbackController feedbackController,
        AdaptiveApiClient apiClient)
    {
        SetObjectReference(verifier, "menuFlowController", flowController);
        SetObjectReference(verifier, "menuAuthController", authController);
        SetObjectReference(verifier, "sceneCarouselController", carouselController);
        SetObjectReference(verifier, "sceneConfigurationController", sceneConfigurationController);
        SetObjectReference(verifier, "appSettingsController", appSettingsController);
        SetObjectReference(verifier, "sceneLauncher", launcher);
        SetObjectReference(verifier, "entryPanel", entryPanel);
        SetObjectReference(verifier, "loginPanel", loginPanel);
        SetObjectReference(verifier, "signupPanel", signupPanel);
        SetObjectReference(verifier, "mainMenuPanel", mainMenuPanel);
        SetObjectReference(verifier, "feedbackController", feedbackController);
        SetObjectReference(verifier, "adaptiveApiClient", apiClient);
    }

    private static EntryPanelView CreateEntryPanel(Transform parent)
    {
        var panel = GetOrCreateUiChild(parent, "EntryPanel");
        Stretch(panel.GetComponent<RectTransform>());
        EnsureVerticalLayout(panel.gameObject);

        CreateLabel(panel.transform, "HeaderLabel", "Welcome");
        CreateButton(panel.transform, "LoginButton", "Login");
        CreateButton(panel.transform, "SignUpButton", "Sign Up");

        return GetOrAddComponent<EntryPanelView>(panel.gameObject);
    }

    private static LoginPanelView CreateLoginPanel(Transform parent)
    {
        var panel = GetOrCreateUiChild(parent, "LoginPanel");
        Stretch(panel.GetComponent<RectTransform>());
        EnsureVerticalLayout(panel.gameObject);

        CreateLabel(panel.transform, "HeaderLabel", "Login");
        CreateInputField(panel.transform, "EmailInput", "Email", false);
        CreateInputField(panel.transform, "PasswordInput", "Password", true);
        CreateButton(panel.transform, "SubmitButton", "Submit");
        CreateButton(panel.transform, "BackButton", "Back");
        CreateLabel(panel.transform, "StatusLabel", string.Empty);

        return GetOrAddComponent<LoginPanelView>(panel.gameObject);
    }

    private static SignupPanelView CreateSignupPanel(Transform parent)
    {
        var panel = GetOrCreateUiChild(parent, "SignupPanel");
        Stretch(panel.GetComponent<RectTransform>());
        EnsureVerticalLayout(panel.gameObject);

        CreateLabel(panel.transform, "HeaderLabel", "Sign Up");
        CreateInputField(panel.transform, "EmailInput", "Email", false);
        CreateInputField(panel.transform, "PasswordInput", "Password", true);
        CreateInputField(panel.transform, "ConfirmPasswordInput", "Confirm Password", true);
        CreateButton(panel.transform, "SubmitButton", "Create Account");
        CreateButton(panel.transform, "BackButton", "Back");
        CreateLabel(panel.transform, "StatusLabel", string.Empty);

        return GetOrAddComponent<SignupPanelView>(panel.gameObject);
    }

    private static MainMenuPanelView CreateMainMenuPanel(Transform parent)
    {
        var panel = GetOrCreateUiChild(parent, "MainMenuPanel");
        Stretch(panel.GetComponent<RectTransform>());
        EnsureVerticalLayout(panel.gameObject);

        CreateLabel(panel.transform, "AuthenticatedUserLabel", "Authenticated User");

        var carouselPanel = GetOrCreateUiChild(panel.transform, "SceneCarouselPanel");
        EnsureHorizontalLayout(carouselPanel.gameObject);
        CreateButton(carouselPanel.transform, "PreviousButton", "<");
        var cardContainer = GetOrCreateUiChild(carouselPanel.transform, "CardContainer");
        EnsureHorizontalLayout(cardContainer.gameObject);
        CreateButton(carouselPanel.transform, "NextButton", ">");

        var detailsPanel = GetOrCreateUiChild(panel.transform, "SceneDetailsPanel");
        EnsureVerticalLayout(detailsPanel.gameObject);
        CreateImage(detailsPanel.transform, "PreviewImage", new Vector2(240f, 140f));
        CreateLabel(detailsPanel.transform, "TitleLabel", "No Scene Selected");
        CreateLabel(detailsPanel.transform, "DescriptionLabel", string.Empty);
        GetOrAddComponent<SceneDetailsPanelView>(detailsPanel.gameObject);

        var configurationPanel = GetOrCreateUiChild(panel.transform, "SceneConfigurationPanel");
        EnsureHorizontalLayout(configurationPanel.gameObject);
        CreateButton(configurationPanel.transform, "LeftHandButton", "Left");
        CreateLabel(configurationPanel.transform, "SelectedHandLabel", "Right");
        CreateButton(configurationPanel.transform, "RightHandButton", "Right");
        GetOrAddComponent<SceneConfigurationPanelView>(configurationPanel.gameObject);

        var appSettingsPanel = GetOrCreateUiChild(panel.transform, "AppSettingsPanel");
        EnsureVerticalLayout(appSettingsPanel.gameObject);
        CreateLabel(appSettingsPanel.transform, "VolumeHeaderLabel", "Audio Volume");
        CreateSlider(appSettingsPanel.transform, "VolumeSlider");
        CreateLabel(appSettingsPanel.transform, "VolumeValueLabel", "100%");
        GetOrAddComponent<AppSettingsPanelView>(appSettingsPanel.gameObject);

        CreateButton(panel.transform, "PlayButton", "Play");
        CreateButton(panel.transform, "SignOutButton", "Sign Out");

        return GetOrAddComponent<MainMenuPanelView>(panel.gameObject);
    }

    private static MenuFeedbackController CreateFeedbackPanel(Transform parent)
    {
        var panel = GetOrCreateUiChild(parent, "FeedbackPanel");
        var rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.sizeDelta = new Vector2(900f, 80f);
        rectTransform.anchoredPosition = new Vector2(0f, 30f);

        var image = GetOrAddComponent<Image>(panel.gameObject);
        image.color = new Color(0f, 0f, 0f, 0.65f);

        var label = CreateLabel(panel.transform, "MessageLabel", string.Empty);
        var labelRect = label.GetComponent<RectTransform>();
        Stretch(labelRect, new Vector2(20f, 10f), new Vector2(-20f, -10f));
        label.alignment = TextAlignmentOptions.Center;

        var controller = GetOrAddComponent<MenuFeedbackController>(panel.gameObject);
        panel.gameObject.SetActive(false);
        return controller;
    }

    private static SceneCardView EnsureSceneCardTemplate(Transform cardContainer)
    {
        var template = GetOrCreateUiChild(cardContainer, "SceneCardTemplate");
        template.gameObject.SetActive(false);
        var layoutElement = GetOrAddComponent<LayoutElement>(template.gameObject);
        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 180f;
        EnsureVerticalLayout(template.gameObject);

        CreateImage(template.transform, "PreviewImage", new Vector2(180f, 100f));
        CreateLabel(template.transform, "TitleLabel", "Scene");

        var indicator = GetOrCreateUiChild(template.transform, "SelectedIndicator");
        var indicatorImage = GetOrAddComponent<Image>(indicator.gameObject);
        indicatorImage.color = Color.green;
        var indicatorLayout = GetOrAddComponent<LayoutElement>(indicator.gameObject);
        indicatorLayout.preferredHeight = 10f;

        var button = GetOrAddComponent<Button>(template.gameObject);
        var background = GetOrAddComponent<Image>(template.gameObject);
        background.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        button.targetGraphic = background;

        var cardView = GetOrAddComponent<SceneCardView>(template.gameObject);
        SetObjectReference(cardView, "selectButton", button);
        SetObjectReference(cardView, "previewImage", template.transform.Find("PreviewImage").GetComponent<Image>());
        SetObjectReference(cardView, "titleLabel", template.transform.Find("TitleLabel").GetComponent<TextMeshProUGUI>());
        SetObjectReference(cardView, "selectedIndicator", indicator.gameObject);
        return cardView;
    }

    private static EventSystem EnsureEventSystem()
    {
        return EnsureEventSystem(false, null);
    }

    private static EventSystem EnsureEventSystem(bool preferOverlayUi, GameObject canvasRoot)
    {
        var eventSystem = FindObjectOfType<EventSystem>(true);
        if (eventSystem == null)
        {
            eventSystem = CreatePlainSceneObject("EventSystem", null).AddComponent<EventSystem>();
        }

        var standalone = eventSystem.GetComponent<StandaloneInputModule>();
        var ovrInputModule = eventSystem.GetComponent<OVRInputModule>();

        if (preferOverlayUi)
        {
            if (ovrInputModule == null)
            {
                ovrInputModule = GetOrAddComponent<OVRInputModule>(eventSystem.gameObject);
            }

            if (standalone != null)
            {
                Undo.DestroyObjectImmediate(standalone);
            }

            var rayTransform = FindPreferredUiRayTransform();
            if (rayTransform != null)
            {
                SetObjectReference(ovrInputModule, "rayTransform", rayTransform);
            }
        }
        else if (standalone == null)
        {
            GetOrAddComponent<StandaloneInputModule>(eventSystem.gameObject);
        }

        if (preferOverlayUi && canvasRoot != null)
        {
            var raycaster = canvasRoot.GetComponent<OVRRaycaster>();
            if (raycaster != null)
            {
                SetObjectReference(raycaster, "pointer", eventSystem.gameObject);
            }
        }

        return eventSystem;
    }

    private static GameObject GetOrCreateCanvas(Transform parent, string name, bool preferOverlayUi)
    {
        var child = FindDirectChild(parent, name);
        var canvasObject = child != null ? child.gameObject : CreateSceneObject(name, parent);
        var rectTransform = GetOrAddComponent<RectTransform>(canvasObject);
        var canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = FindPreferredUiCamera();
        rectTransform.sizeDelta = new Vector2(1600f, 900f);
        rectTransform.localScale = Vector3.one * 0.001f;
        rectTransform.localPosition = new Vector3(0f, 1.4f, 2f);
        rectTransform.localRotation = Quaternion.identity;

        var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);

        if (preferOverlayUi)
        {
            UpgradeCanvasToOverlay(canvasObject);
        }
        else
        {
            GetOrAddComponent<GraphicRaycaster>(canvasObject);
        }

        return canvasObject;
    }

    private static void UpgradeCanvasToOverlay(GameObject canvasObject)
    {
        var overlayCanvas = GetOrAddComponent<OVROverlayCanvas>(canvasObject);
        var rectTransform = canvasObject.GetComponent<RectTransform>();
        SetObjectReference(overlayCanvas, "rectTransform", rectTransform);
        SetSerializedInt(overlayCanvas, "compositionMode", (int)OVROverlayCanvas.CompositionMode.DepthTested);
        SetSerializedInt(overlayCanvas, "opacity", (int)OVROverlayCanvas.DrawMode.OpaqueWithClip);
        SetSerializedInt(overlayCanvas, "shape", (int)OVROverlayCanvas.CanvasShape.Curved);
        SetSerializedFloat(overlayCanvas, "curveRadius", 1.8f);
        SetSerializedBool(overlayCanvas, "superSample", true);
        SetSerializedInt(overlayCanvas, "_mipmapMode", 1);
        SetSerializedBool(overlayCanvas, "_dynamicResolution", true);

        var graphicRaycaster = canvasObject.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster != null && !(graphicRaycaster is OVRRaycaster))
        {
            Undo.DestroyObjectImmediate(graphicRaycaster);
        }

        GetOrAddComponent<OVRRaycaster>(canvasObject);
        AttachOverlayTextTracking(canvasObject, overlayCanvas);
    }

    private static GameObject CreateSceneObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static GameObject CreatePlainSceneObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        var child = FindDirectChild(parent, name);
        return child != null ? child.gameObject : CreateSceneObject(name, parent);
    }

    private static GameObject GetOrCreateUiChild(Transform parent, string name)
    {
        var child = FindDirectChild(parent, name);
        return child != null ? child.gameObject : CreateSceneObject(name, parent);
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        var child = parent.Find(name);
        return child;
    }

    private static GameObject FindSceneRoot(string name)
    {
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        for (var i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i].name == name)
            {
                return rootObjects[i];
            }
        }

        return null;
    }

    private static void EnsureVerticalLayout(GameObject gameObject)
    {
        var layout = GetOrAddComponent<VerticalLayoutGroup>(gameObject);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = GetOrAddComponent<ContentSizeFitter>(gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private static void EnsureHorizontalLayout(GameObject gameObject)
    {
        var layout = GetOrAddComponent<HorizontalLayoutGroup>(gameObject);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = GetOrAddComponent<ContentSizeFitter>(gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static Button CreateButton(Transform parent, string name, string labelText)
    {
        var buttonObject = GetOrCreateUiChild(parent, name);
        var image = GetOrAddComponent<Image>(buttonObject);
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        var button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = image;

        var layoutElement = GetOrAddComponent<LayoutElement>(buttonObject);
        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 56f;

        var label = GetOrCreateUiChild(buttonObject.transform, "Label");
        var text = GetOrAddComponent<TextMeshProUGUI>(label);
        text.text = labelText;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        Stretch(label.GetComponent<RectTransform>());
        return button;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string textValue)
    {
        var labelObject = GetOrCreateUiChild(parent, name);
        var text = GetOrAddComponent<TextMeshProUGUI>(labelObject);
        text.text = textValue;
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        var layoutElement = GetOrAddComponent<LayoutElement>(labelObject);
        layoutElement.preferredWidth = 600f;
        layoutElement.preferredHeight = 44f;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 size)
    {
        var imageObject = GetOrCreateUiChild(parent, name);
        var image = GetOrAddComponent<Image>(imageObject);
        image.color = new Color(0.35f, 0.35f, 0.35f, 0.95f);

        var layoutElement = GetOrAddComponent<LayoutElement>(imageObject);
        layoutElement.preferredWidth = size.x;
        layoutElement.preferredHeight = size.y;
        return image;
    }

    private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText, bool isPassword)
    {
        var inputObject = GetOrCreateUiChild(parent, name);
        var image = GetOrAddComponent<Image>(inputObject);
        image.color = Color.white;

        var layoutElement = GetOrAddComponent<LayoutElement>(inputObject);
        layoutElement.preferredWidth = 500f;
        layoutElement.preferredHeight = 56f;

        var inputField = GetOrAddComponent<TMP_InputField>(inputObject);

        var textArea = GetOrCreateUiChild(inputObject.transform, "Text Area");
        Stretch(textArea.GetComponent<RectTransform>(), new Vector2(12f, 6f), new Vector2(-12f, -6f));

        var textObject = GetOrCreateUiChild(textArea.transform, "Text");
        var text = GetOrAddComponent<TextMeshProUGUI>(textObject);
        text.fontSize = 24f;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        Stretch(textObject.GetComponent<RectTransform>());

        var placeholderObject = GetOrCreateUiChild(textArea.transform, "Placeholder");
        var placeholder = GetOrAddComponent<TextMeshProUGUI>(placeholderObject);
        placeholder.text = placeholderText;
        placeholder.fontSize = 24f;
        placeholder.color = new Color(0f, 0f, 0f, 0.45f);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.raycastTarget = false;
        Stretch(placeholderObject.GetComponent<RectTransform>());

        inputField.textViewport = textArea.GetComponent<RectTransform>();
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        GetOrAddComponent<TmpInputFieldFocusHandler>(inputObject);
        return inputField;
    }

    private static Slider CreateSlider(Transform parent, string name)
    {
        var sliderObject = GetOrCreateUiChild(parent, name);
        var layoutElement = GetOrAddComponent<LayoutElement>(sliderObject);
        layoutElement.preferredWidth = 420f;
        layoutElement.preferredHeight = 32f;

        var slider = GetOrAddComponent<Slider>(sliderObject);
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var background = GetOrCreateUiChild(sliderObject.transform, "Background");
        var backgroundImage = GetOrAddComponent<Image>(background);
        backgroundImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        Stretch(background.GetComponent<RectTransform>(), new Vector2(0f, 8f), new Vector2(0f, -8f));

        var fillArea = GetOrCreateUiChild(sliderObject.transform, "Fill Area");
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(10f, 8f), new Vector2(-10f, -8f));
        var fill = GetOrCreateUiChild(fillArea.transform, "Fill");
        var fillImage = GetOrAddComponent<Image>(fill);
        fillImage.color = new Color(0.16f, 0.7f, 0.35f, 1f);
        Stretch(fill.GetComponent<RectTransform>());

        var handleSlideArea = GetOrCreateUiChild(sliderObject.transform, "Handle Slide Area");
        Stretch(handleSlideArea.GetComponent<RectTransform>());
        var handle = GetOrCreateUiChild(handleSlideArea.transform, "Handle");
        var handleImage = GetOrAddComponent<Image>(handle);
        handleImage.color = Color.white;
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 36f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static void AttachOverlayTextTracking(GameObject canvasObject, OVROverlayCanvas overlayCanvas)
    {
        if (canvasObject == null || overlayCanvas == null)
        {
            return;
        }

        var textElements = canvasObject.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < textElements.Length; i++)
        {
            if (textElements[i] == null)
            {
                continue;
            }

            var tracker = GetOrAddComponent<OVROverlayCanvas_TMPChanged>(textElements[i].gameObject);
            SetObjectReference(tracker, "TargetCanvas", overlayCanvas);
        }
    }

    private static Camera FindPreferredUiCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return Object.FindObjectOfType<Camera>(true);
    }

    private static Transform FindPreferredUiRayTransform()
    {
        var camera = FindPreferredUiCamera();
        return camera != null ? camera.transform : null;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        Stretch(rectTransform, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, "Wire " + propertyName);
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedBool(Object target, string propertyName, bool value)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, "Set " + propertyName);
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedInt(Object target, string propertyName, int value)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, "Set " + propertyName);
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = value;
            }
            else
            {
                property.intValue = value;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedFloat(Object target, string propertyName, float value)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, "Set " + propertyName);
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedString(Object target, string propertyName, string value)
    {
        if (target == null)
        {
            return;
        }

        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var normalized = folderPath.Replace("\\", "/");
        var parts = normalized.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string CombineAssetPath(string folder, string fileName)
    {
        return Path.Combine(folder, fileName).Replace("\\", "/");
    }
}
