using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace VRStrokeRehab.MenuScene
{
    /// <summary>
    /// Bridges the OpenXR controller aim pose and trigger to the menu's OVR raycaster.
    /// Meta Simulator point-and-click arrives through this XR device, not Mouse.current.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MenuOpenXrPointer : MonoBehaviour, OVRInputModule.InputSource
    {
        [SerializeField] private bool leftHand;
        [SerializeField] private OVRCameraRig cameraRig;

        private XRController device;
        private Vector3Control aimPosition;
        private QuaternionControl aimRotation;
        private ButtonControl aimTracked;
        private ButtonControl trigger;
        private Transform pointerTransform;
        private Transform controllerAnchor;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private bool usingInputSystemPose;
        private bool tracked;
        private bool held;
        private bool pressed;
        private bool released;
        private bool reportedTracking;

        private void OnEnable()
        {
            pointerTransform = new GameObject(leftHand ? "LeftMenuAim" : "RightMenuAim").transform;
            pointerTransform.SetParent(transform, false);
            OVRInputModule.TrackInputSource(this);
            Debug.Log($"[MenuUIPointer] Waiting for {(leftHand ? "left" : "right")} OpenXR controller aim and trigger.", this);
        }

        private void OnDisable()
        {
            OVRInputModule.CancelInputSource(this);
            OVRInputModule.UntrackInputSource(this);
            if (pointerTransform != null)
                Destroy(pointerTransform.gameObject);
            device = null;
            tracked = held = pressed = released = reportedTracking = false;
        }

        private void Update()
        {
            if (cameraRig == null)
                cameraRig = FindFirstObjectByType<OVRCameraRig>();

            var currentDevice = leftHand ? XRController.leftHand : XRController.rightHand;
            if (device != currentDevice)
            {
                device = currentDevice;
                aimPosition = device?.TryGetChildControl<Vector3Control>("pointer/position")
                    ?? device?.TryGetChildControl<Vector3Control>("pointerPosition");
                aimRotation = device?.TryGetChildControl<QuaternionControl>("pointer/rotation")
                    ?? device?.TryGetChildControl<QuaternionControl>("pointerRotation");
                aimTracked = device?.TryGetChildControl<ButtonControl>("pointer/isTracked")
                    ?? device?.TryGetChildControl<ButtonControl>("isTracked");
                trigger = device?.TryGetChildControl<ButtonControl>("triggerPressed")
                    ?? device?.TryGetChildControl<ButtonControl>("triggerButton");

                Debug.Log($"[MenuUIPointer] {(leftHand ? "left" : "right")} Input System device="
                    + $"{device?.displayName ?? "<none>"} aimPosition={aimPosition != null} "
                    + $"aimRotation={aimRotation != null} aimTracked={aimTracked != null} trigger={trigger != null}", this);
            }

            var wasTracked = tracked;
            controllerAnchor = cameraRig != null
                ? (leftHand ? cameraRig.leftHandAnchor : cameraRig.rightHandAnchor)
                : null;

            var inputSystemTracked = device != null && device.added && device.enabled
                && aimPosition != null && aimRotation != null && aimTracked != null
                && aimTracked.isPressed && trigger != null && cameraRig != null
                && cameraRig.trackingSpace != null;
            var ovrController = leftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            var ovrPoseValid = cameraRig != null && cameraRig.trackingSpace != null
                && controllerAnchor != null && OVRInput.GetControllerPositionValid(ovrController);

            usingInputSystemPose = inputSystemTracked;
            tracked = inputSystemTracked || ovrPoseValid;
            if (inputSystemTracked)
            {
                localPosition = aimPosition.ReadValue();
                localRotation = aimRotation.ReadValue();
            }
            else if (ovrPoseValid)
            {
                localPosition = OVRInput.GetLocalControllerPosition(ovrController);
                localRotation = OVRInput.GetLocalControllerRotation(ovrController);
            }

            var nowHeld = tracked && (trigger != null
                ? trigger.isPressed
                : OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, ovrController));
            if (wasTracked && !tracked)
                OVRInputModule.CancelInputSource(this);
            pressed = nowHeld && !held;
            released = tracked && !nowHeld && held;
            if (tracked)
            {
                if (trigger != null)
                {
                    pressed |= trigger.wasPressedThisFrame;
                    released |= trigger.wasReleasedThisFrame;
                }
                pressed |= OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ovrController);
                released |= OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, ovrController);
            }
            held = nowHeld;

            if (!tracked && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[MenuUIPointer] No {(leftHand ? "left" : "right")} aim pose: "
                    + $"device={device?.displayName ?? "<none>"} deviceAdded={device?.added ?? false} "
                    + $"deviceEnabled={device?.enabled ?? false} aimControls={aimPosition != null && aimRotation != null} "
                    + $"aimTrackedControl={aimTracked != null} anchor={controllerAnchor?.name ?? "<none>"} "
                    + $"ovrConnected={OVRInput.IsControllerConnected(ovrController)} "
                    + $"ovrPoseValid={OVRInput.GetControllerPositionValid(ovrController)}", this);
            }

            if (tracked != reportedTracking)
            {
                reportedTracking = tracked;
                Debug.Log($"[MenuUIPointer] {(leftHand ? "Left" : "Right")} OpenXR aim "
                    + (tracked ? $"tracking acquired ({device.displayName})." : "tracking lost."), this);
            }
        }

        public bool IsPressed() => pressed;
        public bool IsReleased() => released;
        public bool IsValid() => this != null && isActiveAndEnabled;
        public bool IsActive() => isActiveAndEnabled && tracked;
        public OVRPlugin.Hand GetHand() => leftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
        public void UpdatePointerRay(OVRInputRayData rayData) { } // DrawPointerForUI uses the module's hit data.

        public Transform GetPointerRayTransform()
        {
            if (tracked)
            {
                var trackingSpace = cameraRig.trackingSpace;
                if (usingInputSystemPose)
                {
                    // Aim poses are relative to tracking space, not to the scene origin or grip anchor.
                    pointerTransform.SetPositionAndRotation(trackingSpace.TransformPoint(localPosition),
                        trackingSpace.rotation * localRotation);
                }
                else if (controllerAnchor != null)
                {
                    // OVRInput updates this anchor from the simulator/controller pose.
                    pointerTransform.SetPositionAndRotation(controllerAnchor.position, controllerAnchor.rotation);
                }
            }
            return pointerTransform;
        }
    }
}
