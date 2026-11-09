# Seed throwing with tracked hands — implementation plan

Date: 11 October 2026  
Scope: Garden seed task only (`SeedPickupState`, `SeedPickupThrowSystem`, and `SeedThrowSpawner`).

## Outcome

Make a pinch-release seed throw feel intentional with bare Quest hand tracking, while retaining controller support and the current task/gate/scoring flow.

The release must primarily reflect real hand motion. When tracking cannot produce a trustworthy velocity, it must use a small, visible assisted toss rather than silently dropping seeds at the hand. The system must record whether the release was measured or assisted so therapy analytics do not mistake an assist for patient movement.

## Current behaviour and confirmed failure mode

The current implementation is a custom burst-spawn system, not Meta Interaction SDK's built-in throwable flow:

1. `SeedPickupState` detects an index pinch or grab while a hand is in the seed radius.
2. `SeedPickupThrowSystem` samples the `ActiveHand` transform for up to six `Update` frames over 0.12 seconds.
3. On release it estimates linear/angular velocity from consecutive transform samples.
4. `SeedThrowSpawner` creates ten individual Rigidbody seeds and applies that estimated velocity.

The failure is in step 2/3. The sampled `HandAnchor` is not guaranteed to be the hand-tracked palm/wrist pose that visually moves in the headset. When the estimate is below `minimumMeasuredReleaseSpeed` (0.15 m/s), the spawner deliberately substitutes `minimumFallbackLaunchSpeed` (currently 0.35 m/s) along `hand.forward`.

At normal hand height, 0.35 m/s is a near-drop: gravity dominates before the seeds have travelled a useful distance. This is not primarily a Rigidbody mass, drag, or collision problem.

Relevant implementation files:

- `Assets/Garden Scene/task_drivers/Seeds/TaskLogic/SeedPickupState.cs`
- `Assets/Garden Scene/task_drivers/Seeds/TaskLogic/SeedPickupThrowSystem.cs`
- `Assets/Garden Scene/task_drivers/Seeds/PhysicsHelpers/SeedThrowSpawner.cs`
- `Assets/Garden Scene/task_drivers/Seeds/PhysicsHelpers/SeedPhysicsSettings.cs`

## Design decision

Keep the gesture-based pickup and burst-spawn design. Do not turn each seed into a held `Grabbable`; that would add interaction complexity and runtime objects without improving the therapeutic gesture.

Use a dedicated, local `SeedThrowVelocityEstimator` that follows the same principles as Meta's throwing system:

- source a true tracked hand/palm pose;
- record timestamped pose samples after tracking has updated;
- estimate release velocity from a buffered history rather than one frame;
- compensate for the delay between the throwing motion and pinch-open recognition;
- reject low-confidence or implausible readings; and
- keep the flight tuning separate from velocity measurement.

Meta's installed Interaction SDK already includes `HandPoseInputDevice`, `StandardVelocityCalculator`, `RANSACVelocityCalculator`, `Grabbable`, `VelocityThrow`, `ThrowTuner`, and `ThrowPhysicsProfile`. The older calculator APIs are marked obsolete in the installed package, so they are a behavioural reference, not a dependency for this custom mechanic. The production seed code should not couple to obsolete Meta types.

## Architecture

### 1. Introduce a hand-pose source boundary

Create `SeedThrowPoseSource` (or a small interface nested beside the estimator) whose sole responsibility is to provide the current release pose and its confidence.

Suggested result type:

```csharp
public readonly struct SeedThrowPose
{
    public readonly Pose Pose;
    public readonly bool IsTracked;
    public readonly bool IsHighConfidence;
    public readonly SeedThrowPoseKind Kind;
}
```

`SeedThrowPoseKind` should distinguish `TrackedPalm`, `HandInteractor`, `ControllerAnchor`, and `AnchorFallback`. This will make headset diagnostics unambiguous.

Resolution priority:

1. The active `OVRHand` / `OVRSkeleton` palm or wrist pose, only while `IsTracked`, `IsDataValid`, and high confidence are true.
2. The active `HandGrabInteractor` pose when it is the live hand interaction source.
3. The controller hand anchor when controllers are active.
4. The existing `ActiveHand` anchor only as a final compatibility fallback.

Expose a narrow public method on `SeedPickupState`, such as `TryGetActiveThrowPose(out SeedThrowPose pose)`. Do not let the estimator search the entire scene each frame. `SeedPickupState` already resolves the active hand and input sources, so it owns this decision.

### 2. Add `SeedThrowVelocityEstimator`

Add the estimator to the same GameObject as `SeedPickupThrowSystem`. It is inactive until seeds have been picked up and is reset after every release, task reset, hand switch, disable, or tracking-loss transition.

It stores a fixed-size ring buffer of samples:

```csharp
private readonly struct Sample
{
    public readonly Pose Pose;
    public readonly float Time;
    public readonly bool HighConfidence;
}
```

Sampling requirements:

- Record samples in `LateUpdate` so the estimator runs after normal hand-pose updates.
- Use `Time.unscaledTime` for timestamps.
- Target a 0.15 s history, with a maximum of 12 samples. This is enough for a natural flick at 72–90 Hz without making the throw lag behind the user.
- Ignore duplicate timestamps, non-finite positions/rotations, and samples where the source reports invalid tracking.
- Clear the buffer when the source kind changes. Never combine a controller pose with hand-tracking samples.

The estimator exposes `BeginCapture`, `Capture`, `EstimateRelease`, and `Reset`. It must allocate neither managed memory nor log during normal play.

### 3. Estimate at the intended point of release

Pinch-open is observed after the physical release gesture. Estimate from a short step-back point rather than from the newest pose alone.

Initial algorithm:

1. On `Released`, select samples centred around `releaseTime - 0.07 s`.
2. Calculate a robust linear velocity from the position difference across a 40–70 ms neighbourhood.
3. Calculate angular velocity from the same two rotations.
4. Reject the result if either source confidence is low, the time range is invalid, or linear speed exceeds a safety ceiling before clamping.
5. Retain both the instantaneous result and a trend result across the preceding 100–150 ms.
6. Blend them by configuration, initially 65% step-back instantaneous and 35% trend.

Do not select the single fastest sample blindly; hand-tracking noise can create false speed spikes. A median/trimmed derivative or capped blend is preferable. Angular velocity may later be used to add plausible spin, but it must not determine the launch direction in the first rollout.

Return a `SeedThrowRelease` value containing measured linear velocity, angular velocity, pose, source kind, confidence, and diagnostic flags. `SeedPickupThrowSystem` then passes this to the spawner.

### 4. Separate measurement from launch assistance

Extend `SeedThrowSpawner` with a release-policy method rather than embedding more fallback behaviour in `ResolveLaunchVelocity`.

Launch policy order:

1. **Measured release:** use the estimator's measured velocity when it is valid and above `minimumMeasuredReleaseSpeed`.
2. **Weak but valid release:** retain its measured direction, but blend its speed toward a configurable clinical minimum. This makes a small intended flick visible without replacing the direction.
3. **Invalid/no tracking:** use a controlled assisted toss along the tracked palm's pointing direction, not the generic anchor forward. Clamp downward pitch so a valid release cannot be launched straight into the floor.
4. Clamp linear and angular speed, apply existing seed spread, then assign Rigidbody velocities exactly once.

The first playable tuning pass should use these conservative starting values, all serialized on the spawner or a new `SeedThrowSettings` asset:

| Setting | Start | Tuning range | Purpose |
| --- | ---: | ---: | --- |
| Pose history | 0.15 s | 0.12–0.20 s | Captures a flick without excessive latency. |
| Release step-back | 0.07 s | 0.05–0.10 s | Compensates for pinch-release recognition delay. |
| Valid measured speed | 0.25 m/s | 0.15–0.40 m/s | Separates motion from tracking noise. |
| Weak-release assisted minimum | 1.25 m/s | 0.8–1.8 m/s | Lets a gentle clinical throw visibly travel. |
| No-tracking assisted minimum | 1.8 m/s | 1.5–2.5 m/s | Avoids the present straight-down drop. |
| Maximum launch speed | 4.5 m/s | 3–6 m/s | Keeps the task safe and targetable. |
| Cone half-angle | 6 degrees | 3–10 degrees | Keeps a seed burst readable rather than random. |

These are starting points, not clinical thresholds. Final values must be chosen from on-device trials at the Garden's actual gate distances.

### 5. Preserve task and analytics semantics

Keep the existing event sequence:

`SeedPickupState.PickedUp` → task pick completion → capture starts  
`SeedPickupState.Released` → burst spawn → `ThrowBurstSpawned` → gate/scoring/tracker updates

Add a `SeedThrowTelemetry` payload/event for development and tracker use. It should include:

- source kind;
- tracking/confidence validity;
- raw measured speed;
- final launch speed;
- whether assistance was used;
- release direction; and
- number of spawned seeds.

For rehabilitation reporting, retain raw measured speed and final assisted speed separately. A successful assisted toss must not inflate movement-quality metrics.

## Implementation sequence

### Phase 0 — baseline and instrumentation

Before changing flight behaviour, build a development APK and capture 20 throws per hand:

- five normal throws;
- five gentle throws;
- five throws while turning the wrist;
- five throws near the edge of camera visibility.

Log only at release: source kind, confidence, sample count, measured speed, final launch speed, direction, and assist flag. Record whether each burst reaches a useful horizontal distance and whether it passes a gate.

This establishes whether the current `HandAnchor` is static, delayed, or merely under-sampled.

### Phase 1 — pose source

1. Add `TryGetActiveThrowPose` to `SeedPickupState`.
2. Resolve the `OVRHand` and skeleton/palm pose already associated with the active hand; avoid `FindObjectsByType` in the release loop.
3. Add controller-anchor fallback and preserve left/right handling.
4. Add development-only source/confidence reporting.
5. Verify source switching by starting with controllers, setting them down, then using bare hands.

