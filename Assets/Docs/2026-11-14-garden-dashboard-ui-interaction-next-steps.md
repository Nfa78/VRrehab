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

### 1. Configure the Garden EventSystem

On the Garden `EventSystem`:

1. Add or enable `OVRInputModule`.
2. Assign its `rayTransform` to the intended hand/controller ray origin.
3. Assign its cursor/pointer reference if a visible pointer is required.
4. Disable the existing `XRUIInputModule` unless the project is deliberately migrated to the XRI Input System path.
5. Confirm that only one active module is responsible for submitting UI pointer events.

Do not leave both OVR and XRUI modules active while debugging. Competing modules can produce duplicate, missing, or inconsistent pointer events.

### 2. Configure the dashboard Canvas

On the dashboard Canvas under `TasksGUI`:

1. Add an enabled `OVRRaycaster`.
2. Assign the same pointer object/ray path used by the `OVRInputModule`.
3. Keep the Canvas in world-space mode.
4. Keep `OVROverlayCanvas` only if it is required for the target headset rendering mode.
5. Avoid relying on `GraphicRaycaster` or `TrackedDeviceGraphicRaycaster` as the only raycaster when using `OVRInputModule`.

The dashboard buttons should remain normal Unity UI `Button` components. Their existing listeners in `SimDashboardGUI.cs` do not need to be rewritten.

### 3. Choose and configure the pointer source

The pointer source must be an active transform that follows the user’s hand or controller. Check the MenuScene hierarchy for the exact ray origin used by the project, then reproduce that relationship in Garden.

For hand-ray interaction, verify:

- The hand tracking objects are active in Play Mode.
- The ray origin follows the intended hand.
- The ray is visible while pointing at the dashboard.
- The pointer/cursor is not blocked by another ray or camera object.

If the project uses separate left and right hand rays, configure both according to the existing Oculus interaction setup rather than creating a second independent EventSystem.

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

## Play Mode verification checklist

- [ ] A pointer/ray is visible from the intended hand or controller.
- [ ] Pointing at a dashboard button produces a hover/highlight state.
- [ ] Pressing/selecting the button invokes its existing callback.
- [ ] Pause changes the simulation state.
- [ ] Restart resets the current simulation as expected.
- [ ] Exit follows the existing scene/application exit behavior.
- [ ] No duplicate UI clicks are logged.
- [ ] No EventSystem warning reports multiple active input modules.
- [ ] Interaction still works after starting and stopping the watering task.

## Important scope note

This document describes the required setup; the actual OVR UI components and hand/controller ray references have not yet been added to `Garden.unity`. The next implementation should be tested in Unity Play Mode because the final pointer references depend on the active XR rig and installed Oculus package version.
