# MenuRoot pointer and click diagnosis

## Scope and current status

This report covers `Assets/MenuScene/MenuScene.unity`, specifically the login, signup, and scene-selection UI under `MenuRoot`. The separate `FlatUnityCanvas` and `PCM` objects are experimental and are disabled in the saved scene.

The user interacts in the **separate Meta XR Simulator window**, not Unity's Game tab. The current fix routes the Simulator's right-controller aim pose and trigger through the menu's OVR input module. Desktop mouse emulation is disabled for this scene.

The revised SDK and game assemblies compile with zero errors. Scene-reference checks pass. This is not yet a confirmed end-to-end Simulator fix: the revised scene still needs the user's runtime test.

## UI readability and spacing update

The canvas remains at its original uniform scale. Individual controls are now authored as wider horizontal rectangles: buttons are 320×64, input fields are 640×64, and labels use wider layout bounds while keeping their original font assets and font sizes. Vertical layout groups now apply their preferred heights, so controls no longer remain 100×100 squares in the generated scene.

The `EntryPanel`, `LoginPanel`, and `SignupPanel` had negative vertical layout spacing (`-36.06`, `-45.7`, and `-45`), which caused their controls to overlap. Those groups now use positive spacing of 18–20 units. The main menu, details, settings, and scene-card groups use smaller positive gaps so adjacent controls remain visually separated.

MenuRoot TextMeshPro elements are centered horizontally and vertically, including button labels and input-field text/placeholders, so text stays inside the widened control rectangles. The auto-setup generator uses the same centered alignment for newly generated input fields.

## What the supplied log establishes

- Meta XR Simulator v205.0 is the selected OpenXR runtime.
- The menu verifier reported zero setup errors and one scaled-UI warning.
- The previous implementation logged `CanvasPointer | source=EditorMouse:CenterEyeAnchor`, including after Unity reported `OnApplicationFocus(false)`.
- That implementation was reading Unity's desktop mouse while the user was interacting with the external Simulator window. Its early return prevented the normal XR input path from processing controller events.
- Most logged ray states had `target=<none>`. Later states mentioned `Label` and `HeaderLabel`; the log did not establish that a button received a press or release.
- The log also contains unsupported OpenXR-extension messages, audio fallback, startup stereo-projection warnings, and shutdown warnings. Those messages do not establish the cause of the missing UI clicks.