Exit criterion: each release report identifies a tracked palm/wrist source for hand tracking and a controller anchor for controllers.

### Phase 2 — estimator in shadow mode

1. Add `SeedThrowVelocityEstimator` and sample only while `SeedsLoaded` is true.
2. Keep the existing launch computation active.
3. At every release, calculate both legacy and new estimates, but use only legacy for flight.
4. Compare speed magnitude/direction in an on-device log for at least 40 releases.
5. Fix invalid-pose, source-change, low-confidence, and lost-tracking reset paths.

Exit criterion: the new estimate is non-zero for deliberate hand throws, remains bounded while the hand is held still, and does not mix left/right or hand/controller samples.

### Phase 3 — enable measured launch

1. Route the new release value into `SeedThrowSpawner` behind a serialized `useTrackedThrowEstimator` toggle.
2. Use valid measured velocity with the existing physics configuration.
3. Tune speed multiplier and maximum speed at real gate distances.
4. Keep burst count, seed scale, gate logic, objective completion, and VFX unchanged.

Exit criterion: 90% of deliberate throws from either hand visibly travel forward; the seed path corresponds to the hand's motion direction; stationary release does not create a high-speed projectile.

### Phase 4 — controlled assistance and UX

1. Enable weak/no-tracking assistance using the policy above.
2. Show a short release cue only when assistance is used (subtle launch VFX or trajectory preview); do not falsely imply that the user generated the full speed.
3. Consider a brief palm-direction preview while seeds are loaded, replacing the current generic-anchor arrow.
4. Tune gate tolerance and launch speeds with the rehab team, not solely on developer throws.

Exit criterion: every accepted release produces a readable toss, while telemetry clearly separates measured from assisted attempts.

### Phase 5 — performance and regression hardening

1. Pool seed projectiles if profiling shows allocation/instantiate spikes from ten seeds per burst.
2. Disable development release logs in production.
3. Verify the estimator has no scene-wide searches, per-frame allocations, or extra colliders.
4. Test the Garden with `TriggerDebugSystem` disabled; it is unrelated to the throw estimator but remains a major CPU cost.
5. Run a 10-minute soak test with repeated throws and confirm spawned seeds clean up, no duplicate subscriptions remain, and no velocity grows across releases.

## Test matrix

| Test | Expected result |
| --- | --- |
| Right bare hand, normal flick + pinch-open | Measured palm source; forward burst; no assist. |
| Left bare hand, normal flick + pinch-open | Same behaviour and no right-hand bias. |
| Gentle intentional toss | Visible weak-release assist; telemetry marks it assisted. |
| Hold still then open pinch | Small controlled toss only; no accidental high speed. |
| Hand briefly loses tracking | Buffer resets; no stale velocity launches after reacquisition. |
| Controller throw | Controller pose path works independently of hand tracking. |
| Switch controllers to hands mid-task | Source changes cleanly; no mixed samples. |
| Twist wrist while throwing | Spin may change, but forward travel remains stable. |
| Gate hit and miss | Existing gate sequence and task tracker retain their current behaviour. |
| 100 consecutive releases | No unbounded allocations, leaked seeds, duplicate events, or console spam. |

## Non-goals

- Do not change the TSTag trigger-scope work as part of this task.
- Do not add physics capsules to every hand bone solely for throwing.
- Do not replace the seed burst with ten hand-grabbable physical seed objects.
- Do not use a permanently high fallback speed as the normal launch path.
- Do not count assisted launch speed as patient movement in reports.

## Acceptance criteria

The implementation is ready for a headset playtest when all of the following are true:

1. Bare-hand throws produce a visibly forward seed burst in at least 90% of deliberate releases under normal room lighting.
2. The reported hand-pose source is valid and high-confidence for successful measured throws.
3. No valid release falls back to the current 0.35 m/s near-drop.
4. Left, right, controller, hand-tracking, tracking-loss, and task-reset paths complete without exceptions.
5. Gate/scoring behaviour is unchanged except that valid throws now reach it.
6. Development logs are transition/release-only and are disabled for production builds.
7. Profiling shows no meaningful per-frame CPU, GC, or collider increase while holding seeds.

## Reference material

- Meta's [Throwing Objects documentation](https://developers.meta.com/horizon/documentation/unity/unity-isdk-throwing-objects/) describes profile-based velocity, spin, gravity, damping, and speed limits.
- Meta's [throwing sample](https://developers.meta.com/vr/documentation/unity/unity-sample-isdk-throwing/) demonstrates `Grabbable` and `VelocityThrow` for hand and controller throws.
- Meta's [hand tracking setup](https://developers.meta.com/vr/documentation/unity/unity-handtracking-hands-setup/) explains pose hierarchy and recommends configuring hand tracking for on-device use.
- Meta's [hand-tracking limitations](https://developers.meta.com/vr/documentation/unity/unity-handtracking-troubleshooting-limitations/) documents confidence loss and noise, which the estimator must handle deliberately.
