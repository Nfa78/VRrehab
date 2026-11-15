# Garden Dashboard UI Interaction — Required Next Steps

## Goal

Allow the player’s hand/controller pointer to interact with the world-space dashboard buttons in `Garden.unity`.

The dashboard already has working button callbacks in `SimDashboardGUI.cs`. The missing part is the input and raycast path that delivers hand/controller pointer events to the Canvas.

## Current setup

### MenuScene reference

`MenuScene.unity` uses the Oculus UI path:

- The `EventSystem` has an active `OVRInputModule`.
- `MenuCanvas` has an enabled `OVRRaycaster`.
- The input module and raycaster share a configured pointer/ray transform.
- An `OVRGazePointer` provides the pointer/cursor object.
- The `XRUIInputModule` is disabled.

The `PCM` object contains a `PointableCanvasModule`, but the object is inactive. It is therefore not the component currently making the MenuScene UI interactive.

### Garden dashboard

The dashboard Canvas currently has:

- World-space Canvas configuration.
- `OVROverlayCanvas`.
- `GraphicRaycaster`.
- `TrackedDeviceGraphicRaycaster`.

The Garden `EventSystem` currently has:

- An active `InputSystemUIInputModule` with configured Input System actions.
- An active `XRUIInputModule`, but its explicit action references are empty.

The Garden scene does not currently contain a complete serialized hand/controller UI ray path. Its `HandsResolver` fields are also unassigned.

## Recommended implementation: match MenuScene’s Oculus UI path

Use one UI input system consistently. The lowest-risk path is to mirror the working MenuScene setup.

## What MenuScene actually contains

Adding `OVRInputModule` by itself is not the complete MenuScene setup. The working MenuScene has these linked pieces:

| MenuScene object/component | Important setup | Garden equivalent |
| --- | --- | --- |
| `EventSystem > OVRInputModule` | Enabled; `rayTransform` points to `OVRCameraRig/TrackingSpace/CenterEyeAnchor`; `m_Cursor` points to the GameObject with `OVRGazePointer`; `editorMouseCamera` points to the same CenterEyeAnchor camera | Garden `EventSystem` |
| `EventSystem > MenuOpenXrPointer` | Enabled custom bridge; `leftHand = false` for the right controller; it registers the OpenXR aim pose and trigger with `OVRInputModule`; it can resolve `OVRCameraRig` automatically, but assigning the Garden rig is clearer | Add to Garden `EventSystem` |
| Optional second `MenuOpenXrPointer` | Duplicate the component with `leftHand = true` if left-controller input is required | Same Garden `EventSystem` |
| `MenuCanvas > OVRRaycaster` | Enabled; `pointer` references the same `OVRGazePointer` GameObject used by `OVRInputModule.m_Cursor` | Garden dashboard Canvas under `TasksGUI` |
| `MenuCanvas > OVROverlayCanvas` | Enabled for the MenuScene rendering setup | Garden dashboard Canvas already has this |
| `UILaserPointer > DrawPointerForUI` | Visualizes the authoritative OVR ray and can create a hit marker; it is useful for diagnosis but is not the component that submits the click | Optional Garden debug object |
| `EventSystem > MenuClickRayDebugger` | Logs OVR pointer data and raycast results; useful for diagnosis but not required for interaction | Optional Garden debug component |

The MenuScene `rayTransform` field points to `CenterEyeAnchor` as the fallback/editor camera reference. The actual controller ray is supplied by `MenuOpenXrPointer`, which creates a runtime `RightMenuAim` or `LeftMenuAim` transform and registers it with `OVRInputModule`. Therefore, copying only the `rayTransform` field is not enough for controller interaction.

The `OVRGazePointer` object is a `Cursor_Timer` prefab instance named `GazeIcon` in MenuScene with an `OVRGazePointer` component added to it. Its `rayTransform` also references `CenterEyeAnchor`. The Canvas `OVRRaycaster.pointer` references this `GazeIcon` GameObject.

Before comparing the saved files, save `Garden.unity` in the Unity Editor. The serialized Garden scene on disk currently does not contain the newly added `OVRInputModule`; unsaved Inspector changes cannot be verified from the scene file.

### 1. Configure the Garden EventSystem

On the Garden `EventSystem`:

1. Add or enable `OVRInputModule`.
2. Set `rayTransform` to `OVRCameraRig/TrackingSpace/CenterEyeAnchor` for the same fallback arrangement as MenuScene.
3. Set `editorMouseCamera` to the Camera component on that same `CenterEyeAnchor`.
4. Create or duplicate the MenuScene `GazeIcon`/`Cursor_Timer` pointer object and add `OVRGazePointer` to it if needed.
5. Set `OVRInputModule.m_Cursor` to that pointer GameObject/component.
6. Add `MenuOpenXrPointer` to the Garden `EventSystem`, set `leftHand = false`, and assign the Garden `OVRCameraRig` to `cameraRig`.
7. Add a second `MenuOpenXrPointer` with `leftHand = true` only if left-controller input is required.
8. Disable the existing `InputSystemUIInputModule` and `XRUIInputModule` while matching MenuScene.
9. Confirm that only the OVR module is active and responsible for submitting UI pointer events.

Do not leave both OVR and XRUI modules active while debugging. Competing modules can produce duplicate, missing, or inconsistent pointer events.

### 2. Configure the dashboard Canvas

On the dashboard Canvas under `TasksGUI`:

1. Add an enabled `OVRRaycaster`.
2. Assign its `pointer` field to the exact same `GazeIcon` GameObject used by `OVRInputModule.m_Cursor`.
3. Keep the Canvas in world-space mode.
4. Keep `OVROverlayCanvas` only if it is required for the target headset rendering mode.
5. Temporarily disable `GraphicRaycaster` and `TrackedDeviceGraphicRaycaster` while validating the OVR path, then re-enable only if another required UI path depends on them.
6. Confirm the Canvas is the dashboard Canvas under `TasksGUI`, not the other Garden Canvas.

The dashboard buttons should remain normal Unity UI `Button` components. Their existing listeners in `SimDashboardGUI.cs` do not need to be rewritten.

### 3. Choose and configure the pointer source

The pointer source must be an active transform that follows the user’s hand or controller. For the controller setup that matches MenuScene, the source is supplied by `MenuOpenXrPointer`, not by manually dragging a static scene object into `rayTransform`.

For hand-ray interaction, verify:

- The hand tracking objects are active in Play Mode.
- The ray origin follows the intended hand.
- The ray is visible while pointing at the dashboard.
- The pointer/cursor is not blocked by another ray or camera object.

If the project uses separate left and right hand rays, configure both according to the existing Oculus interaction setup rather than creating a second independent EventSystem.

For diagnosis, duplicate the MenuScene `UILaserPointer` object or create a GameObject with `DrawPointerForUI` and a `LineRenderer`. Set its visualization mode to the authoritative OVR input ray and enable its hit marker. This visualizes the ray but does not create the input source.

### 4. Verify Canvas and button prerequisites

Confirm the following on the dashboard:

- The Canvas is enabled and has a valid world-space transform.
- Each button has an enabled `Graphic` component, such as `Image` or `TextMeshProUGUI`.
- The buttons are not on an ignored layer for the raycaster.
- No transparent or overlay UI element is intercepting pointer events.
- The Canvas is within the pointer ray’s reachable distance and angle.
- The dashboard camera/render setup is compatible with the selected Canvas mode.

## Alternative: direct hand poke interaction

If the intended interaction is fingertip touch rather than a distant ray:

1. Use Meta Interaction SDK hand poke interactors.
2. Activate/configure `PointableCanvasModule` on the relevant EventSystem.
3. Ensure the hand poke interactor objects are active in Garden.
4. Configure the Canvas and UI elements for pointable interaction according to the Meta SDK version in the project.
5. Disable unrelated UI modules while testing.

The inactive `PCM` object in MenuScene is only a reference for component configuration; it is not evidence that MenuScene currently uses poke interaction.

## Recommended implementation order

1. Duplicate the known-working MenuScene pointer/ray relationship in Garden.
2. Add `OVRRaycaster` to the Garden dashboard Canvas.
3. Enable/configure `OVRInputModule` on the Garden EventSystem.
4. Disable the Garden `XRUIInputModule` during the first test.
5. Enter Play Mode and verify hover/highlight before testing clicks.
6. Test each button: pause, restart, and exit.
7. Test both hands/controllers if both are supported.
8. Only after the OVR path works, decide whether the project should migrate to the XRI tracked-device path.

