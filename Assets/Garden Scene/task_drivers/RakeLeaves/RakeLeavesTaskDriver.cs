using System;
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

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        [Header("Task Policy")]
        [SerializeField] private bool enforceTimeLimit = true;

        private Pose initialHoePose;
        private bool hasInitialHoePose;
        private bool wasHoeGrabbedLastFrame;

        public event Action<bool> HoeGrabStateChanged;
        public event Action<RakedLeafProgressReporter, float> RakeProgressAccepted;
        public event Action<float> HoeReturned;

        public override string TaskId => "rake_leaves";
        public string PickupHoeStepId => pickupHoeStepId;
        public string RakeLeavesStepId => rakeLeavesStepId;
        public string ReturnHoeStepId => returnHoeStepId;
        public Transform HoeTransform => hoeObject != null ? hoeObject.transform : null;
        public bool IsHoeCurrentlyGrabbed => IsHoeGrabbed();
        public float ReturnDistanceThreshold => Mathf.Max(0.01f, returnDistanceThreshold);

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            SimTask?.SetFailOnTimeout(enforceTimeLimit);
            SimTask?.SetObjectiveMaxValue(rakeLeavesStepId, profile.requiredRakedLeaves);
        }

        private void Awake()
        {
            ResolveHoeReferences();
            CaptureInitialHoePoseIfNeeded();
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
        }

        private void OnEnable()
        {
            ResolveHoeReferences();
            CaptureInitialHoePoseIfNeeded();
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
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
            if (isHoeGrabbed != wasHoeGrabbedLastFrame)
            {
                HoeGrabStateChanged?.Invoke(isHoeGrabbed);
            }

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
            return RecordRakeProgress(null, delta);
        }

        public bool RakeLeaves(RakedLeafProgressReporter source, float delta = 1f)
        {
            return RecordRakeProgress(source, delta);
        }

        public bool CollectLeaf(float delta = 1f)
        {
            return RakeLeaves(delta);
        }

        public bool CompleteReturnHoeStep()
        {
            return !string.IsNullOrWhiteSpace(returnHoeStepId) && TryCompleteStep(returnHoeStepId);
        }

        public int ClampDifficultyLevel(int level)
        {
            return Mathf.Clamp(level, 1, GetHighestConfiguredDifficultyLevel());
        }

        public void ApplyBackendTimeout(int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
            {
                return;
            }

            SimTask?.SetTimeLimitSeconds(timeoutSeconds);
            SimTask?.SetFailOnTimeout(enforceTimeLimit);
        }

        public float DistanceFromHoeStart()
        {
            return hoeObject != null && hasInitialHoePose
                ? Vector3.Distance(hoeObject.transform.position, initialHoePose.position)
                : 0f;
        }

        private bool RecordRakeProgress(RakedLeafProgressReporter source, float delta)
        {
            if (string.IsNullOrWhiteSpace(rakeLeavesStepId) || delta <= 0f)
            {
                return false;
            }

            bool accepted = TryAddStepProgress(rakeLeavesStepId, delta);
            if (accepted)
            {
                RakeProgressAccepted?.Invoke(source, delta);
            }

            return accepted;
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

            HoeReturned?.Invoke(distance);
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

        private DifficultyProfile ResolveDifficultyProfile(int level)
        {
            if (difficultyProfiles == null || difficultyProfiles.Length == 0)
            {
                difficultyProfiles = CreateDefaultDifficultyProfiles();
            }

            int requestedLevel = Mathf.Max(1, level);
            for (int i = 0; i < difficultyProfiles.Length; i++)
            {
                DifficultyProfile profile = difficultyProfiles[i];
                if (profile != null && profile.level == requestedLevel)
                {
                    return profile;
                }
            }

            return difficultyProfiles[0];
        }

        private int GetHighestConfiguredDifficultyLevel()
        {
            if (difficultyProfiles == null || difficultyProfiles.Length == 0)
            {
                difficultyProfiles = CreateDefaultDifficultyProfiles();
            }

            int highestLevel = 1;
            for (int i = 0; i < difficultyProfiles.Length; i++)
            {
                DifficultyProfile profile = difficultyProfiles[i];
                if (profile != null)
                {
                    highestLevel = Mathf.Max(highestLevel, profile.level);
                }
            }

            return highestLevel;
        }

        private void EnsureTaskTracker()
        {
            if (GetComponent<RakeLeavesTaskTracker>() == null)
            {
                gameObject.AddComponent<RakeLeavesTaskTracker>();
            }
        }

        private void EnsureAdaptiveReporter()
        {
            if (GetComponent<RakeLeavesTaskAdaptiveReporter>() == null)
            {
                gameObject.AddComponent<RakeLeavesTaskAdaptiveReporter>();
            }
        }

        private static DifficultyProfile[] CreateDefaultDifficultyProfiles()
        {
            return new[]
            {
                DifficultyProfile.Current(1),
                DifficultyProfile.Current(2),
                DifficultyProfile.Current(3)
            };
        }

        [Serializable]
        private sealed class DifficultyProfile
        {
            public int level = 1;
            [Min(0f)] public float timeLimitSeconds = 10f;
            [Min(1f)] public float requiredRakedLeaves = 1f;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile { level = level };
            }
        }
    }
}
