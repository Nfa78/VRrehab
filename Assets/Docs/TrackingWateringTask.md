# Tracking Watering Task

## Goal

For task 1, track the full `water_plants` task and export a per-run JSON summary.

Detailed watering telemetry should be captured mainly for:

- `wp1`
- `wp2`

Core metrics include:

- when each objective starts
- elapsed time until each objective completes
- accuracy based on scored particles vs missed particles
- total water can displacement until the first scored water hit
- total water can displacement during each watering objective
- spill active time and spill activation count
- task-level aggregated totals across both plants

## Tracker Scope

The intended architecture is:

- one tracker per task
- not one global tracker for every gameplay mechanic

For this document specifically:

- task 1 should have its own tracker:
  - `WateringTaskTracker`
- task 2 should later have its own tracker:
  - `SeedsTaskTracker`
- task 3 should later have its own tracker:
  - `CatchLeafsTaskTracker`
- task 4 should later have its own tracker:
  - `RakeLeavesTelemetryTracker` or similar

That means this document is only about the task-1 tracker.

If a shared layer is added later, it should only do orchestration such as:

- discover the active task tracker
- ask it for final metrics
- convert those metrics into backend payloads

It should not own task-specific measurement logic.

## Current Hooks Already Available

- `SimManager` already exposes task and objective lifecycle events.
- `SimTask` and `SimTaskObjective` already store runtime timing and progress state.
- `WaterPlantsTaskDriver` already defines the watering task steps:
  - `pick`
  - `wp1`
  - `wp2`
  - `return_can`
- `FlowerPot` already receives water particle collisions and updates objective progress.
- `WaterSpill` already controls when the water particle system plays.
- `AdaptiveSystem` already supports sending task-level `scene_metrics` to the backend later.

## Important Limitation In The Current Code

The current watering logic is good enough for objective progress, but not yet good enough for telemetry accuracy.

Why:

- `FlowerPot` uses `OnParticleCollision(GameObject other)`, but it does not currently expose raw scored-hit counts.
- `FlowerPot` also throttles accepted hits with `particleHitCooldown`.
- because of that cooldown, "objective progress accepted" is not the same thing as "particle scored"

So if we want a meaningful accuracy metric, we need explicit particle telemetry, not just objective progress callbacks.

## Recommended Implementation

### 1. Add A Dedicated Tracker For Task 1

Create a new component:

- `Assets/Garden Scene/task_drivers/WaterPlants/WateringTaskTracker.cs`

Recommended responsibility:

- track watering-task telemetry only
- subscribe to `SimManager` objective events
- detect when each watering objective becomes active
- record start time
- record completion time
- accumulate water can displacement until first scored hit
- receive particle telemetry callbacks
- keep live aggregated values in serialized runtime fields
- export a JSON result file when the task ends or fails

Recommended serialized references:

- `SimManager simManager`
- `WaterPlantsTaskDriver taskDriver`
- `WaterSpill waterSpill`
- `FlowerPot firstFlowerPot`
- `FlowerPot secondFlowerPot`
- `Transform waterCanTransform`

Recommended runtime fields:

- `bool taskRunActive`
- `string activeObjectiveId`
- `int currentAttemptIndex`
- `WateringTaskReport currentTaskReport`
- `WateringTaskReport lastExportedTaskReport`
- `string lastExportFilePath`
- `string lastExportJson`

### 2. Start Tracking When A Watering Objective Becomes Active

Use `SimManager.LogicalTaskStepChanged` or the active objective on `SimTask`.

When the current objective becomes `wp1` or `wp2`:

- reset that objective report if it is a restarted attempt
- set `activeObjectiveId`
- record objective start against `simManager.CurrentClock`
- cache the current can position as the displacement baseline

This should be treated as the source of truth for objective-start timing.

### 3. Track Objective Completion Time

When `SimManager.LogicalTaskObjectiveCompleted` fires for a watering objective:

- record completion against `simManager.CurrentClock`
- compute:

`elapsedSeconds = wp1CompletedAtClock - wp1StartedAtClock`

Note:

- at the current default difficulty, `requiredWateringSecondsPerPlant` is `3`, so first scored hit and objective completion are intentionally separate metrics
- completion now depends on accumulated successful watering time, not a single scored hit

### 4. Track Water Can Displacement Until The First Scored Hit

While the active watering objective is running and `firstHitRegistered == false`:

- sample `waterCanTransform.position` every frame in `Update()`
- accumulate path length:

`displacement += Vector3.Distance(currentPosition, lastTrackedCanPosition)`

- update `lastTrackedCanPosition`

When the first scored hit arrives:

- freeze the accumulated value
- store it as:

`canDisplacementToFirstHitMeters`

This should measure actual path traveled, not straight-line distance.

### 5. Add Raw Scored-Hit Telemetry In `FlowerPot`

Current `FlowerPot` behavior should remain for gameplay progress, but telemetry needs an additional signal.

Recommended change in:

- `Assets/Garden Scene/task_drivers/WaterPlants/FlowerPot.cs`

Add a telemetry event or callback for raw water scoring, for example:

- `event Action<FlowerPot, int> WaterParticlesScored`

Or a tracker callback method such as:

- `NotifyWaterParticleScored(FlowerPot pot, int rawHitCount)`

Important:

- do not use only `updated == true` from objective progress as the scored count
- progress acceptance is throttled by cooldown
- telemetry should count raw scoring interactions separately from task progression

### 6. Add Missed-Particle Telemetry

This is implemented in the current MVP as an estimated value:

- emitted particles are estimated from `ParticleSystem.emission.rateOverTime`
- spill-active seconds are accumulated while the watering objective is active
- missed particles are derived from emitted estimate minus scored particles

This is fast to ship, but it is still an estimate rather than per-particle truth.