## Setup and verification checklist

Complete each item in order. Do not mark an item complete until its test passes.

## Garden scene status snapshot

This snapshot is based on the saved `Assets/Garden Scene/Garden.unity` file on 2026-11-15. Unity Inspector changes that have not been saved are not included.

### Already present in Garden

- [x] Active world-space dashboard Canvas under `TasksGUI`.
- [x] `OVROverlayCanvas` on the dashboard Canvas.
- [x] `GraphicRaycaster` on the dashboard Canvas.
- [x] `TrackedDeviceGraphicRaycaster` on the dashboard Canvas.
- [x] Active `EventSystem`.
- [x] Configured `InputSystemUIInputModule`.
- [x] `XRUIInputModule` exists.
- [x] Dashboard button callbacks exist in `SimDashboardGUI.cs`.

### Current status in the saved Garden scene

- [x] `OVRInputModule` on the Garden `EventSystem`, with `CenterEyeAnchor` assigned as `rayTransform` and `editorMouseCamera`.
- [x] Right-hand `MenuOpenXrPointer` input-source bridge on the Garden `EventSystem`, with the Garden `OVRCameraRig` assigned.
- [x] `GazeIcon`/`Cursor_Timer` object with an added `OVRGazePointer`.
- [x] `OVRRaycaster` on the dashboard Canvas.
- [x] `OVRInputModule.m_Cursor` and `OVRRaycaster.pointer` links point to the same `GazeIcon` object/component.
- [ ] Disable the existing `InputSystemUIInputModule` and `XRUIInputModule` while using the OVR path.
- [ ] Temporarily disable the existing `GraphicRaycaster` and `TrackedDeviceGraphicRaycaster` while validating the OVR path.
- [ ] Add a left-hand `MenuOpenXrPointer`, if left-controller input is required.
- [ ] Play Mode hover, click, and duplicate-input tests.
- [ ] Optional `DrawPointerForUI`/`MenuClickRayDebugger` diagnostic visuals and logs.

### 1. EventSystem input module

- [x] **Status:** Implemented in the saved Garden scene. `OVRInputModule` has `rayTransform` and `editorMouseCamera` assigned to the Garden `CenterEyeAnchor`; `m_Cursor` is assigned to `GazeIcon`; the right-hand `MenuOpenXrPointer` is also present.
- [ ] **Solution:** Add or enable `OVRInputModule` on the Garden `EventSystem`; set `rayTransform` and `editorMouseCamera` to `OVRCameraRig/TrackingSpace/CenterEyeAnchor`, assign an `OVRGazePointer` cursor, and add `MenuOpenXrPointer` with `leftHand = false` so the controller aim pose and trigger are registered.
- [ ] **Test:** In the Inspector, verify all four references/components are assigned and enabled. Enter Play Mode and confirm the Console reports a tracked Menu pointer source. If `MenuOpenXrPointer` is missing, the fallback CenterEyeAnchor may exist but the controller aim/trigger will not be registered like MenuScene.

**Pointer distinction:** The ray origin/direction (`rayTransform`) is what performs the UI hit test. The pointer/cursor object only makes that ray visible and provides visual feedback. Garden needs a valid ray transform even if no visible cursor is used.

### 2. Remove competing UI input modules

- [ ] **Status:** Not complete. Both `InputSystemUIInputModule` and `XRUIInputModule` are enabled in the saved Garden scene.
- [ ] **Solution:** Disable both the Garden `InputSystemUIInputModule` and `XRUIInputModule` while matching MenuScene. Keep only the OVR module active for UI submission.
- [ ] **Test:** Check the Console during Play Mode. There should be no multiple-input-module warning, duplicate click, or double callback when selecting one button once.

### 3. Dashboard Canvas raycaster

- [x] **Status:** Implemented in the saved Garden scene. The dashboard Canvas has an enabled `OVRRaycaster`, and its `pointer` references the `GazeIcon` GameObject. The older `GraphicRaycaster` and `TrackedDeviceGraphicRaycaster` are still enabled and should be disabled for the first OVR-only test.
- [ ] **Solution:** Add and enable `OVRRaycaster` on the dashboard Canvas under `TasksGUI`. Set its `pointer` field to the exact same `GazeIcon`/`OVRGazePointer` GameObject assigned to `OVRInputModule.m_Cursor`.
- [ ] **Test:** Point at the dashboard from a valid distance. The ray should reach the Canvas and the targeted UI graphic should enter its hover/highlight state.

