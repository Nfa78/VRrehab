# Seed Pickup/Throw Proposed Additions (Design Notes)

## Goal
Improve throw readability and introduce skill-based targeting while keeping scripts small and modular.

## Visual Clarity During Throw
Two options were discussed:
- Seed shader emphasis (glow/rim/emissive look)
- Per-seed trail/particle effect

Recommended order:
1. Add a lightweight trail effect first (highest readability for motion).
2. Add shader enhancement only if needed after playtesting.

Why:
- Trails communicate trajectory immediately in VR.
- Lower complexity than custom shader pipeline changes.

## Ring/Circle Target Flow
Add three ring gates in space and validate pass-through behavior.

Design intent:
- Seeds pass through a 3-circle sequence.
- Ring size controls difficulty (larger = easier, tighter = harder).
- Visual feedback on success/fail (pulse/flash shader effect).

## Modular Script Split (Avoid Dense Scripts)

### 1) `SeedPickupThrowSystem` (existing)
Responsibility:
- hand detect, pickup state, release detect, seed spawning.

Additions:
- initialize seed VFX component after spawn.
- keep objective pickup reporting behavior.

### 2) `SeedFlightVfx` (new)
Responsibility:
- seed-only visuals (trail, optional glow).
- no task logic, no scoring.

### 3) `SeedGate` (new)
Responsibility:
- trigger detection of a seed passing ring collider.
- emit `OnSeedPassed` event.

### 4) `SeedGateSequence` (new)
Responsibility:
- track sequence logic across 3 gates.
- evaluate success/failure/time windows.
- report progress/completion.

## Sim Integration for Seed Flow
We accounted for both interaction systems:
- `SimObjectInteraction`: object/interaction-level signaling.
- `SimObjectiveInteraction`: objective progress/completion.

Recommended usage:
- `SeedPickupThrowSystem`: pickup and throw interaction reporting.
- `SeedGateSequence`: first-pass, sequence success/fail, objective progress updates.

## Difficulty Tuning
Difficulty parameters can include:
- ring radius per gate
- sequence time window
- required successes per cycle
- optional ring motion/wobble at higher levels

Config approach:
- profile-style configuration (e.g., easy/medium/hard presets)
- applied by sequence coordinator at start of cycle

## Proposed Initial Implementation Scope
1. Add per-seed trail VFX hookup in spawn flow.
2. Add 3 ring gates with pass-through events.
3. Add sequence coordinator with adjustable ring size and success reporting.
4. Keep all components decoupled and event-driven.

## Why This Structure
- No single giant script.
- Each component has one reason to change.
- Ring system can be enabled/disabled without altering core throw mechanics.
- Reusable pattern for other tasks.
