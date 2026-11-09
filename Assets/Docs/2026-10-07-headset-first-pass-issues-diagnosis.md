# Headset first-pass issues — project diagnosis

Date: 7 October 2026  
Scope: `MenuScene` and `Garden` as currently serialized. This is a source/scene review, not a substitute for an on-device Unity Profiler capture. Findings marked **confirmed** are directly supported by the project; the remaining items are explicitly framed as likely causes and have a concrete verification step.

## Executive summary

The five observations are connected by a few project-wide problems:

1. The menu has a controller-ray click bridge, but no hand-pinch click bridge. A bare-hand pinch is therefore not an input the active menu module can click with.
2. The Garden is substantially above a reasonable standalone-VR rendering baseline: 1,050 serialized mesh renderers, 898 colliders, no `LODGroup`s, dense grass/tree content, camera post-processing, 4x MSAA, soft shadows, and a 50 m shadow distance.
3. The headset’s visible FOV is runtime/lens-defined. It cannot be expected to match the desktop Game view. The project needs a calibrated metres-based test scene rather than screen/FOV-derived measurements.
4. Seed release is based on a short transform-history sample. If that sample is near zero (common when the tracked hand anchor is not the moving release point), the code deliberately launches at only 0.35 m/s along `hand.forward`, which is often a bad throwing direction for a hand anchor. This exactly produces a weak downward-looking drop.
5. There is a concrete serialization migration issue in the seed visual settings: the scene still contains the retired `seedCubeScale: 0.05`, while the current script uses `seedVisualLengthMeters` (default 0.012 m) and aspect `(0.65, 0.45, 1)`. The old value has no effect.

## 1. Menu hand pinches do not click in the headset

### Diagnosis — **confirmed input mismatch**

The active menu EventSystem uses `OVRInputModule` and one enabled `MenuOpenXrPointer`, configured as the **right** hand. The alternative `XRUIInputModule` exists but is disabled.

`MenuOpenXrPointer` is a controller aim/trigger adapter, not a hand-pinch adapter:

- It looks up `XRController.rightHand`/`leftHand` aim pose and `triggerPressed` / `triggerButton` controls.
- Its fallback reads `OVRInput.Button.PrimaryIndexTrigger` and the controller pose.
- It does not read `OVRHand`, `OVRSkeleton`, `HandsResolver`, an index-tip pose, or `GetFingerIsPinching`.
- `inputSystemTracked` additionally requires a trigger control to exist. A hand-tracking device can supply a pose without a controller trigger, so it will not qualify for this path.

The garden’s `HandsResolver` already knows how to obtain `OVRHand` pinch state, but the menu does not use it. The result is expected: controller-trigger pointing can work, while index-thumb pinching with bare hands produces no press/release event.

There is a second failure amplifier. While `MenuOpenXrPointer` is enabled, `MenuSceneSetupVerifier` deliberately clears `OVRInputModule.rayTransform`; if the special pointer is not tracking, there is no ordinary head/grip ray fallback.

Evidence:

- [`MenuOpenXrPointer.cs`](../MenuScene/Scripts/Runtime/UI/Common/MenuOpenXrPointer.cs) reads controller aim/trigger inputs only.
- [`MenuSceneSetupVerifier.cs`](../MenuScene/Scripts/Runtime/Validation/MenuSceneSetupVerifier.cs) sets `rayTransform = null` when that pointer owns the ray.
- [`MenuScene.unity`](../MenuScene/MenuScene.unity) enables `OVRInputModule` and the right-hand pointer, while its `XRUIInputModule` is disabled.

### Recommended fix

Choose one UI interaction stack and use it everywhere in the menu. Since the scene already uses Meta Interaction SDK, the lowest-risk option is Meta hand point/poke + `PointableCanvasModule`/the appropriate hand ray interactor, with exactly one EventSystem/input module.

If retaining the custom OVR ray path, add a separate `MenuHandPinchPointer` which:

1. obtains the correct `OVRHand` for left/right (or uses `HandsResolver`);
2. uses the index-tip / aim pose for the ray origin and direction;
3. translates `GetFingerIsPinching(Index)` to press/release transitions; and
4. rejects input until `IsTracked && IsDataValid` is true.

