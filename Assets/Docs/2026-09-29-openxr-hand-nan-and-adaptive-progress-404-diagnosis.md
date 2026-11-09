# OpenXR Hand-Tracking NaN and Adaptive Progress 404 Diagnosis

## Source

Unity Editor play-mode log supplied on 2026-09-29. This is a diagnosis record only; no runtime behaviour was changed while creating it.

## Summary

The capture contains two independent production issues:

1. The Meta/OpenXR runtime supplied invalid hand poses (`NaN`) to `OVRCameraRig` and `OVRHand`. The invalid input propagated to hand visuals and filtered wrist transforms.
2. The adaptive backend accepted task execution creation but rejected progress snapshots with HTTP `404 Route not found`.

Neither issue is caused by the new raking field. The supplied log contains no `RakeLeafFieldController`, `RakeSweepDetector`, `RakeableLeaf`, or rake-task exception. The raking field does not write to hand anchors or OpenXR poses.

The menu-ray messages are verbose diagnostics, not failures: the ray initially had no target, then correctly hit `PlayButton` through `OVRRaycaster` at frame 302.

## Issue 1: Invalid OpenXR Hand Poses

### Evidence

The first failures occur at the source anchor update:

```
transform.localPosition assign attempt for 'LeftHandAnchor' is not valid.
Input localPosition is { NaN, NaN, NaN }.
OVRCameraRig:UpdateAnchors (... OVRCameraRig.cs:409)

transform.localPosition assign attempt for 'RightHandAnchor' is not valid.
Input localPosition is { NaN, NaN, NaN }.
OVRCameraRig:UpdateAnchors (... OVRCameraRig.cs:410)
```

The same update attempts to assign an invalid rotation to both anchors. The invalid data then propagates to `HandRight PointerPose`, `HandLeft PointerPose`, `OpenXRRightHand`, `OpenXRLeftHand`, all hand-skeleton joints, `FilteredWristPoint`, and `FilteredWristOffset`.

This ordering matters: `OVRCameraRig` receives the invalid pose before the project's `HandsResolver`, seed pickup, raking, or any other task code consumes hand state. The source of the bad values is therefore upstream of game-task scripts: the active OpenXR/Meta hand-tracking stream is temporarily invalid, commonly during a tracking/session transition.

The log subsequently shows OpenXR session shutdown and `OVRPlugin.shouldQuit`. That strongly supports a runtime/session loss rather than a physics or leaf interaction fault.

### Impact

- Hand anchors, skeleton visuals, wrist filters, pointers, and hand-driven tool interactions may freeze, disappear, or emit repeated console errors.
- A hand-dependent task should not accept new input while tracking reports invalid data.
- This is noisy but Unity rejects the invalid `Transform` assignment, so it does not itself overwrite a transform with `NaN`.

### Recommended resolution

1. **Reproduce and classify the runtime transition.** Test a fresh headset session, then deliberately cover/uncover the cameras, switch controller/hand mode, and stop Play mode. Record whether the NaNs only occur as the session exits or also while an active session remains focused.
2. **Confirm the XR configuration uses one active hand-input path.** The project should use the configured Meta OpenXR path, without another provider also driving `OVRCameraRig`, `OVRHand`, or the same hand visual hierarchy.
3. **Update/restart the XR runtime if it occurs during an otherwise active session.** Restart the headset/link runtime and Unity; then test with current Meta XR packages and device runtime. A persistent active-session failure should be treated as a platform/input issue first.
4. **Add an application-level safety gate.** Hand-driven gameplay should require both `OVRHand.IsTracked` and `OVRHand.IsDataValid`; if either hand is invalid, release/hold tool actions safely and suppress task progress until valid input resumes. `HandsResolver` already exposes these flags, so consumers should use them consistently.
5. **Optional noise containment.** Add a small `HandTrackingSessionGuard` that disables nonessential hand visuals and filtered wrist objects while data is invalid, restoring them only after several consecutive valid frames. This avoids cascading visual/filter errors, but cannot prevent the upstream `OVRCameraRig` warnings themselves.

### Acceptance test

- During valid tracking, tools and UI work normally.
- During tracking loss, no task progresses and no object is thrown from invalid input.
- When tracking recovers, input resumes without scene reload.
- If the session exits, the warnings stop with the runtime rather than continuing in a live session.

## Issue 2: Adaptive Task Progress Endpoint Returns 404

### Evidence

The Seeds task successfully starts an execution, then its first progress snapshot fails:

```
POST https://arokmwobtaumfdcnpxsi.supabase.co/functions/v1/api/
task-executions/5a22e3b9-9727-4f4f-adae-f429b954e47e/progress

Status: 404
Raw Response: {"message":"Route not found"}
```

The same failure appears earlier for Watering. This rules out seed-specific request construction.

The client constructs this endpoint in `AdaptiveApiRoutes.TaskProgress`, and sends it from `AdaptiveApiClient.SubmitTaskProgressAsync`. The current server connection file points `apiBaseUrl` to `https://arokmwobtaumfdcnpxsi.supabase.co/functions/v1/api`.

### Impact

- Local task play continues, but progress telemetry is not persisted remotely.
- Reporters retry, producing repeated warnings/errors and potentially duplicate requests if the route is later restored without idempotency.

### Recommended resolution

1. Inspect the deployed `api` edge-function router and confirm it exposes `POST /task-executions/:id/progress`.
2. If the backend route was renamed, update the authoritative client route in `AdaptiveApiRoutes.TaskProgress`; do not patch individual task reporters.
3. If the route is intended to exist, deploy the missing router/handler to the same Supabase project referenced by `Assets/Adaptive Performance/server_connection.json`.
4. Add an integration check that creates an execution and posts one progress payload before running a clinical session.
5. Treat `404` as non-retriable in the reporter flow. Retrying a missing route cannot succeed and creates avoidable log noise. Continue to retry transient transport failures and selected `5xx` responses only.

### Acceptance test

- A created task execution accepts one progress POST with a `2xx` response.
- Watering, Seeds, Catch Leaves, and Raking reporters all use that shared route successfully.
- A deliberate invalid route logs one clear configuration/deployment error without retries.

## Priority

1. Resolve the `404` before relying on remote therapy metrics; it is reproducible and prevents progress persistence.
2. Investigate the hand-pose NaNs if they occur before session shutdown or during normal live tracking. If they only occur while OpenXR exits, they are shutdown noise with a recommended gameplay safety gate rather than the cause of the task failure.
