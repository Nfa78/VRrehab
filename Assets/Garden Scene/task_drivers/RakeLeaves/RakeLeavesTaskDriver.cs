using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class RakeLeavesTaskDriver : SimTaskDriver
    {
        [Header("Objectives")]
        [SerializeField] private string pickupHoeStepId = "pickup_hoe";
        [SerializeField] private string rakeLeavesStepId = "rake_leaves";
        [SerializeField] private string returnHoeStepId = "return_hoe";

        [Header("Hoe")]
        [SerializeField] private GameObject hoeObject;
        [SerializeField] private HandGrabInteractable hoeHandGrabInteractable;
        [SerializeField] private Rigidbody hoeRigidbody;
        [SerializeField] private bool autoFindHoeByName = true;
        [SerializeField] private string hoeObjectName = "HoePrefab";
        [SerializeField][Min(0.01f)] private float returnDistanceThreshold = 0.35f;
        [SerializeField] private bool snapHoeOnReturn = true;
        [SerializeField] private bool clearVelocityOnReturn = true;
        [SerializeField] private bool makeKinematicOnReturn = true;
        [SerializeField] private bool logDebug;

        private Pose initialHoePose;
        private bool hasInitialHoePose;
        private bool wasHoeGrabbedLastFrame;

        public override string TaskId => "rake_leaves";

        private void Awake()
        {
            ResolveHoeReferences();
            CaptureInitialHoePoseIfNeeded();
        }

        private void OnEnable()
        {
            ResolveHoeReferences();
            CaptureInitialHoePoseIfNeeded();
        }

        private void Update()
        {
            if (SimTask == null || SimManager == null || !SimManager.IsRunning)
            {
                wasHoeGrabbedLastFrame = false;
                return;
            }

            ResolveHoeReferences();
            CaptureInitialHoePoseIfNeeded();

            bool isHoeGrabbed = IsHoeGrabbed();
            if (IsPickupHoeStepActive() && isHoeGrabbed && !wasHoeGrabbedLastFrame)
            {
                bool completed = CompletePickupHoeStep();
                if (logDebug)
                {
                    Debug.Log($"[RakeLeavesTaskDriver] Hoe pickup {(completed ? "completed" : "was rejected")}.", this);
                }
            }

            if (IsReturnHoeStepActive() && !isHoeGrabbed)
            {
                TryCompleteReturnHoeStepFromPose();
            }

            wasHoeGrabbedLastFrame = isHoeGrabbed;
        }

        public bool IsPickupHoeStepActive()
        {
            return IsActiveStep(pickupHoeStepId);
        }

        public bool IsRakeLeavesStepActive()
        {
            return IsActiveStep(rakeLeavesStepId);
        }

        public bool IsReturnHoeStepActive()
        {
            return IsActiveStep(returnHoeStepId);
        }

        public bool SetHoeHeld(bool isHeld)
        {
            return !string.IsNullOrWhiteSpace(pickupHoeStepId) && TrySetStepState(pickupHoeStepId, isHeld);
        }

        public bool SetToolHeld(bool isHeld)
        {
            return SetHoeHeld(isHeld);
        }

        public bool CompletePickupHoeStep()
        {
            return !string.IsNullOrWhiteSpace(pickupHoeStepId) && TryCompleteStep(pickupHoeStepId);
        }

        public bool RakeLeaves(float delta = 1f)
        {
            return !string.IsNullOrWhiteSpace(rakeLeavesStepId) && TryAddStepProgress(rakeLeavesStepId, delta);
        }

        public bool CollectLeaf(float delta = 1f)
        {
            return RakeLeaves(delta);
        }

        public bool CompleteReturnHoeStep()
        {
            return !string.IsNullOrWhiteSpace(returnHoeStepId) && TryCompleteStep(returnHoeStepId);
        }

        private void ResolveHoeReferences()
        {
            if (hoeObject == null && autoFindHoeByName && !string.IsNullOrWhiteSpace(hoeObjectName))
            {
                hoeObject = GameObject.Find(hoeObjectName);
            }

            if (hoeObject == null)
            {
                return;
            }

            if (hoeHandGrabInteractable == null)
            {
                hoeHandGrabInteractable = hoeObject.GetComponent<HandGrabInteractable>() ??
                                          hoeObject.GetComponentInChildren<HandGrabInteractable>(true);
            }

            if (hoeRigidbody == null)
            {
                hoeRigidbody = hoeObject.GetComponent<Rigidbody>() ??
                               hoeObject.GetComponentInChildren<Rigidbody>(true);
            }
        }

        private void CaptureInitialHoePoseIfNeeded()
        {
            if (hasInitialHoePose || hoeObject == null)
            {
                return;
            }

            Transform hoeTransform = hoeObject.transform;
            initialHoePose = new Pose(hoeTransform.position, hoeTransform.rotation);
            hasInitialHoePose = true;
        }

        private bool IsHoeGrabbed()
        {
            if (hoeHandGrabInteractable == null || hoeHandGrabInteractable.Interactors == null)
            {
                return false;
            }

            foreach (object candidate in hoeHandGrabInteractable.Interactors)
            {
                HandGrabInteractor interactor = candidate as HandGrabInteractor;
                if (interactor != null && interactor.IsGrabbing)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryCompleteReturnHoeStepFromPose()
        {
            if (hoeObject == null || !hasInitialHoePose)
            {
                return;
            }

            float distance = Vector3.Distance(hoeObject.transform.position, initialHoePose.position);
            if (distance > returnDistanceThreshold)
            {
                return;
            }

            bool completed = CompleteReturnHoeStep();
            if (!completed)
            {
                return;
            }

            ApplyReturnedHoePose();
            if (logDebug)
            {
                Debug.Log($"[RakeLeavesTaskDriver] Hoe return completed. Distance={distance:F3}.", this);
            }
        }

        private void ApplyReturnedHoePose()
        {
            if (hoeObject == null)
            {
                return;
            }

            if (hoeRigidbody != null)
            {
                if (clearVelocityOnReturn)
                {
                    hoeRigidbody.linearVelocity = Vector3.zero;
                    hoeRigidbody.angularVelocity = Vector3.zero;
                }

                if (makeKinematicOnReturn)
                {
                    hoeRigidbody.isKinematic = true;
                }
            }

            if (snapHoeOnReturn)
            {
                hoeObject.transform.SetPositionAndRotation(initialHoePose.position, initialHoePose.rotation);
            }
        }
    }
}
