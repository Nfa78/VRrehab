# Quest seed, bucket, and hoe return findings

Date: 10 November 2026  
Device: Meta Quest  
Scope: Garden seed-throwing, bucket return, seed grabbing, and hoe return flows.

This document records the issues observed during Quest testing and the corresponding code and scene findings. No runtime code or scene configuration was changed as part of this investigation.

## 1. Bucket can remain mid-air after throwing seeds

### Observed issue

After the seed-throwing task, the bucket may remain suspended at the position where it was released instead of returning to its initial place.

### Finding

The `throw_seeds` task currently contains only these objectives:

- `pick_seeds`
- `throw_seeds`

It has no `return_bucket` objective. The scene does contain a `returnStepId: return_bucket` field on `SeedsTaskDriver`, but there is no matching objective in the seed task configuration.

The seed task tracks the bucket's start pose, but that tracked-object state is configured for reset handling, not for snapping the bucket back after task completion:

- `captureStartPose: 1`
- `restoreParentOnReset: 0`

The existing bucket return indicator belongs to the leaf-catching task. Its `TSObjectiveReturnZone` is configured for objective `return_bucket` and is connected to the `catch_leafs` task, not `throw_seeds`.

Relevant references:

- `Assets/Garden Scene/Garden.unity`: `throw_seeds` objectives and tracked bucket configuration.
- `Assets/Garden Scene/Garden.unity`: `BucketReturnIndicator` with `TSObjectiveReturnZone`.
- `Assets/Garden Scene/task_drivers/Seeds/TaskLogic/SeedsTaskDriver.cs`: declared `returnStepId`, but no seed-task return objective exists in the scene.
- `Assets/Garden Scene/task_drivers/CatchLeafs/BucketLeafCatchSystem.cs`: bucket grab/return gating is owned by the leaf-catching task driver.

### Recommended resolution

Add a seed-task `return_bucket` objective that becomes active after `throw_seeds`, then add or configure a bucket return zone for that task. The zone should:

- accept the `Bucket` tag;
- use a `TSTag` scope-compatible trigger;
- reference the seed task's `return_bucket` objective;
- use a snap anchor at the bucket's initial position;
- clear velocity and disable grabbing after a successful return.

The seed and leaf-catching flows should not silently share one zone unless the return-zone implementation explicitly supports both task contexts. Otherwise, use separate indicators/zones with unambiguous objective ownership.

### Proposed implementation

#### A. Add the missing seed return objective

Extend the `throw_seeds` task in `Garden.unity` with:

```text
objectiveId: return_bucket
mode: Boolean
activeRequiresCompletedObjectiveIds:
- throw_seeds
highlightTarget: BucketReturnIndicator
```

The existing `SeedsTaskDriver.returnStepId` already uses the same ID, so the scene and driver are already aligned at the naming level. The task's `autoCompleteWhenAllRequiredObjectivesComplete` setting can then complete the task only after the bucket is returned.

#### B. Reuse the existing zone only if the task gate is made task-agnostic

The existing `BucketReturnIndicator` already has the correct broad return-zone ingredients: `Bucket` tag filtering, a `return_bucket` objective ID, a trigger radius, and a snap anchor. Its `TSObjectiveReturnZone` can complete the current task's `return_bucket` objective because it asks the current task driver to complete the objective by ID.

However, the current `BucketLeafCatchSystem` disables the bucket's grab components whenever the active task is not `catch_leafs`. That means adding the seed objective alone is insufficient: the user may be unable to grab the bucket during the new seed return step.

The preferred solution is to separate bucket interaction gating from leaf-catching logic:

1. Let a generic bucket interaction gate enable the bucket while either the catch-leaf flow or a current bucket-return objective is active.
2. Keep leaf visibility, catch-volume processing, and leaf progress owned by `BucketLeafCatchSystem`.
3. Keep the actual return completion and snap action owned by `TSObjectiveReturnZone`.
4. Disable the bucket interactables only after the return zone has completed its objective.

An alternative is to add an explicit seed-return allowance to `BucketLeafCatchSystem`, but that couples the catch-leaf component to another task and will become harder to maintain if more bucket tasks are added.

