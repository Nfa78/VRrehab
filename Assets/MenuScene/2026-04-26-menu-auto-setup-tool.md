# Menu Scene Auto Setup Tool

Date: 2026-04-26

## Purpose

This document describes the editor tool added to automate the initial `MenuScene` scaffold setup.

The tool creates a default scene hierarchy, adds the menu runtime components, wires the known references, creates placeholder menu assets, and then runs the setup verifier.

The preferred generated VR UI path is:

- `OVROverlayCanvas`
- `OVRRaycaster`
- `OVRInputModule`

## Tool Entry Points

You can open the tool in two ways:

### 1. Main menu

- `Tools > VR Stroke Rehab > Menu Scene > Auto Setup`

### 2. Verifier inspector

- select the object with `MenuSceneSetupVerifier`
- click `Open Auto Setup Tool`

## Implementation Files

### Editor window

- `Assets/MenuScene/Scripts/Editor/MenuSceneAutoSetupWindow.cs`

### Verifier inspector integration

- `Assets/MenuScene/Scripts/Editor/MenuSceneSetupVerifierEditor.cs`

## What The Tool Creates

When you press `Create Or Update Menu Scaffold`, the tool creates or reuses the following:

### Scene roots

- `MenuRoot`
- `MenuRoot/Systems`
- `MenuRoot/MenuCanvas`
- `MenuRoot/MenuCanvas/Panels`
- `MenuRoot/MenuSceneSetupVerifier`

### UI panels

- `EntryPanel`
- `LoginPanel`
- `SignupPanel`
- `MainMenuPanel`
- `FeedbackPanel`

### Main menu sub-sections

Inside `MainMenuPanel`, the tool creates:

- `SceneCarouselPanel`
- `SceneDetailsPanel`
- `SceneConfigurationPanel`
- `AppSettingsPanel`
- `PlayButton`
- `SignOutButton`

### Scene carousel internals

Inside `SceneCarouselPanel`, the tool creates:

- `PreviousButton`
- `CardContainer`
- `NextButton`

Inside `CardContainer`, the tool creates:

- `SceneCardTemplate`

This template is used by `SceneCarouselController` as the runtime card source.

### Event system

If missing, the tool creates:

- `EventSystem`
- `OVRInputModule` when overlay mode is preferred
- `StandaloneInputModule` on the non-overlay fallback path

This matches the current preferred VR interaction setup.

### Runtime controllers

Under `MenuRoot/Systems`, the tool creates or reuses:

- `AdaptiveApiClient` when the tool option allows it and none exists in the scene
- `SceneLauncher`
- `MenuAuthController`
- `SceneConfigurationController`
- `AppSettingsController`
- `SceneCarouselController`
- `MenuFlowController`

### Panel views and feedback

The tool also adds or reuses:

- `EntryPanelView`
- `LoginPanelView`
- `SignupPanelView`
- `MainMenuPanelView`
- `SceneDetailsPanelView`
- `SceneConfigurationPanelView`
- `AppSettingsPanelView`
- `MenuFeedbackController`
- `MenuSceneSetupVerifier`

## What The Tool Wires Automatically

The tool sets the serialized references between the current runtime components.

### `MenuFlowController`

It wires:

- entry panel
- login panel
- signup panel
- main menu panel
- scene carousel controller
- scene configuration controller
- app settings controller
- auth controller
- scene launcher
- feedback controller
- `startAuthenticatedForDebug`

### `MenuAuthController`

It wires:

- `AdaptiveApiClient`
- `LoginPanelView`
- `SignupPanelView`
- `MenuFeedbackController`

### `SceneCarouselController`

It wires:

- `SceneMenuCatalog`
- card container
- `SceneCardView` template
- previous button
- next button
- `SceneDetailsPanelView`

### `SceneConfigurationController`

It wires:

- `SceneConfigurationPanelView`

### `AppSettingsController`

It wires:

- `AppSettingsPanelView`

### Panel views

The tool wires the button/input/text/image references for:

- entry panel
- login panel
- signup panel
- main menu panel
- scene details panel
- hand-used panel
- app settings panel
- feedback panel

### Verifier

It also wires the central references on `MenuSceneSetupVerifier` so validation can run immediately.

## Placeholder Assets Created

If `Create Sample Catalog` is enabled, the tool creates:

