# Menu Scene Scaffold Implementation

Date: 2026-04-26

## Purpose

This document describes the current implementation scaffold created under `Assets/MenuScene`.

It focuses on:

- runtime structure
- separation of concerns
- how controllers, views, and data assets connect
- what is already implemented in code
- what still needs manual scene wiring in Unity

## Current Folder Structure

```text
Assets/MenuScene/
  MenuScene.unity
  2026-04-26-vr-menu-design-brief.md
  2026-04-26-menu-scaffold-implementation.md
  MenuAssets/
  Scripts/
    Editor/
      MenuSceneSetupVerifierEditor.cs
    Runtime/
      Auth/
        MenuAuthController.cs
      Core/
        MenuContext.cs
        MenuFlowController.cs
        MenuLaunchContext.cs
        MenuState.cs
      Data/
        AppSettingsModel.cs
        SceneMenuCatalog.cs
        SceneMenuItemData.cs
        SceneSessionConfiguration.cs
      SceneSelection/
        SceneCardView.cs
        SceneCarouselController.cs
        SceneConfigurationController.cs
        SceneLauncher.cs
      Settings/
        AppSettingsController.cs
      UI/
        Common/
          MenuFeedbackController.cs
        Panels/
          AppSettingsPanelView.cs
          EntryPanelView.cs
          LoginPanelView.cs
          MainMenuPanelView.cs
          SceneConfigurationPanelView.cs
          SceneDetailsPanelView.cs
          SignupPanelView.cs
      Validation/
        MenuSceneSetupVerifier.cs
        MenuValidationIssue.cs
```

## Architecture Summary

The scaffold is split into four practical layers:

- `UI`:
  thin panel/view MonoBehaviours that own buttons, input fields, labels, and display updates
- `Controllers`:
  flow and feature behavior such as auth, scene selection, app settings, and launching
- `Data`:
  plain menu models and ScriptableObject assets used to drive the menu
- `Validation`:
  central scene setup verification for manual inspector wiring

This keeps the UI layer simple and prevents one large menu script from owning auth, carousel state, settings, and scene loading all at once.

## Core Runtime Flow

### 1. Menu state

`MenuState.cs` defines the main high-level states:

- `Entry`
- `Login`
- `SignUp`
- `MainMenu`

### 2. Central orchestrator

`MenuFlowController.cs` is the main coordinator.

It owns references to:

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

Its responsibilities are:

- switch visible panels based on `MenuState`
- react to `Login` and `Sign Up` entry choices
- move from auth flow to authenticated menu flow
- track the selected scene in the shared menu context
- collect final scene configuration and app settings before launch
- sign out and reset the flow back to the entry state

### 3. Shared runtime context

`MenuContext.cs` stores the current runtime state for the menu:

- whether the user is authenticated
- authenticated email
- selected scene
- scene/session configuration
- app settings snapshot

`MenuLaunchContext.cs` is a static handoff object used when starting the selected scene. It captures:

- authenticated email
- selected scene id
- selected scene title
- selected scene load reference
- selected hand used
- master volume

This allows the next scene to inspect launch choices after `SceneManager.LoadScene(...)`.

## Preferred VR UI Path

The current preferred rendering and interaction path for the menu is:

- `OVROverlayCanvas`
- `OVRRaycaster`
- `OVRInputModule`

This is preferred over a generic world-space canvas path because this project is VR-specific and menu readability matters for auth forms, scene cards, and settings controls.

## Auth Layer

### `MenuAuthController.cs`

This is the auth behavior controller.

It is wired to:

- `AdaptiveApiClient`
- `LoginPanelView`
- `SignupPanelView`
- `MenuFeedbackController`

It currently handles:

- login form submission
- signup form submission
- basic validation for empty email/password
- signup password confirmation matching
- API calls through `AdaptiveApiClient.SignInAsync(...)`
- API calls through `AdaptiveApiClient.SignUpAsync(...)`
- auth success propagation back to `MenuFlowController`
- sign-out by clearing the `AdaptiveApiClient` auth session

Important behavior:

- if signup returns a session, the flow moves directly to the authenticated menu
- if signup succeeds but does not return a session, the login screen is shown with the email prefilled

## Scene Selection Layer

### `SceneMenuItemData.cs`

ScriptableObject representing one scene in the menu.

It contains:

- `sceneId`
- `sceneTitle`
- `sceneName`
- `scenePath`
- `shortDescription`
- `previewImage`

