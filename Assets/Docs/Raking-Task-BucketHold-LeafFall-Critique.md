# Raking Task Extension Critique: Post-Seed Bucket Hold + Wind-Driven Leaf Fall

## Idea Summary
After the seed-throw phase, the user holds a seed bucket in designated positions while tree leaves fall due to wind. The hold phase becomes a sustained-control objective before continuing the raking flow.

## User Experience

### Pros
- Clear narrative continuity: throw seeds -> remain active in scene -> environment reacts.
- Adds immersion through visible world response (wind + falling leaves).
- Creates a calmer second phase after a dynamic throw action.
- Can improve perceived purpose of bucket interaction (not just prop handling).

### Cons
- Risk of cognitive overload if wind, leaf motion, target-zone cues, and timing are all introduced at once.
- If hold duration is too long, users may feel "stuck" and bored.
- Visual clutter from many falling leaves can reduce objective readability.
- Users may not understand why holding a seed bucket causes leaf fall unless narrative cueing is explicit.

## Gameplay Design

### Pros
- Good pacing contrast: ballistic throw -> precision hold.
- Supports skill layering: gross motor control plus sustained positioning.
- Easy difficulty tuning via hold time, zone size, and wind intensity.
- Creates natural loop opportunities for repetitive training without feeling identical.

### Cons
- Potential phase mismatch with "raking leaves" fantasy unless connected with a clear cause (seasonal wind, tree shake trigger, etc.).
- If the hold phase has no scoring/feedback variety, replay value drops.
- Hard fail conditions (drop bucket resets all progress) can feel punitive.
- If zone placement is too strict, frustration increases quickly in VR.

## Implementation / Technical

### Pros
- Modular and low-risk if split into dedicated components:
- `BucketHoldObjectiveController`
- `BucketHoldZoneTrigger`
- `LeafFallWindController`
- Integrates with existing `SimObjectiveInteraction` flow.
- Fits internal loop model: multiple cycles inside one logical task execution.
- Leaf effects can be visual-only initially, reducing physics/performance complexity.

### Cons
- Physics/event race conditions possible (grab state, zone detection, objective timing).
- Particle-heavy leaf systems can hurt standalone VR performance if not budgeted.
- Multi-phase objective transitions increase state-management complexity.
- Requires strong telemetry to debug "why objective did/did not complete".

## Stroke Rehab Value

### Pros
- Sustained hold trains postural endurance and shoulder stability.
- Target-zone holding supports proprioception and controlled isometric effort.
- Adjustable zone size/time supports graded difficulty progression.
- Repetition under varied wind visuals can reduce monotony while preserving motor goals.

### Cons
- Risk of fatigue if hold durations are not individualized.
- Limited bilateral challenge unless off-hand or trunk stabilization is intentionally included.
- Overly strict precision targets may discourage users with severe motor impairment.
- If wind visuals imply motion demand but mechanics require static holding, therapeutic intent may feel inconsistent.

## Clinical/Design Guardrails
- Start with short holds (e.g., 3-6s) and larger zones; scale down zone or increase time gradually.
- Use soft-failure handling: partial progress retention rather than full reset.
- Add simple real-time cues: in-zone indicator, hold timer, stability feedback.
- Cap concurrent leaf count and use pooled particles for Quest performance.
- Provide therapist-configurable presets (Easy/Medium/Hard or profile-based).
- Log cycle-level metrics: hold completion rate, drop events, time-to-complete, rest breaks.

## Recommendation
Proceed, but frame it as a **controlled hold subtask** with clear narrative signaling and accessibility-first tuning.

Best first implementation:
1. Single hold zone.
2. Short hold timer objective with visible countdown.
3. Lightweight leaf-fall VFX triggered only while holding in-zone.
4. No harsh reset; allow quick re-entry to continue progress.

## Decision
The concept is strong for rehab and gameplay pacing if implemented with conservative difficulty, explicit feedback, and performance-conscious VFX.
