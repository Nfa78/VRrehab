# General Task System Implementation

This document outlines a reusable `TaskSystem` module for VR rehab scenes where tasks are defined as data, started explicitly, tracked through objective state, and failed or reset when required objects leave valid bounds.

This version is aligned with the existing `Assets/AdaptiveSystem` pipeline so task runtime, telemetry, and adaptive difficulty all use the same identifiers and metrics flow.

## Goals

- Replace scene-specific one-off objective scripts with a shared task framework.
- Define each task in a consistent way: what starts it, which objects it cares about, what success looks like, and what failure looks like.
- Track each required object's initial place so it can be validated and reset.
- Detect task failure when an object falls, leaves the playable area, or becomes invalid for continued play.
- Allow scenes such as kitchen, coffee, garden, and cleaning tasks to reuse the same runtime pipeline.
- Align task state and telemetry with `AdaptiveSystem.Models.TaskStartRequest`, `TaskExecution`, `GlobalMetrics`, and `SceneMetric`.

## Adaptive Alignment

The task framework should not behave as a separate subsystem. It should be the scene-side producer of the data already expected by the adaptive pipeline.

Current adaptive models already define:

- session start/end via `SessionStartRequest` and `SessionEndRequest`
- task execution start/end via `TaskStartRequest` and `TaskEndRequest`
- adaptive decision input via `TaskMetricsRequest`
- aggregate scoring via `GlobalMetrics`
- task-specific features via `SceneMetric[]`

Because `AdaptiveSystem.Models.TaskDefinition` already exists, the gameplay task authoring asset should use a different type name to avoid collisions.

Recommended naming:

- `RehabTaskDefinition` for scene-authored gameplay tasks
- `RehabTaskStepDefinition` for authored steps
- `TaskAdaptiveBridge` for adaptive submission

## Adaptive Model Mapping

The scene task system should map directly to adaptive identifiers.

Recommended mapping:

- `RehabTaskDefinition.taskId` -> `AdaptiveSystem.Models.TaskStartRequest.task_id`
- active rehab session id -> `TaskStartRequest.session_id`
- selected difficulty -> `TaskStartRequest.difficulty_level`
- task runtime start time -> `TaskStartRequest.start_time`
- task runtime end time -> `TaskEndRequest.end_time`
- task-specific measurements -> `TaskMetricsRequest.scene_metrics`
- aggregate errors/prompts/time -> `TaskMetricsRequest.global_metrics`

The runtime task controller should retain:

- `session_id`
- `task_id`
- `task_execution_id`
- `task_level_id`
- `difficulty_level`

These are already present in `TaskStartResponse` and `TaskExecution` and should become part of the live task runtime state.

## Core Idea

Each playable activity should be represented by a `RehabTaskDefinition` asset plus a small runtime controller.

At runtime:

1. A task is selected or started.
2. Runtime state is created from the task definition.
3. Required objects register their initial pose.
4. Objective checks run until the task succeeds or fails.
5. If a failure condition occurs, the system can:
   - fail the task,
   - reset one object,
   - reset the full task,
   - or allow retry from the current step.

## Proposed Runtime Pieces

### `RehabTaskDefinition`

ScriptableObject that describes one task.

Suggested fields:

- `taskId`
- `adaptiveTaskId`
- `displayName`
- `description`
- `scene`
- `taskType`
- `autoStart`
- `startDelay`
- `steps`
- `trackedObjects`
- `failureRules`
- `successRules`
- `resetMode`
- `guidanceProfile`
- `scoreProfile`
- `adaptiveTrackingProfile`

Notes:

- `adaptiveTaskId` should match the backend-recognized `task_id`.
- `scene` and `taskType` should mirror `AdaptiveSystem.Models.TaskDefinition.scene` and `.task_type`.

### `RehabTaskStepDefinition`

Represents one step inside a task.

Suggested fields:

- `stepId`
- `title`
- `requiredObjects`
- `startConditions`
- `completionConditions`
- `failureConditions`
- `showGuidance`
- `allowRetryWithoutFullReset`

### `TrackedTaskObject`

Describes an object the task must monitor.

Suggested fields:

- `objectId`
- `target`
- `rigidbody`
- `adaptiveFeaturePrefix`
- `mustTrackInitialPose`
- `resetPosition`
- `resetRotation`
- `resetVelocityOnReset`
- `failureFloorY`
- `failureDistanceFromStart`
- `failureZone`
- `requiredTag`
- `trackTrajectory`
- `trackCollisions`
- `trackResetCount`
- `trackOutOfBoundsCount`
- `trackTimeHeld`

`adaptiveFeaturePrefix` is the key bridge to `SceneMetric.feature_name`.

Examples:

- `watering_can`
- `bucket`
- `grass_bundle`
- `pan`
- `paper_ball`

### `TaskManager`

Global scene service that starts, stops, resets, and transitions tasks.

Responsibilities:

- load `TaskDefinition`
- create runtime state
- start current step
- listen for success/failure events
- reset tracked objects
- publish UI and analytics events
- own the active adaptive execution context
- tell telemetry recorders which transforms and events to track

### `TaskRuntime`

Per-task runtime state.

Suggested runtime data:

- current task id
- adaptive task id
- task execution id
- task level id
- session id
- difficulty level
- current step index
- task state
- active tracked objects
- start timestamp
- failure reason
- completion progress
- error count
- prompt count
- reset count

### `TaskObjectTracker`

Component attached to or associated with task-relevant objects.

Responsibilities:

- capture initial pose when task starts
- monitor object transform and rigidbody state
- detect out-of-bounds or dropped-object failures
- reset object back to start pose when requested
- produce task-object features for adaptive reporting

## Task State Model

Recommended enum:

```csharp
public enum TaskState
{
    Idle,
    Starting,
    Running,
    StepCompleted,
    Completed,
    Failed,
    Resetting,
    Paused
}
```

Step state can be separate if needed:

```csharp
public enum TaskStepState
{
    Locked,
    Available,
    Running,
    Completed,
    Failed
}
```

## Object Initial Place

Every tracked object should record its initial place when the task begins, not only from editor-time data.

Store:

- world position
- world rotation
- parent transform at task start
- linear velocity
- angular velocity
- distance baseline from expected target if applicable

Suggested runtime struct:

```csharp
public struct TaskObjectPose
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Transform Parent;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
}
```

Why capture at runtime:

- some scenes reposition props before the player starts
- left/right handed variants may move start locations
- interactables may be spawned dynamically
- scene resets should restore the exact live setup the task started with
- adaptive metrics need a stable baseline to compute object drift and recovery

## Failure Detection

For now, assume "object failure" includes cases where an object falls or leaves its valid gameplay area.

Recommended failure checks:

1. Object falls below a floor threshold.
2. Object moves too far from its initial place.
3. Object exits an allowed trigger volume.
4. Object is destroyed or becomes inactive unexpectedly.
5. Object is snapped into an invalid target.
6. Object is dropped for too long in a forbidden area.

These failure checks should increment both local task state and adaptive error metrics.

Suggested failure reasons:

```csharp
public enum TaskFailureReason
{
    None,
    FellOutOfBounds,
    LeftAllowedZone,
    TooFarFromStart,
    Destroyed,
    InvalidPlacement,
    Timeout
}
```

## Recommended Failure Flow

When a tracked object fails:

1. `TaskObjectTracker` raises a failure event.
2. `TaskManager` resolves whether the current step or the full task fails.
3. UI/audio feedback is shown.
4. Reset policy is applied.
5. The task restarts the current step or returns to the task start state.
6. Adaptive counters are updated so the backend can distinguish a clean completion from a completion with repeated recovery.

Suggested policies:

- `ResetObjectOnly`
- `ResetCurrentStep`
- `ResetEntireTask`
- `MarkFailedAndWaitForUserRetry`

Recommended adaptive counters to increment on failure:

- `GlobalMetrics.error_count`
- `GlobalMetrics.step_errors`
- scene metric for specific object failure type

## Reset Behavior

Reset should be deterministic and centralized.

Recommended reset sequence:

1. disable grab/snap interaction temporarily
2. detach object from current parent if needed
3. move object to recorded initial pose
4. zero rigidbody velocity
5. re-enable interaction
6. clear temporary progress for the affected step

If multiple objects are coupled, reset them as a group to avoid half-valid state.

## Event Model

Use events to keep task logic separate from scene-specific interaction scripts.

Suggested events:

- `TaskStarted`
- `TaskStepStarted`
- `TaskProgressChanged`
- `TaskObjectFailed`
- `TaskStepCompleted`
- `TaskCompleted`
- `TaskFailed`
- `TaskReset`
- `TaskMetricObserved`
- `TrackedObjectOutOfBounds`
- `TrackedObjectReset`

This allows:

- checklist UI
- arrows/outlines
- dialogue prompts
- scoring
- telemetry

to subscribe without being hardcoded into task logic.

The same event stream should feed adaptive telemetry collection.

## Adaptive Telemetry Contract

The adaptive system currently expects:

- trajectory samples from `TrajectoryRecorder`
- aggregate metrics from `MetricsAggregator.Aggregate(...)`
- task-specific features as `SceneMetric[]`

The task system should provide the missing task semantics so the adaptive layer knows what to track.

### What To Track Per Task

For every task:

- start time
- end time
- active hand/tool transform trajectory
- steps completed
- prompts shown
- failure count
- reset count
- completion result

For every tracked object:

- number of pickups
- number of drops
- time spent held
- time spent out of allowed zone
- number of resets
- distance traveled from initial pose
- distance error to target pose

For every step:

- step start time
- step completion time
- step failure count
- prompt count during step

### Mapping To `GlobalMetrics`

Use the current adaptive fields as follows:

- `completion_time`: full task duration
- `error_count`: total task failures and object recovery failures
- `prompt_count`: hints, arrows, reminders, dialogue prompts
- `path_efficiency`: from tracked hand/tool trajectory
- `smoothness`: from `FeatureExtractor`
- `hesitation_count`: from trajectory pauses
- `hesitation_total_time`: from trajectory pauses
- `spatial_accuracy`: computed against target object placement or goal position
- `steps_completed`: completed authored steps
- `step_errors`: failures tied to specific steps
- `avg_speed`: from trajectory samples
- `peak_speed`: from trajectory samples

### Mapping To `SceneMetric[]`

Use `SceneMetric` for task-specific or object-specific signals.

Suggested naming scheme:

- `task.reset_count`
- `task.retry_count`
- `task.success`
- `step.{stepId}.completion_time`
- `step.{stepId}.failure_count`
- `object.{objectId}.drop_count`
- `object.{objectId}.reset_count`
- `object.{objectId}.time_held`
- `object.{objectId}.distance_from_start_max`
- `object.{objectId}.out_of_bounds_count`
- `object.{objectId}.target_accuracy`

Example:

```csharp
new SceneMetric
{
    feature_name = "object.watering_can.out_of_bounds_count",
    feature_value = 2f,
    feature_unit = "count"
};
```

## Adaptive Tracking Profile

Each `RehabTaskDefinition` should describe which signals matter for adaptation.

Suggested adaptive tracking profile fields:

- `primaryTrajectoryTarget`
- `secondaryTrajectoryTarget`
- `trackedObjectIds`
- `sceneMetricKeys`
- `accuracyTargetTransform`
- `usePlacementAccuracy`
- `useHoldDuration`
- `useResetCount`
- `useOutOfBoundsCount`

This lets the task system tell the adaptive layer exactly what to record instead of forcing each scene to manually wire telemetry logic.

## Suggested Integration Flow

Scene flow aligned to the current adaptive pipeline:

1. Start session through `AdaptiveApiClient`.
2. Start gameplay task through `TaskManager`.
3. `TaskManager` starts adaptive task execution using the task's `adaptiveTaskId`.
4. `TaskManager` activates `TrajectoryRecorder` on the configured primary tracked transform.
5. `TaskObjectTracker` instances collect object-level counters and failure events.
6. Task completes or fails.
7. `MetricsAggregator` computes `GlobalMetrics`.
8. `TaskAdaptiveBridge` converts task/object/step counters into `SceneMetric[]`.
9. Submit `TaskMetricsRequest`.
10. Apply returned adaptive decision to next task difficulty or assist level.

## Suggested Class Layout

Possible implementation paths:

- `Assets/SimManager/SimManager.cs`
- `Assets/SimManager/TaskSystem/TaskManager.cs`
- `Assets/SimManager/TaskSystem/TaskRuntime.cs`
- `Assets/SimManager/TaskSystem/RehabTaskDefinition.cs`
- `Assets/SimManager/TaskSystem/RehabTaskStepDefinition.cs`
- `Assets/SimManager/TaskSystem/TrackedTaskObject.cs`
- `Assets/SimManager/TaskSystem/TaskObjectTracker.cs`
- `Assets/SimManager/TaskSystem/TaskFailureReason.cs`
- `Assets/SimManager/TaskSystem/TaskEvents.cs`
- `Assets/SimManager/TaskSystem/TaskAdaptiveBridge.cs`
- `Assets/SimManager/TaskSystem/TaskSceneMetricsBuilder.cs`

Optional scene helpers:

- `Assets/SimManager/TaskSystem/Conditions/TriggerCondition.cs`
- `Assets/SimManager/TaskSystem/Conditions/SnapCondition.cs`
- `Assets/SimManager/TaskSystem/Conditions/DistanceCondition.cs`
- `Assets/SimManager/TaskSystem/Conditions/TimerCondition.cs`

## Example Flow

Example: place watering can back on its start stand.

1. Task starts.
2. Watering can initial pose is recorded.
3. Step 1 asks player to grab watering can.
4. Step 2 asks player to carry it to target area.
5. If can falls below `failureFloorY`, task emits `FellOutOfBounds`.
6. Task manager increments error counters and object out-of-bounds counters.
7. Task manager resets the can to its initial place.
8. Current step restarts.
9. If can reaches the valid stand trigger, the step completes.
10. On task end, metrics are converted into `GlobalMetrics` plus object `SceneMetric[]`.

## Inspector Authoring Rules

To keep the system reliable:

- every task object must have a stable `objectId`
- tracked physics objects should have a `Rigidbody`
- failure bounds must be explicit, not implied
- reset policy must be chosen per task or per step
- scene-specific scripts should publish condition events instead of owning full task state
- every adaptive-facing task must declare its `adaptiveTaskId`
- every metric emitted to `SceneMetric` should use stable feature names

## First Implementation Pass

Recommended order:

1. Create `TaskDefinition`, `TaskManager`, and `TaskObjectTracker`.
2. Rename the authoring type to `RehabTaskDefinition` to avoid collision with `AdaptiveSystem.Models.TaskDefinition`.
3. Support only one active task at a time.
4. Capture initial pose for tracked objects.
5. Implement failure checks:
   - below floor threshold
   - too far from start
   - left allowed zone
6. Implement object reset.
7. Add a `TaskAdaptiveBridge` that builds `TaskStartRequest`, `TaskEndRequest`, and `TaskMetricsRequest`.
8. Add one simple step-completion condition based on trigger entry.
9. Add UI event hooks.
10. Migrate one scene task as the pilot implementation.

## Minimal Adaptive Bridge Responsibilities

`TaskAdaptiveBridge` should:

- read the active `RehabTaskDefinition`
- call `AdaptiveApiClient.StartTaskExecutionAsync(...)`
- start and stop one or more `TrajectoryRecorder` instances
- translate runtime counters to `GlobalMetrics`
- build `SceneMetric[]` from tracked objects and steps
- submit metrics at task end
- expose the adaptive decision back to `TaskManager`

## Pilot Candidates

Good first tasks to migrate:

- paper to bin
- pan to stove
- grass or garden pickup/place interaction
- watering bucket return-to-spot task

These are simpler than multi-stage cooking flows and will validate the object tracking and failure-reset loop first.

For the pilot, prefer a task with:

- one primary hand trajectory
- one tracked object
- one obvious failure floor
- one obvious success trigger
- one or two scene metrics beyond `GlobalMetrics`

## Open Questions

- Should failure be step-local or task-global by default?
- Should object reset animate or teleport?
- Do we need separate behavior for hand-held versus snapped objects?
- Should scoring be attached to steps, conditions, or final task completion?
- Should task definitions support left/right handed variants directly?
- Should one task support multiple `TrajectoryRecorder` targets, or should adaptation always focus on one primary limb/tool?
- Which scene metrics are clinically meaningful enough to keep stable across versions?

## Recommended Next Step

Implement the minimal runtime for one pilot task with:

- one tracked object
- one trigger-based success condition
- one floor-based failure condition
- one reset-to-start behavior
- one `TrajectoryRecorder`
- one `TaskMetricsRequest` submission with both `GlobalMetrics` and `SceneMetric[]`

Once that loop is stable, expand to multi-step authored tasks.