Do not treat a controller trigger as equivalent to a hand pinch. Keep a controller-ray source as an accessibility fallback, but test each source independently.

### Acceptance test

In a development APK, log one compact state line on transition only: hand side, tracked/data-valid, ray pose valid, pinch state, raycast target, press, release. Verify each of these paths:

- right controller point + trigger;
- right bare-hand point + pinch;
- left bare-hand point + pinch;
- tracking loss/reacquisition while the pinch is held (must not create a false click).

Also turn off the current every-frame menu ray debugger for release builds. It performs reflection and `EventSystem.RaycastAll` logging every frame and is useful for diagnosis, but it is not a production runtime feature.

## 2. Garden lags on the headset

### Diagnosis — **confirmed high rendering/physics load; exact bottleneck needs profiling**

The Garden scene contains **1,160 GameObjects, 1,050 MeshRenderers, 898 colliders, and no LODGroups**. By comparison, `MenuScene` has 110 GameObjects, five MeshRenderers, and one collider. The Garden has many independent `GrassBase`, `GrassMedium`, and tree objects; their mesh renderers use light/reflection probe settings and the scene has no serialized LOD grouping.

The active Android quality path compounds this:

- Android defaults to the `Mobile` quality tier, which uses `URP-Balanced`.
- `URP-Balanced` uses **4x MSAA**, a 1.0 render scale, soft main-light shadows, a 50 m shadow distance, and up to five additional lights per object.
- The Garden XR camera enables post-processing and temporal anti-aliasing.
- Although that URP asset has Adaptive Performance support enabled, every inspected Android adaptive scaler is disabled. The `OVRPerformanceTuner` script would request sustained-high CPU/GPU and the highest available refresh rate, but it is not referenced by either reviewed scene; it is therefore not a current protection against overload.

On a mobile headset, this mix is likely GPU-bound first (fill rate from stereo rendering, alpha-tested/transparent vegetation, 4x MSAA, post FX and shadows). The 898 colliders and unbatched object count are also credible CPU/physics contributors. A headset that misses its frame budget will reproject frames; this is perceived as lag even if the simulation itself continues.

Evidence:

- [`Garden.unity`](../Garden%20Scene/Garden.unity) contains the Garden’s 1,050 mesh renderers, vegetation, colliders and no `LODGroup` records.
- [`QualitySettings.asset`](../../ProjectSettings/QualitySettings.asset) maps Android to `Mobile`.
- [`URP-Balanced.asset`](../Settings/URP-Balanced.asset) sets 4x MSAA, soft shadows, 50 m shadow distance and render scale 1.
- [`OVRPerformanceTuner.cs`](../Scripts/OVRPerformanceTuner.cs) exists but is not attached in `Garden.unity` or `MenuScene.unity`.

### Recommended fix order

1. **Profile a development APK on the headset first.** Capture CPU Usage, GPU Usage, Rendering and Memory for 20–30 seconds while looking at the dense garden, then while looking down at simple ground. Record refresh rate, App CPU/GPU levels, dropped/app-spacewarp frames, batches, triangles and overdraw.
2. **Make a Quest-specific quality tier**, rather than using the generic Mobile tier. Start with 2x MSAA, hard shadows or lower-quality soft shadows, 20–30 m shadow distance, one cascade, no post-processing, and no TAA. Restore items one at a time only when the target headset holds frame time.
3. **Cull/LOD vegetation.** Convert repeated static grass/tree meshes to GPU-instanced or batched content, give trees/rocks LODs, use billboard/impostor distance, and remove individual grass objects outside the play area. Ensure vegetation does not cast/receive costly shadows unless it matters for the task.
4. **Reduce collider work.** Remove colliders from visual-only grass, replace repeated mesh colliders with simple shared primitives where interaction is not required, and use a collision-layer matrix so seeds/hands do not test against irrelevant scenery.
5. **Set the target deliberately.** Do not select the headset’s highest refresh rate by default. For a rehabilitation task, a stable 72 or 80 Hz is preferable to unstable 90/120 Hz. Enable dynamic resolution only after verifying the runtime implementation and readability at its minimum scale.

