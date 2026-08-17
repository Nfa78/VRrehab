# Reusable Event-Driven Sound System Plan

Date: 2026-08-18

## Goal

Add a reusable sound system where gameplay, UI, task, and objective events can trigger sounds with minimal script coupling.

The system should not be tied to the Garden Scene. Garden should be the first implementation target, but the same system should work later for other scenes.

The target workflow should be:

- Task/system events trigger sounds automatically through `SimManager`.
- Buttons and scene objects can trigger sounds from the Inspector.
- Task drivers can trigger sounds with one simple event id when needed.
- Sounds can be replaced or tuned without editing task logic.
- Shared sounds can be reused across scenes.
- Scene-specific sounds can live beside each scene without changing the core system.

## Current project context

Useful existing pieces:

- `SimManager` exposes task/objective lifecycle events:
  - task completed;
  - objective progress changed;
  - objective completed.
- Garden tasks already have clear task/objective ids and can be the first scene integration.
- The project already contains general SFX assets under `Assets/_Course Library/Audio/FX`.
- The project already has simple audio helper scripts, but the reusable system should centralize event-based playback instead of scattering `AudioSource.Play()` calls across task drivers.

## Proposed architecture

### 1. `EventSoundLibrary`

Create a reusable `ScriptableObject` asset that stores sound definitions by event id.

Each sound definition should contain:

- `eventId`
- one or more `AudioClip`s
- volume
- pitch range
- spatial blend
- minimum cooldown
- mixer/category
- playback mode:
  - one-shot;
  - loop;
  - interrupt previous;
  - ignore if already playing.

Example shared ids:

- `ui.button.click`
- `sim.paused`
- `task.started`
- `objective.completed`

Example scene-specific ids:

- `garden.seed.grabbed`
- `garden.leaf.caught`
- `garden.rake.scrape`
- `kitchen.knife.cut`
- `apartment.paper.thrown`

Reasoning:

This keeps sound choices data-driven. The event id remains stable while clips, volume, and tuning can change in the Inspector.

### 2. `EventSoundManager`

Create one scene-level manager responsible for playback.

Responsibilities:

- expose simple trigger methods:
  - `Play(string eventId)`
  - `PlayAt(string eventId, Vector3 worldPosition)`
  - `StartLoop(string eventId)`
  - `StopLoop(string eventId)`
- look up clips in one or more assigned `EventSoundLibrary` assets;
- randomly choose from clip variants;
- apply cooldowns to prevent spam;
- use pooled `AudioSource`s for one-shots;
- manage global categories:
  - UI;
  - task feedback;
  - object interaction;
  - ambience;
  - voice/instruction.

Recommended scene object:

- `EventSoundManager`
  - `EventSoundManager` component
  - `AudioSource` pool children, or runtime-created pooled sources
  - assigned shared library
  - optional assigned scene-specific library

Reasoning:

One manager gives one stable place to debug missing sounds, volume balance, cooldowns, and pause behavior. Supporting multiple libraries lets us keep shared sounds global and scene sounds local.

### 3. `SoundEventTrigger`

Create a small Inspector-friendly component for manual/event wiring.

Responsibilities:

- expose a serialized `eventId`;
- expose public methods:
  - `Play()`
  - `PlayAtSelf()`
  - `StartLoop()`
  - `StopLoop()`

Usage examples:

- Button `OnClick` calls `SoundEventTrigger.Play`.
- Hoe collision event calls `PlayAtSelf`.
- Bucket return-zone event calls `PlayAtSelf`.
- Future scene objects can use the same component without custom audio code.

Reasoning:

This makes sounds easy to hook from UnityEvents without writing a new script for every button or object.

### 4. `SimManagerSoundBridge`

Create a bridge component that subscribes to `SimManager` events and converts them into sound event ids.

Events to listen to:

- simulation started;
- simulation paused;
- simulation resumed;
- simulation completed;
- simulation failed;
- simulation stopped;
- current task changed;
- task completed;
- objective progress changed;
- objective completed.

Suggested shared mappings:

| Sim event | Sound event |
| --- | --- |
| Simulation started | `sim.started` |
| Simulation paused | `sim.paused` |
| Simulation resumed | `sim.resumed` |
| Simulation completed | `sim.completed` |
| Simulation failed | `sim.failed` |
| Task changed | `task.started` or task-specific id |
| Task completed | `task.completed` |
| Objective completed | `objective.completed` |
| Objective progress changed | `objective.progress_tick` only when gated |

Progress-gating rule:

- Do not play a sound on every progress event.
- Play only when progress crosses an integer count or configured percentage threshold.
- Example: if objective progress changes from `3/10` to `4/10`, play feedback once.
- Example: if progress is continuous from `0.42` to `0.43`, do not spam audio every frame.

