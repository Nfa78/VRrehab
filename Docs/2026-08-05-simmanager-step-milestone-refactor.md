# 2026-08-05 SimManager Step/Milestone Refactor

## Summary

The `SimManager` task model is being simplified from a more generic task/objective framework into a smaller step/milestone runtime with task-specific drivers.

The new target shape is:

- a small `SimManager` that sequences tasks and handles retries
- a small `SimTask` definition that stores task identity plus ordered steps
- a small `SimTaskObjective` step model that stores step id, label, progress mode, and milestone flag
- one driver per task flow that owns the scene-specific gameplay logic

This keeps the reusable layer focused on:

- task start/stop
- current step tracking
- milestone tracking
- retry/reset to milestone
- task completion/failure

Scene logic stays in task drivers.

## Why This Refactor

The project currently has a small number of task flows and they are not all shaped the same way:

- kitchen cooking flow is highly bespoke
- paper bin flow is bespoke
- window cleaning flow is bespoke
- garden tasks already use `SimManager`, but each task still has custom interaction logic

For this task count, a broad reusable task framework adds more generic code than value. The previous direction pushed too much behavior into the shared layer:

- objective orchestration
- prerequisite rollback logic
- tracked object recovery policy
- task/objective specific workflow in generic classes

That makes the core harder to reason about and does not reduce the amount of task-specific scene code.

## New Direction

### Core Contract

Each task should be modeled as:

- `taskId`
- `title`
- ordered `steps`

Each step should be modeled as:

- `stepId`
- `title`
- progress mode
- target value
- `isMilestone`

Milestones are retry anchors:

- if failure happens after a milestone step, retry resumes from that milestone step
- if no milestone has been reached yet, retry resumes from step `0`

### Driver Contract

Each task gets its own driver script.

The driver is responsible for:

- scene references
- enabling/disabling interactables
- reacting to trigger/collision/grab signals
- marking steps complete or advancing progress
- asking the manager to reset/fail/complete when needed

The manager is not responsible for gameplay details.

## Expected Effects

### What Goes Down

- generic orchestration code
- cross-task abstraction pressure
- logic hidden inside shared objective helpers
- coupling between unrelated task flows

### What Goes Up

- task-specific driver code
- explicit scene flow code per task
- direct mapping between gameplay steps and runtime state

This is the intended tradeoff.

## Code Footprint Expectation

Yes, this refactor should reduce the general reusable code footprint.

It should also introduce more specified code per task.

That is a good trade in this project because:

- there are only a few tasks
- the tasks have different interaction rules
- task-specific code is easier to read when it stays near the task
- future extraction can happen later if multiple drivers converge on the same pattern

In short:

- less generic code
- more local task code
- lower architectural overhead
- better fit for the current project scope

## Current Task Mapping Target

### Garden

- `water_plants`
  - step: `pick`
  - step: `wp1`
  - milestone: `wp1`
  - step: `wp2`
  - milestone: `wp2`
  - step: `return_can`

- `throw_seeds`
  - step: `pick_seeds`
  - milestone: `pick_seeds`
  - step: `throw_seeds`
  - milestone: `throw_seeds`
  - step: `return_bucket`

### Kitchen

- open drawer
- grab/place pan
- grab/place carrot
- pan on stove
- carrot on board
- carrots in pan
- oil on carrots
- stove on
- stir carrots
- stove off / win

### Cleaning / Other Scene Tasks

- paper in bin
- remove spots on window

## Migration Notes

- Existing `SimObjectiveInteraction` calls should continue to work by targeting step ids.
- Existing scene-specific scripts should be wrapped or upgraded into task drivers instead of being forced into generic manager code.
- Garden scene task data should remain usable during migration by treating existing objective entries as ordered steps.
- Debug and adaptive hooks should follow the lighter step/milestone state, not reintroduce heavy objective orchestration.

## Implementation Goal For This Update

This update should:

1. add the refactor documentation
2. slim the manager/task runtime toward steps + milestones
3. keep compatibility for current garden integrations
4. add driver-style mappings for the currently implemented bespoke tasks
