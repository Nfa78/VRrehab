# Seed System Flow (SimManager + SimObjectiveInteraction)

## Goal
Define the exact flow for:
- picking seeds from a bucket child object (`Seeds`) with trigger-based detection,
- confirming pickup via hand gesture (pinch or grab),
- spawning seed cubes from hand on release as a cone burst,
- reporting objective progress through `SimObjectiveInteraction` into `SimManager`.

This flow assumes:
- `Bucket` has child object `Seeds`.
- `Seeds` has a trigger collider.
- seed-pick logic is attached to `Seeds` (or equivalent controller on bucket referencing `Seeds`).
- objective reporting is done through `SimObjectiveInteraction` on the same gameplay object.

## Components

### Seeds (child of Bucket)
Attach:
- `SimObjectiveInteraction` (configured per objective)
- `SeedPickupThrowSystem` (implemented runtime flow script)
- trigger collider (`isTrigger = true`)
- optional `ObjectiveHighlightRuntime` target

Responsibilities:
- detect hand entering/leaving trigger
- detect confirming gesture (pinch or grab)
- lock "seeds in hand" state after gesture
- detect release event
- spawn throw burst
- report objective updates to `SimManager` through `SimObjectiveInteraction`

### Hand Reference
- Use tracked hand transform (or grab interactor transform) to:
  - capture current world position for spawn origin
  - compute hand movement direction for throw force

### SeedGroundZone
- Trigger volume where thrown seed particles/prefabs are counted.
- Can report per-seed landings or aggregate per throw.

## SimManager Objective Model

Recommended objective(s) in task sequence:

1. `pick_seeds` (Boolean)
- Completed when hand is inside trigger and pinch/grab is performed.
- This marks "seeds are in hand".

Optional extension:
2. `throw_seeds` (Counter)
- Progress increases from landed spawned cubes.
- Completed at `MaxValue = N`.

## Runtime State Machine (Seed Object)

States:
- `Idle`
- `HandInTrigger`
- `LoadedInHand`
- `ReleasedSpawned`

Transitions:
- `Idle -> HandInTrigger`: hand enters `Seeds` trigger.
- `HandInTrigger -> LoadedInHand`: pinch OR grab gesture is detected.
- `LoadedInHand -> ReleasedSpawned`: gesture/grab release detected; spawn burst.
- `ReleasedSpawned -> Idle`: reset for next pickup cycle.

## Event Flow

### 1) Detect Hand in Trigger
- On hand collider enter `Seeds` trigger:
  - set `handInside = true`
  - cache active hand reference (left/right)
- On exit:
  - set `handInside = false` (unless already loaded)

### 2) Confirm Pickup Gesture
- While `handInside == true`, if either is true:
  - pinch gesture performed, OR
  - grab gesture performed
- Then:
  - set `seedsLoaded = true`
  - call `SimObjectiveInteraction.CompleteObjective()` for `pick_seeds`
  - optional pickup feedback (haptics/audio)

### 3) Release -> Spawn Seeds
- If `seedsLoaded == true` and action is released (pinch/grab release event):
  - spawn exactly **10** small cube seeds
  - spawn origin = current hand position
  - base throw direction = normalized hand movement direction
    - recommended: `(handPositionNow - handPositionPrevious) / deltaTime`
  - apply force in cone burst:
    - sample random direction inside cone around base direction
    - `coneHalfAngleDeg` configurable (e.g. 12-20 deg)
    - `forceMin/forceMax` configurable
  - clear `seedsLoaded`
  - return to idle cycle

## Current Implementation Notes

Implemented script:
- `Assets/Garden Scene/Scripts/Watering/SeedPickupThrowSystem.cs`

Behavior implemented:
- detects hand entry via `HandGrabInteractor` / `OVRHand` / optional hand tag fallback
- detects action active by:
  - `HandGrabInteractor.IsGrabbing` (grab), and/or
  - `OVRHand` index pinch (pinch)
- completes pickup objective on pickup edge
- spawns 10 cubes on release edge in a movement-direction cone burst
- optional throw-progress reporting (`throwObjectiveInteraction`)

## Throw Spawn Details (Required)

- Spawn count: `10`
- Spawn prefab: small cube (pooled recommended)
- Rigidbody required on each cube
- Force mode: `Impulse`
- Direction source: hand movement vector at release
- Cone distribution:
  - axis = movement direction
  - random angular offset within radius
  - slight force variation per cube

## Reporting Path

`SeedPickupTrigger` / seed scripts do **not** call `SimManager` directly.
They call:
- `SimObjectiveInteraction.CompleteObjective()`
- `SimObjectiveInteraction.AddObjectiveProgress()`

`SimObjectiveInteraction` forwards into `SimManager`, which:
- updates active objective/task state,
- handles transitions,
- logs runtime state for debugging.

## Adaptive Considerations

For easier/harder variants (driven by session/difficulty):
- trigger size on `Seeds`
- allowed gesture time window after trigger entry
- cone half-angle (wider = easier)
- throw force variance
- throw target count (`throw_seeds.MaxValue`, if used)

Keep objective IDs stable:
- `pick_seeds`
- `throw_seeds`

This ensures consistent analytics/adaptive behavior mapping.

## Failure + Recovery

If seed object falls:
- existing `SimManager` object-fall recovery resets it to initial pose.
- if configured, active objective attempt is failed/reset with reason `ObjectFell`.

Behavior expectation:
- task continues running,
- user retries current objective attempt.

## Outline / Highlight

Use objective highlight on the seeds object:
- `SimTaskObjective.highlightTarget = Seeds`
- active objective enables pulsing outline through `ObjectiveHighlightRuntime`

This keeps visual guidance coupled to objective activation state.

## Minimal Implementation Notes

- Prefer pooled cubes (avoid runtime instantiate spikes).
- Require explicit release edge (pressed -> released) to prevent repeated spawns.
- Keep a short cooldown after spawn (e.g. 0.2s) to avoid double-trigger.