#### C. Use a separate seed indicator if task ownership must remain isolated

If the leaf-catching indicator must remain exclusive to `catch_leafs`, create a second indicator such as `SeedBucketReturnIndicator` with:

- `TSTag` scope;
- accepted tag `Bucket`;
- `SimObjectiveInteraction.objectiveId = return_bucket`;
- a `TSObjectiveReturnZone` whose snap anchor is the bucket's initial placement;
- a visual highlight target referenced by the seed task.

The two zones can share the same objective ID because only the active task should accept completion, but separate objects make scene ownership and debugging clearer.

### Why this solution is preferable

It fixes the complete flow rather than only moving the bucket after the throw. The user gets a visible next objective, the bucket remains grabbable during that objective, the zone validates the physical placement, and the return action clears velocity and stabilizes the object. It also preserves the existing catch-leaf return implementation instead of adding seed-specific snap code to the seed throw system.

## 2. Seed grabbing has a Quest hand-tracking edge case

### Observed issue

Grabbing seeds on Quest can produce an intermittent or unexpected state transition. The issue is most noticeable when hand tracking briefly changes, when the user moves between hands, or when the hand leaves/re-enters the pickup area.

### Finding

`SeedInteraction` is a polling state machine:

1. It selects an object from a `HashSet<GameObject>` of targets inside the seed trigger.
2. It resolves the hand by inspecting the target's parent names for `left` or `right`.
3. It treats `Hand.Pose.Grab` or `Hand.Pose.Pinch` as holding.
4. `SeedThrowingSystem` interprets `GrabbingHand == null` as a release and immediately starts the throw.

There are several Quest-sensitive weak points:

- The active target is selected by iterating a `HashSet`, so selection is not deterministic if more than one target is inside the radius.
- Left/right resolution depends on transform names. If no parent name contains `left`, the method falls back to the right hand.
- While `SeedsLoaded`, if the active hand temporarily cannot be resolved, `SeedInteraction.Update()` returns without explicitly resetting the loaded state.
- `SeedThrowingSystem` can then see `GrabbingHand == null` and treat a tracking interruption as a release, using its cached `releasedHand` for the throw.
- The same seed interaction remains allowed during both `pick_seeds` and `throw_seeds`, so a hand re-entry or re-grab can occur during the throw step unless the state is explicitly guarded.

Relevant references:

- `Assets/Garden Scene/task_drivers/Seeds/TaskLogic/SeedInteraction.cs`: target selection, hand resolution, and `SeedsLoaded` transitions.
- `Assets/Garden Scene/task_drivers/Seeds/TaskLogic/SeedThrowingSystem.cs`: release detection and burst spawning.
- `Assets/Garden Scene/Garden.unity`: the seed `TSParasite` detects `HandAnchor` targets and the `SeedInteraction` task-driver field is auto-resolved at runtime.

### Recommended resolution

Harden the seed flow for temporary tracking loss:

- bind the selected seed target to a specific hand identity rather than repeatedly resolving it from transform names;
- make target selection deterministic and prefer the nearest valid target;
- distinguish `tracking lost` from `released`;
- cancel or pause the loaded state when the hand source becomes invalid;
- only spawn a throw after a confirmed release transition from the same hand;
- block re-entry until the current throw has finished or the state has been reset;
- add development logs for hand identity, pose, tracking validity, and release reason.

The previous hand-grab improvement in `HandsResolver` does not remove these seed-specific state and hand-identity risks because `SeedInteraction` still performs its own target-to-hand resolution.

### Proposed implementation

Use an explicit seed interaction state machine with a confirmed release event:

1. **Select a hand once.** When seeds enter the pickup radius, resolve the nearest valid hand and store its identity. Do not switch hands while seeds are loaded.
2. **Use a stable target.** Select the nearest valid target deterministically instead of taking the first entry returned by a `HashSet`.
3. **Separate tracking loss from release.** If the selected hand becomes temporarily invalid, start a short grace timer. Do not interpret the first invalid frame as release.
4. **Confirm release from the same hand.** Require a valid open-hand transition, or a tracked hand-pose transition, before emitting a `SeedsReleased` event.
5. **Drive throwing from that event.** `SeedThrowingSystem` should spawn the burst only from the confirmed release event, not merely because `GrabbingHand` became null.
6. **Reset safely.** Clear the loaded state on task-step changes, disable, hand-loss timeout, and completed throw. A tracking loss should cancel or pause the pickup rather than launch a stale cached hand.
7. **Instrument Quest-only transitions.** Log the selected hand, target, pose, tracking validity, loss duration, release reason, and whether the throw was confirmed or cancelled.

