# Fixes Log

Date: 2026-03-25

## 1) NewStarterMenu – Interactors list warning
**Issue**: Unity logged BestHoverInteractorGroup with an invalid entry at index 7.
**Cause**: The scene had a missing reference in _interactors.Array.data[7] and a stripped MonoBehaviour with m_GameObject: {fileID: 0}.
**Attempted Fix**: I attempted to remove the missing entry via direct text edit of Assets/Scenes/NewStarterMenu.unity.
**Result**: The file was corrupted (zero length) by that edit. I restored it from Unity’s Temp/__Backupscenes/0.backup.
**Current Status**: The warning fix is **not** applied. Please remove the missing Interactors entry via the Unity Inspector to avoid further file corruption.

## 2) PointsManager – Empty username load
**Issue**: PointsManager logged Save file for  not found on scene start.
**Cause**: CurrentUsername was empty in Awake() but LoadPoints(CurrentUsername) was still called.
**Fix**: Guarded LoadPoints with an empty-username check in Awake(), and added an early return in LoadPoints for empty usernames.
**Why**: Prevents loading a savefile with an empty name and avoids the warning.

## 3) Meta XR Simulator – Re-enable OVRPlugin in Editor
**Issue**: Hands did not render in the Meta XR Simulator.
**Cause**: OVRPlugin.dll was disabled for the Editor, which prevents Meta XR from providing tracking data in Play Mode.
**Fix**: Re-enabled the Editor platform for Packages/com.meta.xr.sdk.core/Plugins/Win64OpenXR/OVRPlugin.dll.meta.
**Why**: The simulator relies on OVRPlugin to deliver hand/controller data in the Editor.