The load reference is resolved from:

- `scenePath` if present
- otherwise `sceneName`

### `SceneMenuCatalog.cs`

ScriptableObject collection of `SceneMenuItemData` assets.

This is the data source for the carousel.

### `SceneCarouselController.cs`

This controller owns the current scene selection behavior.

It is wired to:

- `SceneMenuCatalog`
- card container transform
- `SceneCardView` prefab
- previous button
- next button
- `SceneDetailsPanelView`

Responsibilities:

- spawn one scene card instance per catalog item
- subscribe to card selection events
- move selection forward/backward
- keep one selected scene index
- update the details panel
- emit `SelectionChanged`

### `SceneCardView.cs`

This is the visual/interactive unit for one scene card.

It owns:

- card button
- preview image
- title label
- optional selected indicator

It binds one `SceneMenuItemData` item and raises selection events to the carousel controller.

### `SceneDetailsPanelView.cs`

Displays the currently selected scene:

- preview image
- title
- short description

## Scene Session Configuration Layer

### `SceneSessionConfiguration.cs`

This model stores session-specific menu choices for the next play session.

Currently implemented:

- `handUsed`

### `SceneConfigurationPanelView.cs`

This is the UI layer for choosing `Hand Used`.

It owns:

- left hand button
- right hand button
- selected hand label

### `SceneConfigurationController.cs`

This controller owns the `Hand Used` session configuration.

Responsibilities:

- hold the current `SceneSessionConfiguration`
- initialize a default hand
- update the view
- react to left/right hand selections
- expose a configuration snapshot to the launch flow

Important modeling decision:

- `Hand Used` is treated as scene/session configuration
- it is not treated as a general app/software setting

## App Settings Layer

### `AppSettingsModel.cs`

This model stores application-level settings.

Currently implemented:

- `masterVolume`

### `AppSettingsPanelView.cs`

This is the UI layer for app settings.

It currently owns:

- volume slider
- volume value label

### `AppSettingsController.cs`

This controller manages software-level settings.

Responsibilities:

- load saved volume from `PlayerPrefs`
- apply current volume to `AudioListener.volume`
- update the volume view
- persist volume changes back to `PlayerPrefs`
- expose a settings snapshot to the launch flow

Important modeling decision:

- audio volume belongs to software/app settings
- it is separate from the scene/session choices

## Main Panel Views

### `EntryPanelView.cs`

Owns:

- `Login` button
- `Sign Up` button

Raises:

- `LoginSelected`
- `SignUpSelected`

### `LoginPanelView.cs`

Owns:

- email input
- password input
- submit button
- back button
- optional status label

Raises:

- `SubmitRequested(email, password)`
- `BackRequested`

### `SignupPanelView.cs`

Owns:

- email input
- password input
- confirm password input
- submit button
- back button
- optional status label

Raises:

- `SubmitRequested(email, password, confirmPassword)`
- `BackRequested`

### `MainMenuPanelView.cs`

Owns:

- play button
- sign out button
- optional authenticated user label

Raises:

- `PlayRequested`
- `SignOutRequested`

## Feedback Layer

### `MenuFeedbackController.cs`

Central feedback helper for status messages.

It supports:

- info
- success
- warning
- error

It is used by auth and launch flow to show:

- loading-like progress text
- validation failures
- auth errors
- scene launch errors

## Launch Layer

### `SceneLauncher.cs`

This is the scene transition boundary.

Responsibilities:

- verify that a scene is selected
- verify that the selected scene has a load reference
- capture the current menu context into `MenuLaunchContext`
- call `SceneManager.LoadScene(...)`

The launcher currently assumes the resolved scene reference is loadable by Unity.

## Validation Layer

### `MenuValidationIssue.cs`

Defines the validation issue shape:

- severity
- source
- message

Severity values:

- `Warning`
- `Error`

### `MenuSceneSetupVerifier.cs`

This is the central manual wiring validator for the menu scene.

It checks:

- required controllers exist
- required panels exist
- required inspector references are assigned within controllers/views
- cross-wiring matches expected references
- scene catalog presence and emptiness
- `AdaptiveApiClient` presence and key/base URL setup
- `EventSystem` presence
- presence of a recognized UI input module
- preferred presence of `OVRInputModule`
- whether `OVRInputModule.rayTransform` is assigned
- `Canvas` presence
- presence of a world-space canvas
- presence of a `GraphicRaycaster`
- preferred presence of `OVROverlayCanvas`
- preferred presence of `OVRRaycaster`