Reasoning:

`SimManager` is the correct central source for task state. The bridge prevents individual task drivers from duplicating task/objective sound logic.

## Event id naming convention

Use lowercase dot-separated ids.

Shared/global ids should not include a scene prefix:

`<system>.<event>`

Examples:

- `ui.button.click`
- `ui.button.error`
- `sim.started`
- `sim.paused`
- `sim.resumed`
- `sim.completed`
- `task.started`
- `task.completed`
- `objective.completed`
- `objective.progress_tick`

Scene-specific ids should include a scene/domain prefix:

`<scene_or_domain>.<area>.<event>`

Examples:

- `garden.water.can_grabbed`
- `garden.water.pouring_loop`
- `garden.seed.grabbed`
- `garden.seed.thrown`
- `garden.seed.planted`
- `garden.bucket.grabbed`
- `garden.bucket.returned`
- `garden.leaf.caught`
- `garden.hoe.grabbed`
- `garden.rake.scrape`
- `garden.leaf.raked`

Task-specific ids can be added when the generic sound is not enough:

- `garden.task.water_plants.started`
- `garden.task.throw_seeds.completed`
- `garden.task.catch_leafs.completed`
- `garden.task.rake_leaves.completed`

Keep existing internal task/objective ids unchanged if they are already used by the task system.

## Initial shared sound event map

| Trigger | Event id | Playback |
| --- | --- | --- |
| Button clicked | `ui.button.click` | 2D one-shot |
| Invalid action / blocked action | `ui.button.error` | 2D one-shot |
| Simulation started | `sim.started` | 2D one-shot |
| Simulation paused | `sim.paused` | 2D one-shot |
| Simulation resumed | `sim.resumed` | 2D one-shot |
| Simulation completed | `sim.completed` | 2D one-shot |
| Simulation failed | `sim.failed` | 2D one-shot |
| Task started | `task.started` | 2D one-shot |
| Task completed | `task.completed` | 2D one-shot |
| Objective completed | `objective.completed` | 2D one-shot |
| Objective progress tick | `objective.progress_tick` | 2D one-shot with cooldown |

## Garden Scene initial integration map

Garden should use the reusable system as the first scene-specific implementation.

### Task 1: Water plants

| Trigger | Event id | Playback |
| --- | --- | --- |
| Water can grabbed | `garden.water.can_grabbed` | 3D one-shot |
| Water can returned | `garden.water.can_returned` | 3D one-shot |
| Pouring starts | `garden.water.pouring_loop` | 3D loop |
| Pouring stops | stop `garden.water.pouring_loop` | stop loop |
| Plant watered | `garden.water.plant_watered` | 3D or 2D one-shot |

### Task 2: Seeds

| Trigger | Event id | Playback |
| --- | --- | --- |
| Seed grabbed | `garden.seed.grabbed` | 3D one-shot |
| Seed thrown | `garden.seed.thrown` | 3D one-shot |
| Seed lands in valid zone | `garden.seed.planted` | 3D one-shot |
| Seed misses | `garden.seed.missed` | 3D or 2D one-shot |
| All required seeds planted | `garden.task.throw_seeds.completed` | 2D one-shot |

### Task 3: Catch leaves

| Trigger | Event id | Playback |
| --- | --- | --- |
| Bucket grabbed | `garden.bucket.grabbed` | 3D one-shot |
| Leaf caught | `garden.leaf.caught` | 3D one-shot with cooldown |
| Catch progress tick | `objective.progress_tick` | 2D one-shot |
| Bucket returned | `garden.bucket.returned` | 3D one-shot |
| Task completed | `garden.task.catch_leafs.completed` | 2D one-shot |

### Task 4: Rake leaves

| Trigger | Event id | Playback |
| --- | --- | --- |
| Hoe grabbed | `garden.hoe.grabbed` | 3D one-shot |
| Hoe returned | `garden.hoe.returned` | 3D one-shot |
| Hoe collides with leaf | `garden.rake.scrape` | 3D one-shot with short cooldown |
| Leaf exits target z-range | `garden.leaf.raked` | 3D or 2D one-shot with progress cooldown |
| Required leaves removed | `garden.task.rake_leaves.completed` | 2D one-shot |

## Pause behavior

When simulation pauses:

- pause or reduce ambience/task loops;
- keep UI/menu sounds available;
- do not play gameplay progress sounds while paused;
- resume ambience/task loops when simulation resumes.

Recommended rule:

- UI category ignores pause.
- Gameplay, task feedback, object interaction, and ambience categories respect pause.

## Spatial audio rules

Use 2D audio for:

- dashboard/menu UI;
- objective completion;
- task completion;
- fail/success stingers;
- instruction/voice lines.

Use 3D audio for:

- object grab/release;
- object collisions;
- physical task interactions;
- local ambience sources.

Recommended default settings:

- UI: `spatialBlend = 0`
- task feedback: `spatialBlend = 0`
- object interactions: `spatialBlend = 1`
- ambience: depends on source; birds/wind can be 2D or wide 3D.

## Cooldown and spam control

Cooldowns are required for repeated physical interactions.

Suggested defaults:

| Event type | Cooldown |
| --- | --- |
| UI click | `0.03s` |
| Objective progress tick | `0.15s` |
| Leaf caught | `0.08s` |
| Rake scrape | `0.12s` |
| Collision impact | `0.10s` |
| Error/invalid action | `0.35s` |

For physics collisions, cooldown should be per event id and optionally per object instance.

## Debugging requirements

`EventSoundManager` should have a debug toggle.

When enabled, log:

- event id requested;
- clip selected;
- playback position/mode;
- ignored sounds due to cooldown;
- missing event ids;
- missing clips;
- loop start/stop.

Missing sound ids should warn once per id to avoid log spam.

## Minimal implementation phases

### Phase 1: Core event playback

Files to add:

- `Assets/SoundSystem/Scripts/EventSoundManager.cs`
- `Assets/SoundSystem/Scripts/EventSoundLibrary.cs`
- `Assets/SoundSystem/Scripts/SoundEventTrigger.cs`
- `Assets/SoundSystem/Scripts/SimManagerSoundBridge.cs`

Assets to add:

- `Assets/SoundSystem/Libraries/SharedEventSoundLibrary.asset`
- `Assets/Garden Scene/Sound/GardenEventSoundLibrary.asset`

Scene setup:

- Add `EventSoundManager` GameObject.
- Assign the shared `EventSoundLibrary`.
- Assign the optional Garden-specific `EventSoundLibrary`.
- Add `SimManagerSoundBridge` and assign the scene `SimManager`.
- Add initial clips for UI click, objective completed, task completed, pause, resume, success, failure.

Expected result:

- Task/objective completion sounds work from `SimManager` events.
- Dashboard buttons can play click sounds through Inspector wiring.
- No task driver needs direct audio code for generic task feedback.

### Phase 2: Garden task-specific sounds

Add event triggers to Garden interaction points:

- watering can grab/return/pour;
- seed grab/throw/valid landing;
- bucket grab/return;
- leaf catch;
- hoe grab/return;
- rake scrape and leaf raked.

Expected result:

- Each Garden task has basic interaction feedback.
- Sounds are still configured from libraries, not hardcoded in drivers.

### Phase 3: Ambience and polish

Add:

- scene ambience loops;
- random one-shots, such as birds or environmental sounds;
- subtle pitch randomization for repeated sounds;
- volume balancing by category;
- pause-aware loop control.

Expected result:

- Scenes feel less silent without making task feedback harder to hear.

## Editor workflow

To add a new sound:

1. Add/import the `AudioClip`.
2. Open the relevant `EventSoundLibrary` asset.
3. Add a new sound definition or add the clip to an existing event id.
4. Tune volume, pitch range, spatial blend, and cooldown.
5. Trigger it either:
   - through `SimManagerSoundBridge`;
   - through `SoundEventTrigger`;
   - from task code with one event id call.

To hook a button:

1. Add `SoundEventTrigger` to the button object or a nearby UI object.
2. Set `eventId` to `ui.button.click`.
3. Add `SoundEventTrigger.Play` to the button `OnClick`.

To hook an object:

1. Add `SoundEventTrigger` to the object.
2. Set the relevant event id.
3. Call `PlayAtSelf` from collision/grab/return-zone events.

## Acceptance criteria

The sound system is ready when:

- generic task completion sound plays from `SimManager`;
- objective completion sound plays from `SimManager`;
- objective progress sounds are gated and do not spam every frame;
- UI buttons can trigger sounds from the Inspector;
- scene task drivers do not directly own random `AudioSource` references for generic feedback;
- missing sound ids produce useful debug warnings;
- pause/resume behavior does not leave gameplay loops stuck playing;
- Garden-specific sounds work through the same reusable system used for shared sounds.

## Recommended minimal first pass

Implement only:

- `EventSoundLibrary`;
- `EventSoundManager`;
- `SoundEventTrigger`;
- `SimManagerSoundBridge`;
- generic sim/task/objective sounds;
- UI button click sound;
- one Garden-specific object sound to verify scene-specific library support.

Then add the rest of the Garden task-specific object sounds after the core path is verified in Play Mode.
