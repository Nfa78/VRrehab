# Garden Scene Task System Updates

Date: 2026-08-16

This document summarizes the current Garden Scene task-system updates: SimDashboard wiring, HandsResolver, Task 3 leaf catching, Task 4 rake leaves, fallen-leaf physics, and task difficulty profiles.

## SimDashboard implementation

`SimDashboardGUI` now acts as the runtime dashboard for the active simulation.

Implemented behavior:

- Displays the current task title/id.
- Displays the current objective title/id.
- Displays current objective progress as `current/max` and percentage.
- Wires restart/reset, pause/resume, and stop/exit buttons to `SimManager`.
- Pause button swaps between `playSprite` and `pauseSprite` based on simulation state.
- Dashboard refreshes automatically while the simulation runs.

Related files:

- `Assets/Garden Scene/Scripts/SimDashboardGUI.cs`
- `Assets/SimManager/SimManager.cs`

Pause behavior:

- `SimManager` freezes Unity time with `Time.timeScale = 0` when paused.
- Resuming, resetting, stopping, or starting restores the previous time scale.

## HandsResolver implementation

`HandsResolver` centralizes hand/input state so task scripts do not need to individually guess which input source is reliable.

Implemented behavior:

- Exposes static access through `HandsResolver.RightHand` and `HandsResolver.LeftHand`.
- Resolves Meta/Oculus hand sources where available.
- Resolves Unity XR controller input fallback.
- Tracks useful hand data:
  - hand side;
  - active source;
  - tracked/data-valid state;
  - grab gesture;
  - pinch gesture;
  - grip strength;
  - trigger strength;
  - index/thumb/middle pinch states where available.
- Includes toggleable debug logging for source resolution and hand state.

Related files:

- `Assets/Garden Scene/Scripts/HandsResolver/HandsResolver.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedPickupState.cs`

Seed pickup now uses `HandsResolver` first, with legacy/interactor/XR fallback still present. This keeps seed pickup working across Quest hardware and simulator/controller input.

## Task 3: Catch leaves

Task 3 now has a complete bucket-based catch flow.

Task id:

- `catch_leafs`

Objectives:

- `pick_bucket`
- `catch_leafs`
- `return_bucket`

Implemented behavior:

- Bucket grab components are enabled only for the catch-leaves task.
- The pickup objective completes when the bucket is grabbed.
- Falling leaves are visible during the catch objective.
- Leaves are counted when they enter the bucket catch logic.
- After the catch objective, the bucket must be returned to its return zone.
- The bucket return zone can be reused across tasks because returned-object state clears when the objective is no longer active.

Related files:

- `Assets/Garden Scene/task_drivers/CatchLeafs/CatchLeafsTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/CatchLeafs/BucketLeafCatchSystem.cs`
- `Assets/Garden Scene/Scripts/TriggerSystem/TSObjectiveReturnZone.cs`

Notes:

- Falling leaves are not highlighted while they fall.
- Bucket catch-volume size is not currently exposed as a difficulty parameter because it is not visually represented. If catch-volume tuning is needed later, it should be derived from or represented by the TS/radius system.

## Falling leaves and physics copies

`LeafsFallingEffect` now handles animated falling leaves and spawned physics copies.

Implemented behavior:

- Original animated leaves fall and sway.
- When an animated leaf drops below `resetYThreshold`, it resets back to its start pose.
- Immediately before reset, it can spawn a fallen physics copy.
- Fallen copies receive:
  - `Rigidbody`;
  - `BoxCollider`;
  - optional lifetime cleanup;
  - rake progress reporter;
  - hoe collision impulse component.

Related files:

- `Assets/Garden Scene/task_drivers/RakeLeaves/LeafsFallingEffect.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakedLeafProgressReporter.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakedLeafHoeImpulse.cs`

## Task 4: Rake leaves

Task 4 was changed from the old collect/leaf placeholder setup to a hoe-based rake task.

Task id:

- `rake_leaves`

Objectives:

- `pickup_hoe`
- `rake_leaves`
- `return_hoe`

Implemented behavior:

- The task tracks `HoePrefab`.
- The hoe root is tagged `Hoe`.
- Picking up the hoe completes `pickup_hoe`.
- Fallen leaf copies can be raked.
- A fallen leaf counts as successfully raked when its world `z` leaves the success band:
  - inside band: `6.7 <= z <= 7.8`;
  - success: `z < 6.7` or `z > 7.8`.