It supports:

- `Reset()` auto-assignment attempts
- `Start()` validation logging in development builds
- a context-menu validation command

### `MenuSceneSetupVerifierEditor.cs`

Adds an inspector button:

- `Validate Menu Scene Setup`

This allows manual validation from the Unity inspector without entering play mode.

## How The Pieces Connect

### Entry to auth

1. `EntryPanelView` raises `LoginSelected` or `SignUpSelected`.
2. `MenuFlowController` receives the event.
3. `MenuFlowController` switches the state to `Login` or `SignUp`.

### Login path

1. `LoginPanelView` raises `SubmitRequested(email, password)`.
2. `MenuAuthController` validates the inputs.
3. `MenuAuthController` calls `AdaptiveApiClient.SignInAsync(...)`.
4. On success, `MenuAuthController` raises `Authenticated(email)`.
5. `MenuFlowController` receives that event and switches to `MainMenu`.

### Signup path

1. `SignupPanelView` raises `SubmitRequested(email, password, confirmPassword)`.
2. `MenuAuthController` validates the inputs and password match.
3. `MenuAuthController` calls `AdaptiveApiClient.SignUpAsync(...)`.
4. If a session is returned, auth is considered complete and `Authenticated(email)` is raised.
5. If only account creation succeeds, `SignUpCompletedRequiresLogin(email)` is raised and the flow returns to login with email prefilled.

### Scene selection path

1. `SceneCarouselController` spawns cards from `SceneMenuCatalog`.
2. Each `SceneCardView` binds a `SceneMenuItemData`.
3. Card selection updates the carousel's selected index.
4. `SceneDetailsPanelView` is refreshed.
5. `SceneCarouselController` raises `SelectionChanged`.
6. `MenuFlowController` stores the selected scene in `MenuContext`.

### Session configuration path

1. `SceneConfigurationPanelView` raises `HandSelected`.
2. `SceneConfigurationController` updates `SceneSessionConfiguration`.
3. `MenuFlowController` reads a configuration snapshot during `Play`.

### App settings path

1. `AppSettingsPanelView` raises `VolumeChanged`.
2. `AppSettingsController` updates `AppSettingsModel`.
3. `AppSettingsController` applies the volume to `AudioListener`.
4. `AppSettingsController` saves the volume with `PlayerPrefs`.
5. `MenuFlowController` reads a settings snapshot during `Play`.

### Launch path

1. `MainMenuPanelView` raises `PlayRequested`.
2. `MenuFlowController` gathers:
   - selected scene
   - scene configuration
   - app settings
3. `SceneLauncher` validates launch readiness.
4. `SceneLauncher` stores values into `MenuLaunchContext`.
5. `SceneLauncher` loads the selected Unity scene.

## Manual Scene Wiring Still Required

The code scaffold exists, but the Unity scene still needs manual setup.

Required wiring steps include:

- add the controllers to appropriate GameObjects in `MenuScene.unity`
- add the panel view scripts to the correct UI objects
- assign button/input/text/image references in all panel views
- assign `AdaptiveApiClient` to `MenuAuthController`
- create and assign a `SceneMenuCatalog`
- create scene item assets for the carousel
- create and assign a `SceneCardView` prefab
- assign the carousel card container and navigation buttons
- assign `SceneDetailsPanelView`
- assign left/right hand buttons and selected-hand label
- assign the volume slider and value label
- assign the feedback label/root
- assign `SceneLauncher`
- confirm the overlay canvas has the intended camera/reference setup
- confirm `OVRInputModule.rayTransform` points at the intended VR pointer/camera transform
- add `MenuSceneSetupVerifier` to the scene root and run validation

## Current Limitations

- the auto-setup tool creates and wires a first scaffold, but final visual layout still needs manual adjustment
- no patient/profile setup flow exists after auth
- launch currently assumes the target scene is already present in build settings or otherwise loadable
- compile validation from the terminal was inconclusive because `dotnet build` returned a non-zero shell exit code even though it reported `0 Warning(s)` and `0 Error(s)`

## Recommended Next Step

The next practical step is to wire the actual UI hierarchy in `MenuScene.unity`, assign all inspector references, create the scene catalog assets, and run `MenuSceneSetupVerifier` until the scene reports no blocking errors.
