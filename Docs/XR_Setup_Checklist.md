# XR Setup Checklist (Meta Quest 3)

**Goal**: A minimal, reliable Quest 3 setup using OpenXR + Meta XR + Meta Interaction SDK.

**Project Settings**
1. **Build target**: Android
2. **XR Plug-in Management**
   - Enable `OpenXR` for Android
   - Enable `OpenXR` for Standalone only if you want to run in Editor
3. **OpenXR Features**
   - Enable Meta Quest Support (Meta XR feature set)
   - Enable Hand Tracking
4. **Player Settings**
   - Color Space: Linear
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64

**Packages**
1. `com.meta.xr.sdk.all` (Meta XR SDK)
2. `com.unity.xr.openxr`
3. `com.unity.xr.management`
4. Optional: `com.unity.xr.hands` (Unity XR Hands)

**Scene Requirements (Meta Interaction SDK)**
1. **XR Origin / Rig**
   - A single rig for head and hands
2. **Interaction Manager**
   - Only one active interaction manager in the scene
3. **Hand Interactors**
   - Meta hand prefabs for near + far interactions
4. **Meta Interaction SDK runtime objects**
   - Any required services / registries from the Meta prefabs

**Interactable Objects**
1. Add `HandGrabInteractable` (Meta) to grabbable objects
2. Ensure `Rigidbody` and correct `Collider` setup
3. Assign required references on your custom scripts

**Common Failure Checks**
1. **No interaction?** Confirm the scene has an Interaction Manager and hand interactors.
2. **No grabs?** Confirm `HandGrabInteractable` is on the target and colliders are not trigger‑only.
3. **Wrong hand scoring?** Confirm `PointsManager.Instance.Arm` is set correctly.
4. **Editor crashes?** Ensure Meta XR native plugin is not loading in Editor if you see `OVRPlugin` crashes.

**Runtime/Device Checks**
1. Verify Quest 3 is in Developer Mode
2. Confirm Android SDK/NDK/JDK paths if you build from Unity
3. On-device, ensure OpenXR runtime initializes (check logs)

**Do Not Mix**
1. Avoid mixing Meta Interaction SDK and Unity XRI on the same objects
2. Pick one interaction system per object to avoid double interactions