### Acceptance test

Define a per-device budget before visual tuning: at 72 Hz, stay below 13.89 ms/frame; at 80 Hz, below 12.5 ms/frame. Test both the heaviest garden view and a seed throw. A visual change is accepted only if it removes dropped frames without making hand targets, seed trajectories, or clinical-scale cues unreadable.

## 3. Headset FOV differs and measurements look wrong

### Diagnosis — **expected FOV difference; measurement path needs calibration**

The desktop/editor Game view is a single perspective camera. In VR, the OpenXR/Meta runtime supplies the per-eye projection matrices needed for the headset lenses. The visible FOV is therefore hardware/runtime dependent; changing or comparing a normal Unity camera FOV is not a valid way to make the headset view match the editor.

The reviewed rig uses `OVRCameraRig`, floor-level tracking origin (`_trackingOriginType: 1`), and unit scale. It has scene-specific placement only: the menu rig is positioned at `(0, 3.15, -4.23)` and the Garden rig at approximately `(-11.73, 1.26, 7.24)`. Those positions place the participant in each scene; they do not change metre-to-Unity-unit scale. No global rig scale override was found in the serialized scene review.

The apparent measurement problem is consequently more likely to be one of:

- measurements inferred from screen pixels/FOV instead of world-space metres;
- task objects authored/imported at inconsistent scale;
- a floor-vs-eye-level origin assumption; or
- poor depth judgement caused by the large garden, wide hardware FOV, or inconsistent reference objects.

Evidence:

- [`Garden.unity`](../Garden%20Scene/Garden.unity) configures floor-level tracking and the XR camera with XR rendering enabled.
- [`MenuScene.unity`](../MenuScene/MenuScene.unity) and [`Garden.unity`](../Garden%20Scene/Garden.unity) place the same OVR rig prefab differently but do not apply a global scale override.

### Recommended fix

Adopt and enforce **1 Unity unit = 1 metre**. Create a small in-headset calibration scene/panel containing a 1 m floor ruler, 10 cm cube, 50 cm reach marker, and a seated/standing eye-height marker. Measure using `Transform.position`/collider bounds in world metres, never pixels or camera FOV.

For each clinical metric, write down the reference frame explicitly: `tracking space`, `XR origin`, `hand joint`, or `world`. Convert once at the boundary and persist both the value and reference frame in telemetry. This prevents changing XR origin or scene placement from silently changing a metric.

Do not tune headset FOV to make it resemble the Game view. Tune object distance, angular size and physical scale from inside the target headset.

### Acceptance test

With the headset on, place the physical/virtual 1 m ruler against a real 1 m reference, then verify that moving an object exactly 0.10 units changes the reported distance by 0.10 m. Repeat after loading both scenes and after recentering. Metrics must remain invariant.

## 4. Seeds fall down instead of feeling thrown

### Diagnosis — **confirmed weak fallback; likely unreliable hand-velocity source**

The throw system samples `ActiveHand.position` in `Update` over at most six samples / 0.12 seconds. On release it averages that history and passes it to the spawner. If the measured speed is less than 0.15 m/s, `SeedThrowSpawner` deliberately substitutes a **0.35 m/s** launch in `hand.forward`.

This is a plausible exact match for the report:

- At 0.35 m/s, gravity makes a seed drop roughly 40 cm in about 0.29 s, during which it travels only about 10 cm forward. It reads as a drop, not a throw.
- `hand.forward` on an OVR/hand-anchor transform is not a robust throw direction. It can point down, toward the palm, or be offset from the release direction depending on the hand representation.
- In the headset, tracking may update a visual/interaction anchor differently from the transform being sampled. That leaves the release history near zero even while the person moves their actual hand.
- The scene has no seed prefab assigned, so every seed is built through `GameObject.CreatePrimitive(PrimitiveType.Sphere)` at release. This is not the primary trajectory bug, but it creates ten allocations, colliders and rigidbodies per throw and can create a small hitch.

The current physics makes a weak launch worse: seeds use gravity, 3 g mass, rigidbody damping and optional aerodynamic drag. Those are reasonable only after a credible release velocity exists.