## Recommended Accuracy Definition

For `wp1`:

`accuracy = scoredParticleCount / (scoredParticleCount + missedParticleCount)`

This means we need both:

- how many water particles scored on plant 1
- how many water particles were emitted during `wp1` but never scored on plant 1

## Recommended Way To Count Missed Particles

### Preferred Approach: Dedicated Particle Telemetry

Create a helper component such as:

- `Assets/Garden Scene/task_drivers/WaterPlants/WaterParticleTelemetry.cs`

Responsibilities:

- count particles emitted while `wp1` is active
- mark particles that entered the plant-1 scoring volume
- count particles that expired without scoring as missed

Implementation direction:

- attach it to the same particle system created by `WaterSpillSetup`
- track emitted particles during objective `wp1`
- use a dedicated telemetry volume for plant 1
- treat "entered plant-1 score volume" as scored
- treat "died without ever entering plant-1 score volume" as missed

This is the most defensible version if the metric will later influence adaptation.

### Lighter MVP: Estimated Emission Count

If a first pass is needed quickly:

- estimate emitted particles from particle emission rate and active spill time
- count scored particle collisions separately
- compute missed as:

`missed = emittedEstimate - scored`

This is simpler but lower fidelity, especially if the spill rate changes or particles collide in ways that do not map cleanly to scoring.

## Recommended Scoring Rule

For telemetry, "scored particle" should mean:

- particle reached the plant-1 scoring target while `wp1` is active

It should not mean:

- particle caused accepted objective progress

That distinction matters because:

- progress is throttled by cooldown
- required hits per plant can change with difficulty

## Output Model

Add a small runtime data model, for example:

```csharp
public sealed class WateringObjectiveTelemetry
{
    public string objectiveId;
    public float startedAtClock;
    public float completedAtClock;
    public float elapsedSeconds;
    public float firstHitSecondsFromStart;
    public float canDisplacementToFirstHitMeters;
    public int emittedParticles;
    public int scoredParticles;
    public int missedParticles;
    public float accuracy;
}
```

The current implementation also wraps those objective-level values in a task-level report that includes:

- local and UTC task dates
- task title and description
- attempt index
- difficulty details
- task outcome
- aggregated totals across `wp1` and `wp2`

## JSON Export

The current tracker writes one JSON file per task run to:

- `Application.persistentDataPath/TaskTracking/WateringTask/yyyy-MM-dd/`

Filename pattern:

- `WateringTask_yyyyMMdd_HHmmss_completed.json`
- `WateringTask_yyyyMMdd_HHmmss_failed.json`

That JSON contains:

- date and timestamp fields
- task identity and difficulty fields
- full objective reports for `pick`, `wp1`, `wp2`, and `return_can`
- aggregated task totals for scored, missed, wrong-target, displacement, spill time, and accuracy

The same values also stay available in real time on the component through serialized runtime fields:

- `currentTaskReport`
- `lastExportedTaskReport`
- `lastExportFilePath`
- `lastExportJson`

## Scene Metrics To Publish Later

When backend wiring is added, convert the result into `SceneMetric[]` entries such as:

- `wp1_time_to_complete_seconds`
- `wp1_first_hit_seconds`
- `wp1_scored_particles`
- `wp1_missed_particles`
- `wp1_accuracy`
- `wp1_can_displacement_to_first_hit_meters`

These can later be attached to:

- `TaskMetricsRequest.scene_metrics`

## Files That Should Change

Recommended new file:

- `Assets/Garden Scene/task_drivers/WaterPlants/WateringTaskTracker.cs`

Later, for the broader architecture, similar files should exist per task:

- `Assets/Garden Scene/task_drivers/Seeds/SeedsTaskTracker.cs`
- `Assets/Garden Scene/task_drivers/CatchLeafs/CatchLeafsTaskTracker.cs`
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakeLeavesTelemetryTracker.cs`

Recommended modified files:

- `Assets/Garden Scene/task_drivers/WaterPlants/FlowerPot.cs`
- `Assets/Garden Scene/task_drivers/WaterPlants/WaterSpill.cs`
- `Assets/Garden Scene/task_drivers/WaterPlants/WaterSpillSetup.cs`
- `Assets/Garden Scene/Garden.unity`

Optional later integration:

- `Assets/SimManager/TaskSystem/TaskAdaptiveBridge.cs`
- or a separate gameplay telemetry bridge that submits `scene_metrics`

## Edge Cases To Handle

- if `wp1` restarts, all `wp1` counters must reset
- if the simulation pauses, use `simManager.CurrentClock` so paused time is excluded consistently
- if the user waters plant 2 while `wp1` is active, decide whether that counts as a miss for `wp1`
- if `requiredWateringSecondsPerPlant` changes, first hit and completion time remain separate metrics
- if the can is dropped and the task resets to a milestone, tracker state must reset with it

## Recommended Build Order

1. Implement `WateringTaskTracker` with objective start, completion, and can displacement tracking.
2. Add a raw scored-hit telemetry signal in `FlowerPot`.
3. Add missed-particle telemetry.
4. Log the final `wp1` metrics in the editor for validation.
5. Convert the result into `scene_metrics`.
6. Wire backend submission after the local values are stable.

## Open Questions To Decide Before Final Wiring

- Does "missed" include water that hits plant 2 while `wp1` is active?
- Should scored telemetry be based on the current pot collider, or a dedicated telemetry volume for plant 1?
- Should displacement start exactly when `wp1` becomes active, or only when the player starts spilling?

## Recommended Answer To Those Questions

Unless there is a rehab-specific reason to do otherwise:

- count plant-2 hits during `wp1` as misses for `wp1`
- use a dedicated plant-1 telemetry score volume for clean particle accounting
- start displacement when `wp1` becomes active
