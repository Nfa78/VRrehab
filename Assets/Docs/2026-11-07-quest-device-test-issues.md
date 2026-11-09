# Quest device test issues

Date: 7 November 2026  
Device: Meta Quest  
Context: Issues observed while running the Garden tasks on the headset.

## 1. Falling leaves spawn too far from the gate

**Issue**: Falling leaves should spawn a little closer to the direction of the gate.

**Expected change**: Move the leaf spawn position slightly toward the gate-facing direction while preserving the intended play area and catchability.

**Acceptance check**: On the Quest device, confirm that leaves visibly fall closer to the gate direction and remain reachable with the bucket.

## 2. Seeds only respond to pinch grab

**Issue**: Seeds can currently be grabbed only with a pinch gesture.

**Expected change**: Support the regular grab gesture as well as pinch, for both hands where applicable.

**Acceptance check**: Verify that a seed can be picked up using pinch and a full-hand grab, then released without changing the existing seed interaction flow.

## 3. Bucket does not return after catching leaves

**Issue**: Returning the bucket after catching leaves does not work on the Quest device.

**Expected change**: Fix the bucket return interaction/state transition so the bucket can be returned after the leaf-catching step is complete.

**Acceptance check**: Catch the required leaves, attempt the return action, and confirm that the bucket returns successfully and the task advances or completes as intended.

## 4. Headset UI is not sufficiently clear

**Issue**: The UI is difficult to read and understand in the headset; its visual quality and clarity are not sufficient for the task.

**Expected change**: Improve headset readability, including text legibility, contrast, sizing, spacing, and clarity of the active instruction/status elements.

**Acceptance check**: Review the UI in-headset at the normal play distance and confirm that the current instruction, interaction guidance, and task status can be understood without guesswork.

## 5. Highlighting effect is not visible in the headset

**Issue**: The highlighting effect does not appear when running on the Quest headset.

**Expected change**: Make the relevant interaction or task highlight render reliably in the Quest runtime, including its material, layer, visibility, and activation state.

**Acceptance check**: Trigger each highlighted interaction on the Quest device and confirm that the highlight is clearly visible from the normal player viewpoint without relying on the Unity Editor or desktop Game view.