Evidence:

- [`SeedPickupThrowSystem.cs`](../Garden%20Scene/task_drivers/Seeds/TaskLogic/SeedPickupThrowSystem.cs) samples an anchor transform and estimates release velocity from a 0.12 s buffer.
- [`SeedThrowSpawner.cs`](../Garden%20Scene/task_drivers/Seeds/PhysicsHelpers/SeedThrowSpawner.cs) uses 0.15 m/s as the threshold and 0.35 m/s along `hand.forward` as fallback.
- [`Garden.unity`](../Garden%20Scene/Garden.unity) has `seedCubePrefab: {fileID: 0}`, `minimumFallbackLaunchSpeed: 0.35`, and ten spawned seeds.

### Recommended fix

1. **Instrument before tuning.** For every release, log/display: active source, hand-anchor name, release-point name, sample count/window, raw velocity vector, fallback-used flag, base direction, launch speed, and the first seed’s velocity. This will distinguish “gesture not detected” from “wrong transform” from “weak fallback.”
2. **Sample a real release point.** Prefer a tracked palm/index-tip pose or a child transform explicitly aligned to the grasped seed. Do not sample the generic rig hand anchor. Sample in `Update`/`BeforeRender` using unscaled timestamps and apply the result in the next `FixedUpdate`.
3. **Use a weighted/regression velocity estimate and a minimum useful assisted throw.** A rehabilitation-friendly initial range is typically a configurable 1.0–2.0 m/s assisted launch (then validate against clinical goals), not 0.35 m/s. Keep the measured horizontal direction when available; if it is absent, use a calibrated palm/aim direction with a small upward component, not raw `hand.forward`.
4. **Make direction and velocity independent.** Use tracked velocity for speed, but blend its direction with the hand/palm aim only when the velocity direction is noisy. Clamp vertical launch angles so a valid gesture cannot turn into an immediate ground impact.
5. **Pool a seed prefab** with its renderer, collider and rigidbody preconfigured. This removes launch-time construction noise and makes seed shape/scale explicit.

### Acceptance test

Run ten slow, ten normal and ten fast releases with each hand. For each group, record launch speed, horizontal range and landing area. The normal release should have a clearly visible forward arc; slow therapeutic motions should receive an intentional, documented assist rather than silently dropping. Check the same data in editor simulation and on the headset; their median launch speeds should be comparable.

## 5. Object sizes may be off

### Diagnosis — **confirmed seed-scale migration issue; broader authoring audit required**

The current seed script replaced `seedCubeScale` with physical dimensions:

```text
seedVisualLengthMeters = 0.012
seedVisualAspect       = (0.65, 0.45, 1.0)
```

But the Garden scene still serializes `seedCubeScale: 0.05`. That field no longer exists in `SeedThrowSpawner`, so Unity does not apply it. With no prefab assigned, the fallback sphere is now scaled to approximately **7.8 × 5.4 × 12 mm**, not the legacy 5 cm value. This is likely intentional for a seed, but it is a concrete example of why editor expectations may not match the running scene.

More generally, the Garden combines third-party environment assets, hand/interaction prefabs and newly authored task props. There is no documented scene-wide physical-scale validation or LOD/size policy. The headset is much less forgiving of inconsistent scale than the desktop view because reach, body calibration and stereo depth expose it immediately.

Evidence:

- [`SeedThrowSpawner.cs`](../Garden%20Scene/task_drivers/Seeds/PhysicsHelpers/SeedThrowSpawner.cs) defines the new metre-based visual dimensions.
- [`Garden.unity`](../Garden%20Scene/Garden.unity) retains the obsolete `seedCubeScale: 0.05` key and leaves the seed prefab null.

### Recommended fix

Create a `PhysicalScale` prefab set (1 m ruler, 10 cm cube, common hand-sized object, standard pot/bucket) and use it to review all interactive objects in headset. For every task prefab, document: intended real-world dimensions, Unity bounds in metres, interaction/reach distance, collider bounds, and visual scale owner (prefab vs runtime script).

