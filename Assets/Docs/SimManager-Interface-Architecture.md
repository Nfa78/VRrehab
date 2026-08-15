# SimManager Interface-Based Architecture (Proposed)

## Goal
Create a clean, flexible simulation architecture that supports multiple task types (including custom task flows) without building a large monolithic manager script.

## Design Principles
- Single responsibility: each layer has one clear job.
- Event-driven: gameplay scripts publish events; session/task systems evaluate progress.
- Progressive migration: existing `SimObjectiveInteraction` and `SimObjectInteraction` can continue through adapters.

## Layered Architecture

### 1) Core Task Layer
No Unity scene dependencies.

Key types:
- `SimManager` (or a future session controller) - start/stop task lifecycle.
- `targetCycles` - simple loop/repetition policy.
- `SimTask` - task state, timing, and objective progression.
- `SimTaskObjective` - objective state and progress rules.

Role:
- Defines what the system means, not how Unity implements it.

### 2) Runtime Helpers
Coordinates runtime behavior around the core task types.

Primary services:
- `SimManagerObjectiveCommandService`
- `SimManagerDebugView`
- Difficulty/adaptive hooks if needed

Role:
- Keeps objective updates and debug UI out of `SimManager`.

### 3) Infrastructure Layer (Unity Adapters)
Concrete adapters bridging existing scene systems.

Examples:
- `SimObjectiveInteraction`
- Task/config repositories via ScriptableObjects if needed

Role:
- Connects existing Unity objects and APIs to domain/application contracts.

### 4) Gameplay/Presentation Layer
Current gameplay scripts remain focused on interaction mechanics.

Examples:
- `SeedPickupThrowSystem`
- Gate/ring scripts

Role:
- Detect hand actions, trigger events, spawn visuals.
- No direct ownership of global task looping/session flow.

## Loop / Repeat Model
Task repetition is managed by orchestration, not by gameplay scripts.

Concepts:
- `targetCycles` (configured repeat count)
- `currentCycle` (runtime index)
- cycle-level reset of objective states

Base flow:
1. Start task cycle.
2. Gameplay emits events.
3. Objective tracker updates progress.
4. On cycle completion, apply repeat strategy.
5. Repeat next cycle or advance session.

## Integration with Existing Sim Systems
Use both systems with clear intent:
- `SimObjectInteraction`: interaction/telemetry or object-level signals.
- `SimObjectiveInteraction`: objective progress/completion.

Recommended integration points:
- `SeedPickupThrowSystem`: pickup/throw interaction reporting.
- task-level sequence coordinator (for ring flow): sequence success/failure progress reporting.

## Naming Conventions (Agreed Direction)
Prefer names by role:
- `...Controller`: lifecycle control
- `...Tracker`: progress/state tracking
- `...Emitter`: event publishing
- `...Strategy`: decision policy
- `...Adapter`: bridge to existing systems

Suggested examples:
- `SimManager`
- `SimTask`
- `SimObjectiveInteraction`
- `targetCycles`

## Minimal Adoption Plan
1. Keep one concrete session controller first.
2. Keep simple repetition as a built-in `targetCycles` setting.
3. Route gameplay progress through `SimObjectiveInteraction`.
4. Add adaptive/debug helpers only where they pay for themselves.

## Benefits
- Supports custom task flows without core-manager bloat.
- Keeps gameplay scripts small and focused.
- Makes testing easier at domain/application level.
- Allows gradual migration from current code.
