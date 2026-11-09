# Quest device fixes log

Date: 8 November 2026  
Scope: Garden scene issues observed on the Meta Quest headset.

This document records the issues investigated so far, the approach taken, the root cause found, and the current resolution status.

## 1. Bucket did not return after catching leaves

### Observed issue

After the leaf-catching step, returning the bucket to its indicator did not work reliably on the Quest device.

### Investigation

The bucket return flow is handled by `TSObjectiveReturnZone`, not by `BucketLeafCatchSystem`.

The bucket itself was correctly configured:

- Root object name: `Bucket`
- Root tag: `Bucket`
- Return objective: `return_bucket`
- Rigidbody and grab components present

The return indicator had the correct accepted object tag and objective ID, but two scene references were missing or invalid:

1. `BucketReturnIndicator` was tagged `Untagged`, while its `TSParasite` required the `TSTag` scope tag.
2. `TSObjectiveReturnZone.snapAnchor` was unassigned.

Because of the scope mismatch, the trigger system could reject the return zone before processing the bucket.

### Resolution

Updated `Assets/Garden Scene/Garden.unity`:

- Changed `BucketReturnIndicator` from `Untagged` to `TSTag`.
- Assigned the existing `BucketReturnIndicator` transform as `snapAnchor`.

No objects were added, moved, or deleted.

### Status

Patched in the scene. Quest verification is still required for detection, snapping, and objective completion.

## 2. Seeds responded only to pinch

### Observed issue

Seeds could be picked up with pinch but not with a regular full-hand grab.

### Investigation

`SeedInteraction` already accepted both `Hand.Pose.Pinch` and `Hand.Pose.Grab`:

```csharp
bool holding = pose == Hand.Pose.Grab || pose == Hand.Pose.Pinch;
```

The problem was how `Hand.Pose.Grab` was produced in `HandsResolver`:

```csharp
if (grab != null && grab.IsGrabbing)
    return Hand.Pose.Grab;
```

`HandGrabInteractor.IsGrabbing` only becomes true when an Interaction SDK interactable is selected. The seed pickup system is trigger-based and the seed object does not have its own hand-grab interactable. Therefore the pinch path worked through `OVRHand.GetFingerIsPinching`, but the grab path was effectively unavailable.

### Resolution

Updated `Assets/Garden Scene/Scripts/HandsResolver/HandsResolver.cs` to:

- Resolve the Interaction SDK `HandGrabAPI` for each hand.
- Read `GetHandPalmScore(GrabbingRule.DefaultPalmRule, true)`.
- Treat a palm score above the configurable `palmGrabScoreThreshold` as `Hand.Pose.Grab`.
- Preserve pinch priority over grab.
- Fall back to the `HandGrabInteractor`-owned `HandGrabAPI` when a separate API reference is not found.

The default palm-grab threshold is `0.55`.

### Status

Patched in code. Quest verification is required to tune the threshold and confirm open hand, grab, pinch, and release transitions.

## 3. Objective highlighting was not visible on Quest

### Observed issue

The objective highlighting effect appeared to be missing in the Quest headset even though objectives had highlight targets configured in the Garden scene.

### Investigation

`SimTaskObjective` correctly activates `ObjectiveHighlightRuntime` when an objective begins. The runtime then creates an outline material by looking up the shader dynamically:

```csharp
Shader.Find("Shader Graphs/OutlineShader");
Shader.Find("OutlineShader");
```

The project contained `Assets/Shaders/OutlineShader.shadergraph` and `Assets/Shaders/OutlineMat.mat`, but the shader was not referenced by the Garden scene and was not listed in the project’s Always Included Shaders. Unity could therefore strip it from the Android player build while leaving it available in the Editor.

The runtime also failed silently when the shader lookup returned `null`.

### Resolution

Updated `ProjectSettings/GraphicsSettings.asset`:

- Added the outline shader to `m_AlwaysIncludedShaders` using the shader asset GUID.

Updated `Assets/SimManager/TaskSystem/ObjectiveHighlightRuntime.cs`:

- Added a warning when the outline shader cannot be found at runtime.

The existing objective activation and outline behavior were preserved.

### Status

Patched for Android/Quest shader inclusion. A fresh Quest build is required to confirm the outline is visible at runtime.

## 4. Falling leaves spawn too far from the gate

### Current finding

The leaf system resets each `LeafsFallingEffect` to the position captured during initialization. The current code does not expose an explicit gate-facing spawn reference or offset.

### Status

Not patched yet. The recommended approach is to add a serialized gate-facing spawn transform or local offset to `LeafsFallingEffect`, then tune the offset in-headset without changing the bucket catch volume at the same time.

## 5. Headset UI lacks clarity

### Current finding

The UI was reported as difficult to read and understand in the headset. This requires an in-headset layout/readability pass covering world-space size, text scale, contrast, spacing, and the number of simultaneous instructions.

### Status

Not patched yet. The recommended approach is to reduce each task to one primary instruction and one progress/status line, then verify readability at normal play distance on Quest.

## Validation checklist

Run a fresh Quest build and verify:

- Bucket return zone detects the `Bucket` tag.
- Bucket snaps to the return indicator and completes `return_bucket`.
- Seeds respond to pinch and full-hand grab.
- Seed pickup does not trigger from an open hand.
- Highlight shader is found in the Android player.
- Bucket, return-indicator, seed, and watering-can highlights are visible.
- Falling leaves remain reachable after any future spawn adjustment.
- Active task instructions are readable without using the desktop Game view.