- Each fallen leaf reports rake progress only once.
- Fallen leaves receive a small extra push impulse when colliding with the hoe.
- The hoe return objective completes when the hoe is released close to its original pose.
- On successful return, the hoe can snap back, clear velocity, and become kinematic.

Related files:

- `Assets/Garden Scene/task_drivers/RakeLeaves/RakeLeavesTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/LeafsFallingEffect.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakedLeafProgressReporter.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakedLeafHoeImpulse.cs`
- `Assets/Garden Scene/Garden.unity`
- `ProjectSettings/TagManager.asset`

Notes:

- The folder is now `RakeLeaves`.
- The class `LeafsFallingEffect` still keeps its older spelling for compatibility.

## Difficulty-level implementation

Difficulty is now implemented as an editor-selectable task-level enum plus per-driver profiles.

Selection location:

- In `SimManager`, expand `tasks`.
- Each `SimTask` now has a `Difficulty Level` enum.

Difficulty enum:

- `Level0`
- `Level1`
- `Level2`

Mapping to driver profiles:

- `Level0` maps to profile level `1`.
- `Level1` maps to profile level `2`.
- `Level2` maps to profile level `3`.

This keeps the user-facing editor enum zero-based while preserving the existing profile-level numbering internally.

Related files:

- `Assets/SimManager/TaskSystem/SimTask.cs`
- `Assets/SimManager/TaskSystem/SimTaskDriver.cs`
- `Assets/SimManager/SimManager.cs`
- `Assets/SimManager/TaskSystem/TaskAdaptiveBridge.cs`

Default behavior:

- All three profiles default to the current gameplay values.
- Adding the difficulty system does not change runtime behavior unless profile values are edited.
- The old raw integer difficulty field on `SimTaskDriver` is hidden.
- The selected difficulty should be changed on the task entry, not on the driver.

## Difficulty parameters by task

### Task 1: Water plants

Approved difficulty parameters:

- required water hits per plant;
- water trigger radius;
- water trigger length;
- plant hitbox scale;
- tilt threshold;
- spill duration;
- time limit;
- return-zone radius.

Related files:

- `Assets/Garden Scene/task_drivers/WaterPlants/WaterPlantsTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/WaterPlants/WaterSpill.cs`
- `Assets/Garden Scene/task_drivers/WaterPlants/WaterSpillSetup.cs`
- `Assets/Garden Scene/task_drivers/WaterPlants/FlowerPot.cs`
- `Assets/Garden Scene/Scripts/TriggerSystem/TSObjectiveReturnZone.cs`

### Task 2: Throw seeds

Approved difficulty parameters:

- seed pickup radius;
- required successful throws;
- gate/ring radius scale;
- active gate count;
- wrong-gate reset behavior;
- seed spawn count;
- throw force min/max;
- throw cone half-angle;
- throw direction arrow visibility.

Rejected/not implemented as difficulty:

- max time between gates.

Related files:

- `Assets/Garden Scene/task_drivers/Seeds/SeedsTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedPickupState.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedGateSequence.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedGate.cs`
- `Assets/Garden Scene/task_drivers/Seeds/TSSeedGate.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedThrowSpawner.cs`
- `Assets/Garden Scene/task_drivers/Seeds/SeedThrowArrow.cs`

### Task 3: Catch leaves

Approved difficulty parameters:

- required caught leaves;
- time limit;
- leaf fall speed;
- leaf sway amount;
- leaf depth/drift amount;
- leaf respawn delay.

Rejected/not implemented as difficulty:

- bucket catch volume.

Related files:

- `Assets/Garden Scene/task_drivers/CatchLeafs/CatchLeafsTaskDriver.cs`
- `Assets/Garden Scene/task_drivers/CatchLeafs/BucketLeafCatchSystem.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/LeafsFallingEffect.cs`

### Task 4: Rake leaves

Approved difficulty parameters:

- required raked leaves;
- time limit.

Related files:

- `Assets/Garden Scene/task_drivers/RakeLeaves/RakeLeavesTaskDriver.cs`

## Adaptive bridge behavior

`TaskAdaptiveBridge` still sends the configured difficulty level to the adaptive API when a task starts.

It can also push its `difficultyLevel` into the active local task driver, but the main editor workflow is now:

1. Select the task difficulty enum on `SimManager.tasks`.
2. Edit the actual profile values on the task driver.
3. Start the simulation.

## Validation

After these updates, `Assembly-CSharp.csproj` compiled successfully with:

- `0` errors;
- existing unrelated warnings only.