- `Assets/MenuScene/ScriptableObjects/Generated/SceneMenuCatalog_Generated.asset`
- `Assets/MenuScene/ScriptableObjects/Generated/SceneMenuItem_Generated.asset`

The generated scene item is intentionally a placeholder and should be replaced or edited with real values.

## Tool Options

The editor window currently exposes these options:

### `Root Name`

Name of the root GameObject to create or reuse.

Default:

- `MenuRoot`

### `Generated Assets Folder`

Folder used for the generated sample catalog assets.

Default:

- `Assets/MenuScene/ScriptableObjects/Generated`

### `Create Sample Catalog`

If enabled:

- creates or updates a generated scene catalog asset
- creates or updates one generated placeholder scene item

### `Create API Client If Missing`

If enabled:

- creates an `AdaptiveApiClient` in the scene when none is found

### `Prefer OVR Overlay Canvas`

If enabled:

- upgrades or creates the main menu canvas with `OVROverlayCanvas`
- uses `OVRRaycaster` on the canvas
- prefers `OVRInputModule` on the `EventSystem`
- attaches `OVROverlayCanvas_TMPChanged` to generated TMP text elements

This is the recommended option for this VR project and is enabled by default.

### `Start Authenticated For Debug`

If enabled:

- writes `true` into `MenuFlowController.startAuthenticatedForDebug`

This is useful when you want to test the carousel, hand-used selection, volume slider, and play flow without completing auth first.

### `Select Created Root`

If enabled:

- selects the created or reused `MenuRoot` after setup completes

## Automatic Validation Behavior

At the end of scaffold creation, the tool runs:

- `MenuSceneSetupVerifier.ValidateAndLog()`

This gives immediate feedback on:

- missing references
- missing world-space canvas support
- missing event system
- missing preferred `OVROverlayCanvas` / `OVRRaycaster` / `OVRInputModule` setup
- missing or incomplete API configuration
- empty generated scene catalog

## What Still Requires Manual Work

The tool reduces setup time, but several project-specific things still need human decisions.

### API configuration

You still need to configure:

- `AdaptiveApiClient.PublishableKey`
- auth base URL if different from the current default
- API base URL if different from the current default

### Scene content

You still need to:

- replace the generated placeholder scene item with real scenes
- assign real preview images
- set real scene ids, titles, descriptions, and load references
- ensure target scenes are loadable through Unity scene loading

### VR UX and layout polish

You still need to adjust:

- canvas position and scale for the headset/environment
- panel spacing and sizing
- button sizing
- typography
- scene-card visual design
- final section layout

### XR input choice

The preferred generated path is:

- `OVROverlayCanvas`
- `OVRRaycaster`
- `OVRInputModule`

You may still need to manually assign the final pointer source for `OVRInputModule.rayTransform` depending on the camera rig and interaction rig used in the scene.

## Recommended Usage

### First-time setup

1. Open `MenuScene.unity`.
2. Open the auto-setup tool.
3. Keep `Create Sample Catalog` enabled.
4. Optionally enable `Start Authenticated For Debug`.
5. Click `Create Or Update Menu Scaffold`.
6. Inspect the generated hierarchy.
7. Read the verifier output in the Console.
8. Fix remaining manual items.

### Quick runtime smoke test

1. Enable `Start Authenticated For Debug`.
2. Run the auto-setup tool.
3. Enter Play mode.
4. Verify:
   - main menu opens
   - carousel shows the placeholder item
   - hand-used selection updates
   - volume slider updates app volume
   - play button blocks launch until a real scene reference is configured

### Auth flow test

1. Disable `Start Authenticated For Debug`.
2. Configure the `AdaptiveApiClient`.
3. Enter Play mode.
4. Verify:
   - entry panel appears
   - login and signup navigation works
   - validation errors are shown for bad input
   - successful auth transitions to the main menu

## Limitations Of The Current Tool

- it creates a functional scaffold, not a final visual design
- it generates a scene card template in-scene rather than a prefab asset
- it creates a placeholder catalog item, not a real production scene list
- it cannot fully infer the final project-specific VR pointer source for `OVRInputModule.rayTransform`
- it does not modify build settings or add target scenes automatically
- it does not create preview artwork

## Next Practical Step

Use the tool once to scaffold the scene, then replace the generated placeholder content with real scene data and adjust the world-space layout for the actual VR menu experience.