### 4. Canvas rendering and transform

- [x] **Status:** The saved Garden dashboard Canvas is enabled, world-space, and has an `OVROverlayCanvas`. Transform/camera behavior still needs the runtime test below.
- [ ] **Solution:** Keep the dashboard Canvas enabled and set to world-space mode. Confirm its transform, scale, facing direction, and camera/render setup match the working MenuScene arrangement.
- [ ] **Test:** In Play Mode, verify the dashboard is visible in the headset and that the ray can hit the front face of the Canvas rather than passing behind it.

### 5. Pointer source and hand/controller tracking

- [x] **Status:** The right-controller bridge is present and assigned to the Garden `OVRCameraRig`. A left-controller bridge is not present.
- [ ] **Solution:** For the MenuScene controller path, use `MenuOpenXrPointer` rather than assigning a static hand/controller anchor directly. Add a second copy with `leftHand = true` only when left-controller input is required.
- [ ] **Test:** Move the hand/controller while watching the ray. The ray origin should follow the device without lagging, remaining fixed in the scene, or disappearing when the hand is tracked.

### 6. Button graphics and raycast targets

- [x] **Status:** The dashboard Canvas and existing UI raycasters are present. Individual button raycast targets still need the test below.
- [ ] **Solution:** Confirm every dashboard button has an enabled raycastable `Graphic`, such as `Image` or `TextMeshProUGUI`, and that its layer is included by the Canvas raycaster.
- [ ] **Test:** Point directly over each button. Each button should highlight independently, and pointing between buttons should move the highlight to the correct target.

### 7. Existing button callbacks

- [x] **Status:** Existing `Button` callbacks are wired in `SimDashboardGUI.cs`; runtime behavior is not yet verified through the OVR path.
- [ ] **Solution:** Keep the existing `Button` components and `SimDashboardGUI.cs` listeners. Do not replace the callbacks unless a callback itself is reported broken.
- [ ] **Test:** Select each button once and confirm the expected behavior: Pause changes the simulation state, Restart resets the current simulation, and Exit performs the configured exit behavior.

### 8. Pointer press/select action

- [ ] **Solution:** Configure the OVR input module’s click/select action and confirm the selected pointer source can submit a UI press.
- [ ] **Test:** Hover a button, press the configured hand/controller select input, and confirm exactly one button click is generated. Release and press again to confirm each press is independent.

### 9. UI obstruction and layer filtering

- [ ] **Solution:** Remove or disable transparent UI elements that intercept raycasts, and ensure the dashboard Canvas and button layers are not excluded by the raycaster.
- [ ] **Test:** Use the raycast debug view or temporarily inspect pointer hover targets. The dashboard button should be the first UI hit when the ray is aimed at it.

### 10. Interaction during task runtime

- [ ] **Solution:** Keep the dashboard active while Garden tasks start, change objectives, pause, restart, and stop. Ensure task scripts do not disable the Canvas or EventSystem.
- [ ] **Test:** Start the Garden simulation, interact with the dashboard before and after starting the watering task, then repeat after pausing and restarting. UI interaction should remain available and callbacks should still execute once.

### 11. Both input sides, if supported

- [ ] **Solution:** Configure and test the left and right hand/controller ray paths using the same EventSystem and Canvas raycaster. Do not create a second EventSystem.
- [ ] **Test:** Repeat the hover and click tests with each supported hand/controller. Both should select the intended button without duplicate clicks.

### 12. Optional direct hand-poke path

- [ ] **Solution:** If fingertip touch is required instead of a distant ray, activate/configure `PointableCanvasModule` and the Meta hand poke interactors, then disable unrelated UI input modules during testing.
- [ ] **Test:** Touch the physical dashboard button with the fingertip. The button should highlight on contact and invoke its callback on the configured poke/press gesture. If using the ray path instead, leave this item unchecked.

## Important scope note

The core OVR UI components and right-controller ray references are now serialized in `Garden.unity`. The remaining work is to disable competing UI modules if using the OVR path, optionally add left-controller support and diagnostics, and complete Unity Play Mode verification because actual pointer tracking and click behavior depend on the active XR rig and headset/simulator input.