For the seed system specifically, open and resave the component after assigning a proper seed prefab and explicit `seedVisualLengthMeters`; this removes the stale serialized key. Decide the clinically useful apparent seed size first—visual visibility may justify a larger seed than botanic realism—but keep the collider/landing target consistent with that decision.

## Suggested implementation sequence

1. Fix/menu-test hand pinch input, retaining controller fallback.
2. Add lightweight on-device telemetry for tracking, seed releases and frame timing.
3. Establish the 1 m calibration scene and validate all measurement reference frames.
4. Correct the seed release-point/velocity estimator and pool the seed prefab.
5. Capture a Garden headset profile, then reduce vegetation/overdraw/shadows/post-processing in measured order.
6. Run the full five-item headset regression on the target device(s), including a cold launch and a 15-minute thermal soak.

## 6. Garden re-audit - 9 October 2026

The Garden scene changed after the first-pass count. The first count remains valid as a serialized-component count, but it did not distinguish components already disabled in the scene from those active at runtime. The current audit is therefore the source of truth for optimization work below.

### Current scene inventory

- 1,151 scene GameObjects: 1,147 active and four inactive.
- 1,050 `MeshRenderer` components: **136 enabled** and **914 already disabled**.
- 891 enabled colliders: 184 mesh, 694 box, six sphere, and seven capsule colliders.
- **845 GameObjects have a disabled renderer but an enabled collider on the same object.** The repeated `Plane` objects alone account for 180 of those colliders.
- Zero serialized `LODGroup` components.
- 134 of the 136 visible renderers both cast and receive shadows.
- The Quest render path remains 4x MSAA, 50 m shadow distance, soft shadows, post-processing and temporal anti-aliasing.

### Highest-confidence changes

1. **Disable `TriggerDebugSystem` in the Garden scene for headset builds.** It is an active debug-only object. On startup it finds every collider and adds `TriggerDebuggerParasite` to it. With the current scene, that is roughly 891 extra behaviours. Each has an `Update`; trigger contacts can also emit `Debug.Log` output. This is the clearest immediate CPU win and should not alter game behaviour.
2. **Disable `ReportCollisions` and `Tracker` for headset builds.** `ReportCollisions` adds copies of itself to child objects and logs collision events. `Tracker` performs a frame-by-frame transform copy. Neither should ship in the player.
3. **Turn off remaining runtime debug logging.** At minimum set `logDebug` false on `SeedPickupThrowSystem`, `SeedPickupState`, `BucketLeafCatchSystem`, the seed gate/return-zone diagnostics and water diagnostics. Logging during interaction is disproportionately expensive on Android/Quest.
4. **Turn off shadows on non-task scenery.** Start with the 134 currently visible scene renderers. Preserve shadows only where they are an intentional depth or safety cue near the active task; turn off cast and receive shadows for the remainder. This is a low-risk GPU test because the geometry remains visible.
5. **Create a minimal collision set before removing colliders.** Do not blindly disable all 845 invisible-object colliders: many may be intentionally providing physical collision while their original renderers are replaced by combined visible geometry. Instead retain only floor/terrain, boundaries, task props, soil/catch/return triggers and directly reachable obstacle surfaces. Remove or layer-ignore the rest, especially small decorative and hidden mesh colliders.

### Recommended reduction sequence

1. Build and test with `TriggerDebugSystem`, `ReportCollisions` and `Tracker` disabled, and all debug logs off.
2. Test with post-processing disabled, TAA disabled, 2x MSAA, soft shadows disabled, and 20-30 m shadow distance.
3. Audit the 845 invisible collider objects by region. Disable a region's colliders, then verify walking/hand contact, seed bounce and every task target. Keep only the colliders that fail a required interaction test.
4. Add LODs or simplify the visible 136 renderers, prioritizing large garden mesh pieces and repeated scenery. Do not spend time on the 914 renderers that are already disabled.

### Guardrail

The scene's disabled renderers are not the main graphics cost now. The main immediate risks are debug behaviours/logging, the 891 active colliders, shadowed visible geometry, and the expensive Quest render configuration. Validate each reduction in a development APK on the headset; scene serialization alone cannot reveal the final GPU triangle count or overdraw of the 136 active combined meshes.