Suggested starting values for a first Quest pass:

- tracking-loss grace period: `0.10` to `0.20` seconds;
- release confirmation: two consecutive valid open-hand samples;
- no target switching while loaded;
- no throw if the hand source changes during the grace period.

### Why this solution is preferable

The current implementation uses `null` as both “the user released the seeds” and “the hand could not be resolved this frame.” Those are different physical events on a headset. Making release explicit prevents a tracking dropout from becoming an unintended throw and prevents a stale `releasedHand` from being used after the user's hand identity changes.

## 3. Hoe return has no return-zone object

### Observed issue

The hoe return step needs a visible and reliable return zone, consistent with the watering-can and bucket flows.

### Finding

The `return_hoe` objective currently highlights the hoe itself. There is no `TSObjectiveReturnZone` configured for that objective in the Garden scene.

Instead, `RakeLeavesTaskDriver` checks the hoe's distance from its captured initial pose after it detects that the hoe is no longer grabbed. If the distance is within `0.35` metres, it completes the objective and optionally snaps the hoe to the captured pose.

This is a pose-distance check rather than a physical return-zone interaction. It provides no dedicated zone trigger, no zone-specific visual target, and can be sensitive to release timing and Quest tracking jitter.

Relevant references:

- `Assets/Garden Scene/Garden.unity`: `return_hoe` has the hoe as its highlight target and no return-zone object.
- `Assets/Garden Scene/task_drivers/RakeLeaves/RakeLeavesTaskDriver.cs`: `TryCompleteReturnHoeStepFromPose()` and `ApplyReturnedHoePose()`.
- `Assets/Garden Scene/Garden.unity`: the hoe root is tagged `Hoe`, so it can be detected by a return zone configured for that tag.

### Recommended resolution

Add a dedicated `HoeReturnIndicator` containing:

- a `TSParasite` with `TSTag` scope;
- accepted object tag `Hoe`;
- a `SimObjectiveInteraction` with objective ID `return_hoe`;
- a `TSObjectiveReturnZone` using the hoe's original placement as its snap anchor.

One system should own completion and snapping. If the return zone becomes authoritative, the distance-based return check in `RakeLeavesTaskDriver` should be disabled or reduced to a fallback so both systems cannot race to complete and reposition the hoe.

### Proposed implementation

1. Add a `HoeReturnIndicator` near the original hoe placement.
2. Give it a `TSParasite` configured with `TSTag` scope and accepted tag `Hoe`.
3. Add `SimObjectiveInteraction` with objective ID `return_hoe`.
4. Add `TSObjectiveReturnZone` with `snapAnchor` set to the hoe's intended placement.
5. Change the `return_hoe` objective's `highlightTarget` from the hoe to the indicator.
6. Set a forgiving Quest detection radius, initially around `0.35` to `0.45` metres, and tune it in-headset.
7. Make the return zone the authoritative completion path. It should clear velocity, make the rigidbody kinematic, snap rotation, disable the hoe interactable, and then complete the objective.
8. Keep the existing distance check only as a diagnostic or emergency fallback, disabled by default. If it remains enabled, it must not snap or complete after the zone has already processed the return.

### Why this solution is preferable

A return zone gives the user a concrete spatial target and gives the task system a reliable trigger independent of one-frame release timing. It also makes the hoe flow consistent with the watering-can and bucket flows, improves objective highlighting, and gives Quest testing a measurable detection boundary.

## Recommended implementation order