The previous Game-tab mouse fix addressed the wrong input source. A mouse action in the Simulator must reach the app as simulated XR input; its screen coordinates are not Unity Game-view mouse coordinates. Meta describes point-and-click as making the controller ray follow the mouse, with a configurable left/right Action input: [Meta point-and-click documentation](https://developers.meta.com/horizon/documentation/native/xrsim-point-and-click/).

## Implemented fixes

### 1. Bind the menu to the OpenXR aim pose and trigger

`Assets/MenuScene/Scripts/Runtime/UI/Common/MenuOpenXrPointer.cs` is attached to the menu EventSystem. It implements `OVRInputModule.InputSource` and registers while enabled.

- The saved scene selects the right controller (`leftHand: false`).
- It reads the OpenXR controller's `pointer/position`, `pointer/rotation`, tracking validity, and `triggerPressed` controls, with equivalent pointer-position/rotation and trigger-button aliases as fallbacks.
- If the Simulator does not expose those controls as an `XRController`, it falls back to the `OVRInput` right-controller pose and the `OVRCameraRig.rightHandAnchor`, which are updated by the active Meta/OpenXR runtime.
- It logs the discovered Input System device/control availability and periodically logs why tracking has not been acquired (`device`, controls, anchor, OVR connection, and pose-validity state).
- It transforms the local aim pose through `OVRCameraRig.trackingSpace`, so the rig's scene translation and rotation are accounted for.
- It does not substitute the controller grip pose or the head-camera direction for an unavailable aim pose.
- A trigger press and release feed the OVR module's existing button-event processing.
- Loss of tracking or disabling the source cancels an outstanding press/drag without synthesizing a click on its previous target.

The installed OpenXR controller profile exposes distinct device/grip and pointer/aim controls. Using the actual aim pose avoids depending on a raw anchor's orientation. This addresses a plausible source of the original angular offset; the exact original offset was not reproduced during this investigation.

### 1a. Draw the ray actually processed by the OVR module

`Assets/MenuScene/Scripts/Runtime/UI/Common/MenuClickRayDebugger.cs` is attached to the menu EventSystem. It reads the private `m_VRRayPointerData` dictionary after `OVRInputModule.Process()` has run, then draws a red line from every stored `worldSpaceRay` to its actual `pointerCurrentRaycast.worldPosition`. A yellow sphere marks a valid UI hit. If pointer data is not present, it draws fallback rays from registered input sources so a missing source is visible too.

The debugger logs changes in the Unity Console with the `[MenuClickRay]` prefix. The source ID, origin, direction, hit target, and whether the editor mouse override is active are included. This is the authoritative diagnostic: it shows the ray that the OVR module used for hover and trigger events, rather than reconstructing a ray from the visible cursor.

The debugger is deliberately attached to the OVR EventSystem and not to the disabled experimental PCM EventSystem. It uses runtime-created `LineRenderer` objects, so the red ray should appear in the Simulator's XR view while Play mode is running.

### 2. Stop desktop mouse input from suppressing XR input

In `MenuScene.unity`, `OVRInputModule.useEditorMouse` is now false. The optional Game-tab mouse mode remains available but additionally requires application focus. It is not the mode to use for the separate Simulator window.

`OVRInputModule.ShouldActivateModule()` now recognizes active registered XR sources. Its module-support check uses the enabled Input System rather than unconditionally falling through to legacy mouse APIs.

### 3. Keep pointer visuals tied to the processed ray

`DrawPointerForUI.cs` reads the active input module and its saved `OVRPointerEventData.worldSpaceRay` and hit result. Authoritative mode does not independently smooth the endpoint or fall back to an inactive module.

`OVRGazePointer.cs` applies its forward-ray fallback in `LateUpdate` only when no hit position was supplied during the frame. This prevents its ordinary `Update` from overwriting an actual hit set by the input module.

`MenuSceneSetupVerifier.cs` uses the explicitly assigned menu EventSystem. When `MenuOpenXrPointer` owns input, the verifier does not replace its ray with a camera/controller-anchor fallback.

### 4. Respect UI Raycast Target flags

The installed `OVRRaycaster` iterated all graphics and did not skip `Graphic.raycastTarget == false`. Its call to `Graphic.Raycast()` checks filters but does not itself enforce that flag.

The raycaster now skips graphics with Raycast Target disabled. Decorative text and headers therefore cannot intercept a ray solely because their rectangles overlap a button. This is a confirmed code issue; the supplied log alone does not prove that it caused every failed click.

### 5. Keep one menu input setup and add diagnostics

The saved scene has:

| Object/component | State |
| --- | --- |
| `MenuRoot` | Enabled |
| Original `EventSystem` | Enabled |
| `OVRInputModule` | Enabled |
| `MenuOpenXrPointer` | Enabled, right hand |
| `XRUIInputModule` on that EventSystem | Disabled |
| Experimental `PCM` | Disabled |
| Experimental `FlatUnityCanvas` | Disabled |
| `MenuCanvas.worldCamera` | Explicit CenterEyeAnchor camera |
| `OVRRaycaster.pointer` | GazeIcon |
| OVR Editor mouse override | Disabled |
| Pointer-click diagnostics | Enabled |

The fallback controller button is configured as the right index trigger rather than the A button. The registered OpenXR source reads its own trigger directly; this fallback is not the main click path.

## Simulator setup for the next test

1. Reopen `Assets/MenuScene/MenuScene.unity` after Unity imports the changed files.
2. Enter Play mode and use the **Meta XR Simulator window**.
3. In Simulator Inputs, set **Right input = Controller**.
4. Turn **Point & click on** and set **Action input = Right controller**.
5. Turn **ISDK pointer offset off** for this OVR/OpenXR aim-pose implementation. That option applies the additional pose adjustment used by ISDK's Ray Interactor, which this menu does not use.
6. If the Simulator cursor and UI aim remain offset, enable **Compatibility mode**. The supplied log reports Depth Submission Mode = None, and this menu uses an overlay canvas. Meta recommends compatibility mode when depth is unavailable or default aiming is imprecise.

The Simulator settings above were not changed programmatically. Their meaning is documented in [Meta's point-and-click guide](https://developers.meta.com/horizon/documentation/native/xrsim-point-and-click/). For hand/controller input selection, see [Meta's input simulation guide](https://developers.meta.com/horizon/documentation/unity/xrsim-input-keyboard/).

## Expected diagnostics and functional checks

Filter Unity's Console for `MenuClickRay` and `MenuUIPointer`.

Expected startup messages:

```text
[MenuUIPointer] Waiting for right OpenXR controller aim and trigger.
[MenuUIPointer] Right OpenXR aim tracking acquired (...).
```

The new debugger emits a frame summary and a full raycast dump for every stored XR pointer:

```text
[MenuClickRay][FRAME 123] eventSystem=OVRInputModule ovrEnabled=True menuRoot=True pointerDataCount=3 trackedSourceCount=1
[MenuClickRay][RAYCAST pointerId=0] rayOrigin=(...) rayDirection=(...) storedHit=MenuRoot/... underMenu=True ... allResults=...
[MenuUIPointer][RAY] source=MenuOpenXrPointer id=0 origin=(...) direction=(...) hit=... distance=... module=OVRRaycaster
[MenuUIPointer][STATE] source=MenuOpenXrPointer id=0 pressed=False released=False active=True hit=... pointerEnter=...
```

Each `RAYCAST` dump lists every result, its hierarchy path, `underMenu` status, distance, world position, raycaster, and parent `Selectable`/`Button` interactability. Interpret the output as follows:

- A frame with `pointerDataCount=0` and `trackedSourceCount=0`: the OVR module has no usable XR pointer data. Check Simulator right-controller selection and the aim pose source.
- A red ray appears but its target is `<none>`: the module is running, but its ray misses the menu or the canvas raycaster is not active.
- A yellow endpoint and a button target appear, but no `[MenuUIPointer][STATE] ... pressed=True` or `Press hit` message follows: the Simulator trigger/action binding is not reaching `OVRInputModule`.
- `Press hit` names a label or decorative image: inspect its Raycast Target or parent hierarchy; the raycaster now skips graphics with Raycast Target disabled.
- `Press hit` names a button and `Release` names the same button, but the panel does not change: input routing is working and the remaining issue is button interactability/listener wiring.

The laser should report a tracked source containing `MenuOpenXrPointer:RightMenuAim`, rather than `EditorMouse:CenterEyeAnchor`.

On a button press/release, expect messages such as:

```text
[MenuUIPointer] Press hit=... handler=... position=...
[MenuUIPointer] Release hit=... pressed=...
```

Check all of these:

- Hover and click Login and Sign Up, including near their top and bottom edges.
- Hold the pointer still for a second before clicking; then repeat while moving it. A persistent offset and a motion-only offset are different failures.
- Press over a button, move away, and release: it should not click that button.
- Test selecting an input field and entering text. Authentication requests are a separate check from UI event delivery.
- Test the scene selector and settings slider once reachable.
- Move or rotate the simulated headset and repeat to check tracking-space alignment.
- If possible, interrupt controller tracking while a button is held; it should release/cancel without activating the button.

## Potential remaining issues and how to isolate them

| Symptom | Likely area to inspect | Next check or fix |
| --- | --- | --- |
| Only the waiting message appears | Controller disabled, wrong hand selected, missing input profile, or unavailable aim controls | Confirm Right input is Controller and the matching OpenXR controller profile is enabled. Check whether the simulated device exposes pointer and trigger controls. |
| Tracking acquired, but no press messages | Simulator click binding or trigger input | Confirm Point & click and Action input Right; inspect the Simulator's Input bindings. |
| Press messages have `hit=<none>` | Aim misses the UI | Compare the visible ray with the panel, check compatibility mode and ISDK offset, then inspect canvas placement and orientation. |
| Press has a hit but `handler=<none>` | Non-interactive graphic intercepts the ray | Inspect that graphic's Raycast Target and parent hierarchy. A click handler must be on the target or one of its parents. |
| Press reaches a button, release has a different target | Pointer movement, tracking changes, or layout moving during the click | Hold the pointer still, inspect UI animation/layout and press/release positions. |
| Press and release reach the same button but the panel does not change | Button interactability, CanvasGroup, or menu event listeners | Inspect `Button.interactable`, parent CanvasGroups, `EntryPanelView`, and `MenuFlowController`; input routing is then less likely to be the issue. |
| Clicking works but there is a consistent angle/position offset | Simulator aiming mode, ISDK offset, or rendered-overlay alignment | Disable ISDK offset; test compatibility mode. Temporarily compare a flat ordinary world-space canvas if needed. Do not compensate by moving each button. |
| Offset appears only while moving | Pose-update timing or other visual smoothing | Compare logged event data with visuals; authoritative laser smoothing is already disabled. |
| Only VolumeSlider or FeedbackPanel/Close is misaligned | Locally scaled UI or parent layout | Inspect those specific RectTransforms. The verifier flags their scale, but this is not proof of a global click problem. |
| Projection warnings continue after XR startup | Camera/overlay initialization or invalid XR view data | Inspect the running center-eye camera and overlay after the session starts. The supplied startup warnings alone are insufficient to diagnose this. |
| A future SDK update reintroduces the issue | Embedded SDK modifications overwritten | Preserve/reapply or upstream the changes to OVRInputModule, OVRGazePointer, and OVRRaycaster. |

The component is currently configured for a **right controller**, not hand gestures. To use the left controller, enable `Left Hand` on `MenuOpenXrPointer` before Play and choose the matching Simulator Action input. Hand-pinch support requires a hand input source and is not established by this controller fix.

## Historical findings versus current causes

The last committed scene had `OVRRaycaster.pointer` assigned to `EventSystem` at world origin. That was a real mismatch in the legacy camera-to-pointer mouse path. The working scene had already changed this reference to GazeIcon before the latest runtime test. It should not be treated as the established cause of that test's missing clicks.

Similarly, `MenuRoot` being disabled in an earlier saved scene was intentional experimentation, not evidence that its normal configuration caused the offset.

## Feedback panel and authenticated API handoff

The saved `FeedbackPanel` had been changed to a 150x30 world-space rectangle, with a local Y scale of 2 on the panel and 0.5 on `MessageLabel`. Its local Z was also -102 relative to `MenuCanvas`, which could place the panel behind the overlay. The panel is now a 900x80 rectangle at the existing top position, with unit scale and local Z 0. `MessageLabel` fills it with 20-unit margins, uses unit scale, 28-point centered TextMeshPro text, and normal overflow. The placeholder scene text was cleared; `MenuFeedbackController` supplies the message at runtime. The controller keeps the informational panel from blocking UI raycasts while it fades. The editor auto-setup path now resets the same transform values so regenerating the menu does not restore the hidden layout.

`AdaptiveApiClient` is now a persistent singleton. The active client under `MenuRoot/Systems` registers as `AdaptiveApiClient.Instance`, calls `DontDestroyOnLoad`, and keeps mirroring the authenticated `AuthSessionResponse` into `AdaptiveRuntimeContext`. A second client created by a Garden scene is removed before it can overwrite the configured client or session.

The Console now identifies this handoff without printing credentials: look for `[AdaptiveApiClient] Registered persistent client`, `[AdaptiveApiClient] Authentication session stored ... tokenPresent=True`, and, if a scene-local copy exists, `[AdaptiveApiClient] Duplicate client ... was removed`.

The Seeds, Water Plants, Rake Leaves, and Catch Leafs adaptive reporters, plus `TaskAdaptiveBridge`, resolve `AdaptiveApiClient.Instance` before searching only the current scene. Consequently, the client that received the menu login token remains the one used for patient-profile, session, adaptive-task, and metrics requests after loading Garden. `AdaptiveRuntimeContext` already survives scene loads as a static context and retains the token, authenticated user, patient profile, active session ID, and task difficulty state. Sign-out still clears both the client token and that context.

## Validation limits

- `Oculus.VR.csproj`: build passed with zero errors.
- `Assembly-CSharp.csproj`, including the new source through a temporary compile target: build passed with zero errors and existing warnings.
- The latest `Oculus.VR.csproj` verification also passed with zero warnings and zero errors. A repeat of the full Assembly-CSharp build was unavailable in this shell because Unity's generated `Temp/bin/Debug` dependency DLLs are not present; Unity regenerates those when it imports the project.
- Static scene checks confirmed source attachment, right-hand selection, disabled desktop mouse override, active MenuRoot, and consistent file IDs.
- These checks use Unity's cached dependency assemblies. They do not execute the Simulator or verify end-to-end button behavior.
- Final acceptance remains the Simulator test above, using the newly added press/release diagnostics if it fails.

## Garden task records: why a session could exist without tasks

The Garden task drivers add their components at runtime. Their original order was:

1. add the task tracker;
2. the tracker enables and synchronizes itself with the already-running `SimManager`;
3. add the adaptive reporter.

`TaskRunStarted` is an event, so a reporter added after step 2 could miss it permanently. The reporter then never called `POST /task-executions`, while its own flow (or another reporter) could still create `POST /sessions/start`. The backend therefore showed a session with no task execution or metrics rows.

Each tracker now exposes a snapshot of its active run, and each reporter checks that snapshot immediately after subscribing. This closes the lifecycle race and guards against duplicate starts. The shared flow also logs every stage: API/auth resolution, patient profile, session reuse/start, task execution start, metrics submit, and task end. Logs use the task-specific reporter prefix, for example `[WateringTaskAdaptiveReporter]`, and never print the access token.

For the next simulation, filter the Console for `Task reporting`, `Recovered active task run`, `Starting task execution`, and `Task execution created`. If the task-start request fails, the log now includes the backend or transport error; that distinguishes a server task-id/configuration problem from a Unity event-lifecycle problem.
