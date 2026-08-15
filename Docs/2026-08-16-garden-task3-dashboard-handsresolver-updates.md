# Garden Task 3, Dashboard, and Hands Resolver Updates

Date: 2026-08-16

This document summarizes the recent Garden scene updates around Task 3 leaf catching, the simulation dashboard UI, and the shared hand input resolver.

## Task 3: Catch Leafs

Task 3 is implemented as the `catch_leafs` task.

Main files:

- `Assets/Garden Scene/task_drivers/CatchLeafs/CatchLeafsTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/CatchLeafs/BucketLeafCatchSystem.cs`
- `Assets/Garden Scene/task_drivers/CollectLeafs/LeafsFallingEffect.cs`
- `Assets/Garden Scene/Garden.unity`

Implemented behavior:

- The task driver exposes the task id `catch_leafs`.
- The pickup objective uses `pick_bucket`.
- The catch objective uses `catch_leafs`.
- `BucketLeafCatchSystem` enables bucket grab components only while the `catch_leafs` task is active.
- The bucket pickup step completes when the bucket is grabbed during the pickup objective.
- Falling leaves become visible only during the catch objective, or during the legacy `rake_leafs` task.
- A local catch volume on the bucket detects when a falling leaf enters the bucket.
- When a leaf is caught:
  - `CatchLeafsTaskDriver.CatchLeaf()` increments objective progress.
  - The leaf is hidden/reset.
  - The leaf respawns after `leafRespawnDelaySeconds`.
- Debug logging can report:
  - current task id,
  - whether catch task gating is active,
  - bucket `Grabbable`, `GrabInteractable`, and `HandGrabInteractable` enabled states,
  - bucket grab pickup completion,
  - leaf catch progress updates.

Important notes:

- The bucket task gate intentionally disables bucket grab components outside `catch_leafs`.
- This keeps Task 3 from interfering with other task flows, but it also means any other task that expects the same bucket to be grabbable must either own its own gate or use a shared policy.
- `BucketLeafCatchSystem.IsCurrentlyGrabbed()` currently checks `HandGrabInteractable.Interactors`, so it is best suited to the Meta Interaction SDK hand grab path.

## SimDashboardGUI

Main files:

- `Assets/Garden Scene/Scripts/SimDashboardGUI.cs`
- `Assets/SimManager/SimManager.cs`
- `Assets/SimManager/SimManagerObjectiveCommandService.cs`
- `Assets/SimManager/TaskSystem/SimTaskDriver.cs`
- `Assets/Garden Scene/Garden.unity`

Implemented behavior:

- `TaskName_UIText` displays the active task title, falling back to the task id.
- `TaskObjectiveStatus_UIText` displays:
  - active objective title, falling back to objective id,
  - current progress,
  - max progress,
  - normalized percent.
- Pause button calls `SimManager.PauseSimulation()` or `SimManager.ResumeSimulation()`.
- Pause button sprite switches:
  - paused state shows `playSprite`,
  - running state shows `pauseSprite`.
- Restart button resets and starts the simulation.
- Exit button stops the simulation.

Pause behavior:

- `SimManager` now has a `freezeUnityTimeWhenPaused` option.
- When pausing, the manager stores the previous `Time.timeScale` and sets `Time.timeScale = 0`.
- When resuming, stopping, resetting, disabling, completing, or failing, it restores the previous time scale.
- Task updates and objective command execution are guarded so paused or non-running simulation state does not continue progressing objectives.

Important notes:

- In VR, full pause behavior depends on systems respecting Unity time scale. Some XR runtime/input updates may continue because they are runtime-driven.
- If time-scale pause causes VR issues, the dashboard pause can be treated as a menu/display state while the later pause menu is implemented.

## HandsResolver

Main file:

- `Assets/Garden Scene/Scripts/HandsResolver/HandsResolver.cs`

Seed integration:

- `Assets/Garden Scene/task_drivers/Seeds/SeedPickupState.cs`

Purpose:

`HandsResolver` centralizes hand and controller input lookup so task scripts do not each need to resolve `OVRHand`, `OVRSkeleton`, Unity XR devices, and `HandGrabInteractor` separately.

Public access examples:

```csharp
HandsResolver.RightHand.Gestures.Grab
HandsResolver.RightHand.Gestures.Pinch
HandsResolver.RightHand.Gestures.IndexPinch
HandsResolver.RightHand.Indexes.Index0
HandsResolver.RightHand.Indexes.Tip
HandsResolver.RightHand.ActiveSource
```

Supported sources:

- `OVRHand`
  - tracked/data-valid state,
  - finger pinch booleans,
  - finger pinch strengths.
- `OVRSkeleton`
  - wrist,
  - palm,
  - thumb joints,
  - index joints,
  - fingertip poses.
- Unity XR `InputDevice`
  - left/right devices,
  - `CommonUsages.grip`,
  - `CommonUsages.trigger`,
  - `CommonUsages.gripButton`,
  - `CommonUsages.triggerButton`,
  - `CommonUsages.isTracked`.
- `HandGrabInteractor`
  - select grab,
  - palm grab through `HandGrabAPI`.

Runtime behavior:

- The resolver runs early with `DefaultExecutionOrder(-250)`.
- It can auto-resolve sources at runtime.
- It can also be added manually to the scene and assigned explicit left/right sources.
- `HandsResolver.Instance` auto-creates a resolver object in play mode if no resolver exists.
- `Refresh()` updates both hands once per frame and prevents recursive refresh loops.

Debug options:

- `Log Debug`
- `Log Source Resolution`
- `Log Hand State`
- `Debug Log Interval`

Useful debug output:

- `[HandsResolver] Source resolution: ...`
- `[HandsResolver] Left: source=..., tracked=..., valid=..., final(grab=..., pinch=...), ...`
- `[HandsResolver] Right: source=..., tracked=..., valid=..., final(grab=..., pinch=...), ...`

Seed task migration:

- `SeedPickupState` now reads `HandsResolver` first.
- If the resolver reports grab or pinch for the active hand, seed pickup uses that.
- The older direct checks remain as fallback:
  - `HandGrabInteractor`,
  - `HandGrabAPI`,
  - `OVRHand`,
  - Unity XR grip/trigger.
- Seed debug logs now include the resolver result beside the legacy checks.

Useful seed debug lines:

```text
[HandsResolver]
[SeedPickupState]
[SeedPickupThrowSystem]
```

Expected successful seed pickup signal:

```text
[SeedPickupState] Gesture state ... resolver=Right/source=.../grab=True ...
```

or:

```text
[SeedPickupState] Gesture state ... resolver=Right/source=.../pinch=True ...
```

## Verification

Latest command-line compile result:

```text
dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet
Build succeeded.
0 errors.
```

The remaining warnings are existing unrelated warnings from other project scripts.
