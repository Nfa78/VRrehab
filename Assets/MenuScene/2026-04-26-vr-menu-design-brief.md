# VR Menu Design Brief

Date: 2026-04-26

## Goal

Design the initial VR menu for `Assets/MenuScene/MenuScene.unity`.

The menu should:

- first show two entry options: `Login` and `Sign Up`
- open the corresponding authentication form when one option is selected
- transition to the authenticated menu after successful authentication
- show a scene-selection carousel with one card per available scene
- show personal settings controls below the carousel

Each scene card should contain:

- preview image
- scene title

## Intended User Flow

1. User enters the menu scene.
2. The default panel shows `Login` and `Sign Up`.
3. Selecting `Login` opens a login form.
4. Selecting `Sign Up` opens a signup form.
5. User submits credentials.
6. On successful authentication, auth panels are hidden.
7. The main selection panel is shown.
8. User browses the scene carousel.
9. User selects the hand used for the upcoming scene session.
10. User optionally adjusts separate app/software settings.
11. User confirms a scene and proceeds.

## Menu States

### 1. Entry State

- simple welcome header
- `Login` button
- `Sign Up` button

### 2. Login State

- email field
- password field
- submit button
- back button
- error/status message area

### 3. Sign Up State

- email field
- password field
- confirm password field
- submit button
- back button
- error/status message area

### 4. Authenticated State

- scene carousel
- selected scene details
- hand-used selector below the scene area
- start/continue button
- separate app settings section
- sign out button

## Proposed Scene UI Structure

Suggested hierarchy inside `MenuScene`:

- `MenuRoot`
- `XRUIRigAnchor`
- `MenuStateController`
- `Panels`
- `EntryPanel`
- `LoginPanel`
- `SignupPanel`
- `MainMenuPanel`
- `SceneCarouselPanel`
- `SceneDetailsPanel`
- `SceneConfigurationPanel`
- `AppSettingsPanel`
- `FeedbackPanel`

## Proposed Prefabs

- `MenuRoot.prefab`
- `AuthChoicePanel.prefab`
- `LoginFormPanel.prefab`
- `SignupFormPanel.prefab`
- `SceneCarouselPanel.prefab`
- `SceneCard.prefab`
- `SceneConfigurationPanel.prefab`
- `AppSettingsPanel.prefab`
- `StatusToast.prefab`

## Proposed Scripts

### Core Flow

- `MenuFlowController`
  - owns current menu state
  - switches panels
  - handles authenticated transition

- `MenuAuthController`
  - submits login/signup requests
  - receives success/error result
  - stores authenticated user session for menu flow

- `MenuSceneCatalog`
  - provides available scenes to display in the carousel
  - can be backed by ScriptableObjects

- `SceneConfigurationController`
  - owns session-specific options for the selected scene
  - manages `Hand Used` selection
  - exposes the final scene configuration to the launch flow

- `AppSettingsController`
  - manages software-level settings such as audio volume
  - applies and persists app-wide preferences separately from scene/session choices

- `MenuSceneSetupVerifier`
  - checks that required scene components are present
  - validates that required references are wired in the inspector
  - reports warnings for incomplete optional setup
  - reports errors for missing required setup
  - helps catch manual scene configuration mistakes before runtime

### UI Controllers

- `AuthChoicePanelController`
  - handles `Login` and `Sign Up` option selection

- `LoginFormController`
  - binds login inputs and submit action

- `SignupFormController`
  - binds signup inputs and validation

- `SceneCarouselController`
  - populates scene cards
  - tracks current selection
  - handles previous/next interactions

- `SceneCardView`
  - renders scene image and title
  - raises selection events

- `MenuFeedbackController`
  - shows loading, error, and success messages

## Proposed Data Assets

### Scene Definition

Create a ScriptableObject such as `SceneMenuItemData` with:

- `sceneId`
- `sceneTitle`
- `previewImage`
- `scenePath` or build index reference
- optional short description

### Personal Settings

Create a scene/session configuration model for values such as:

- hand used

Create a separate app settings model for values such as:

- audio volume
- subtitles or language
- comfort options if needed

## Required Unity Components

### VR UI Foundation

- world-space `Canvas`
- preferred VR rendering path: `OVROverlayCanvas`
- `EventSystem`
- preferred VR input path: `OVRInputModule`
- ray interactors or direct interactors for button/form interaction
- preferred VR raycaster: `OVRRaycaster`

### UI Elements

- buttons for auth choice and navigation
- TMP input fields for forms
- TMP text for titles, labels, and feedback
- image components for scene previews
- segmented buttons, toggles, or dropdowns for `Hand Used`
- sliders, toggles, or dropdowns for app settings
- scroll or paged layout support for the carousel

### Integration Points

- reuse `Assets/AdaptiveSystem/Api/AdaptiveApiClient.cs` for sign-in/sign-up calls
- define a post-auth handoff from API auth success to menu state change
- define scene-launch entry point after carousel confirmation
- add `MenuSceneSetupVerifier` to the scene root so manual assignments can be validated centrally

## Setup Verification Requirements

Create a `MenuSceneSetupVerifier` component that runs validation for manual scene setup.

It should verify:

- required root objects exist
- required panels are assigned
- auth controllers are linked to the correct form panels
- carousel controller has its card container, navigation controls, and data source assigned
- scene configuration controller has the `Hand Used` UI wired correctly
- app settings controller has required UI references assigned
- feedback/status UI is assigned
- `AdaptiveApiClient` reference is assigned when auth is enabled
- scene launch target or loader integration is assigned
- required XR UI interaction components are present in the scene

It should report results as:

- `Warning` for optional but recommended setup gaps
- `Error` for missing references or components that block correct behavior

Suggested usage:

- run in editor validation methods such as `OnValidate` or via a context-menu/manual check
- optionally run once at scene start in development builds
- log a grouped validation summary so scene setup issues are obvious to whoever wires the scene manually

## Suggested Folder Structure Under `Assets/MenuScene`

- `MenuScene.unity`
- `2026-04-26-vr-menu-design-brief.md`
- `Scripts/`
- `Scripts/MenuSceneSetupVerifier.cs`
- `Prefabs/`
- `ScriptableObjects/`
- `Sprites/`
- `Materials/`
- `UI/`

## Implementation Notes

- Keep auth UI and authenticated menu UI as separate panels rather than one overloaded canvas.
- Keep scene data separate from presentation so adding new scenes only requires creating another data asset.
- Treat the carousel selection as the single source of truth for the selected scene.
- Place `Hand Used` directly under the scene area and above the `Play` button because it belongs to the upcoming session configuration.
- Keep app/software settings in a separate section because they belong to the software experience, not the selected session itself.
- Include a loading state during auth requests to prevent duplicate submissions.
- Reserve a consistent feedback area for auth and scene-launch errors.
- Centralize scene setup validation in `MenuSceneSetupVerifier` instead of spreading null-checks across all UI scripts.

## Minimum First Implementation Slice

1. Build the panel structure and state switching.
2. Implement `Login` and `Sign Up` forms with placeholder validation.
3. Connect auth calls to the existing adaptive API client.
4. Add a local scene catalog with mock scene cards.
5. Build the carousel interaction.
6. Add `Hand Used` selection below the carousel.
7. Add a separate app settings section for audio volume and similar software settings.
8. Add scene start confirmation behavior.

## Open Decisions

- Which exact scenes should be exposed in the first carousel version.
- Which app/software settings are mandatory for MVP beyond audio volume.
- Whether signup should require extra patient/profile data immediately or only after auth.
- Whether scene selection should load directly or go through a confirmation panel.