1. Add the seed `return_bucket` objective and make bucket grabbing available during that objective.
2. Reuse or add the bucket return zone and verify that it completes the seed task before changing throw mechanics.
3. Harden seed hand selection and confirmed-release handling.
4. Add the hoe return indicator and make it the single completion/snap authority.
5. Build a fresh Quest APK and test each flow independently before testing the complete Garden sequence.

## Quest validation checklist

After implementing the return zones and seed-state hardening, verify on the headset:

- The seed task advances from `throw_seeds` to `return_bucket`.
- Releasing the bucket after throwing does not leave it suspended in mid-air.
- The bucket is detected, snapped, and made stable in its seed return zone.
- Pinch and full-hand grab both work for seeds with either hand.
- A temporary tracking loss does not create an unintended seed throw.
- Moving a hand out of the seed area does not leave the seed flow permanently loaded.
- The hoe return zone visibly activates only for `return_hoe`.
- Releasing the hoe inside the zone completes the objective and snaps it to its original placement.
- Releasing the hoe outside the zone does not complete the return objective.
- The completed Quest APK contains the latest scene and script changes.

## Quest follow-up observations — 10 November 2026

### 1. Bucket return position is offset

#### Observed issue

After the bucket return was changed to wait for release, the bucket moved smoothly but ended at an offset position instead of its intended initial placement.

#### Finding

The bucket return zone currently uses the `BucketReturnIndicator` transform as its explicit `snapAnchor`. The authored bucket root position and the indicator anchor are not the same:

- Bucket root: approximately `(-11.789, 0.700, 6.463)`.
- Bucket return anchor: approximately `(-11.728, 0.695, 6.644)`.

This is an offset of approximately `0.19` metres, mainly along the Z axis. The smooth-return code is therefore reaching the configured anchor correctly; the configured anchor itself is not the bucket's true initial pose.

#### Recommended correction

Create a dedicated `BucketReturnSnapAnchor` at the exact authored bucket placement, including rotation, and assign that transform to `TSObjectiveReturnZone.snapAnchor`. The visual return indicator can remain offset or be repositioned independently; it should not also serve as the physical snap pose unless the two poses are intentionally identical.

Keep the release gate introduced for the bucket. The corrected sequence should be:

```text
Bucket released inside return radius
    ↓
Return objective completes
    ↓
Bucket clears velocity
    ↓
Bucket smoothly moves to BucketReturnSnapAnchor
```

#### Motivation

Separating the visual indicator from the physical snap anchor prevents future changes to the indicator's artwork, scale, or floor placement from moving the returned bucket. It also makes the intended placement explicit and easier to tune on Quest.

### 2. Hoe raking task completes after too few leaves

#### Observed issue

The hoe task ended after only a small number of leaves had visibly been raked.

#### Finding

The current level-1 rake profile requires only six accepted leaves:

- Level 1: `requiredRakedLeaves: 6`
- Level 2: `requiredRakedLeaves: 8`
- Level 3: `requiredRakedLeaves: 12`

The runtime applies this profile value to the `rake_leaves` objective. Each field leaf can contribute one progress point when the rake sweep moves it, and the task completes as soon as the configured count is reached. Therefore, seeing approximately six leaves before completion is consistent with the current configuration rather than a return-zone issue.

#### Recommended correction

Choose the required count based on the intended therapy flow and the number of visible leaves. For example, if the user should rake most of the initial field of twelve leaves:

- set level 1 to approximately `10` or `12` required leaves;
- increase higher levels proportionally, for example `12` and `16` if the scene contains enough leaves;
- verify that the visible field contains at least as many distinct raked leaves as the objective requires.

The objective should continue to count distinct leaves once each. The current `RakeableLeaf` state already prevents a leaf from being counted repeatedly after it enters its raked/pile state.

#### Motivation

Increasing the required count makes the task duration and visible effort match the user-facing instruction. It also avoids a mismatch where the scene presents a larger field but the objective ends after only the first few successful sweeps.

#### Validation points

- Confirm the intended number of visible leaves on Quest.
- Confirm one leaf contributes one progress point.
- Confirm a single long rake sweep cannot count the same leaf repeatedly.
- Confirm the task completes at the selected threshold and not earlier.
- Tune the threshold separately for each difficulty profile rather than relying on the level-1 value for every user.
