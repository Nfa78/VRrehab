using System;
using SoundSystem;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SeedsTaskDriver : SimTaskDriver
    {
        public event Action<float> ThrowSucceeded;

        [SerializeField] private string pickupStepId = "pick_seeds";
        [SerializeField] private string throwStepId = "throw_seeds";
        [SerializeField] private string returnStepId = "return_bucket";

        [Header("Difficulty Targets")]
        [SerializeField] private bool autoFindDifficultyTargets = true;
        [SerializeField] private SeedInteraction seedInteraction;
        [SerializeField] private SeedThrowingSystem seedThrowingSystem;
        [SerializeField] private SeedThrowArrow seedThrowArrow;
        private bool hasPlayedReturnSound;

        [Header("Soil Regions")]
        [Tooltip("Assign up to four transparent soil cubes. They configure themselves as seed targets at runtime.")]
        [SerializeField] private GameObject[] soilRegionObjects = new GameObject[4];
        [SerializeField] [Min(1)] private int seedsPerRegion = 5;
        [SerializeField] private Color emptySoilRegionColor = new Color(0.25f, 0.55f, 1f, 0.2f);
        [SerializeField] private Color fullSoilRegionColor = new Color(0.25f, 1f, 0.4f, 0.5f);
        [SerializeField] [Min(0.01f)] private float soilRegionMetallicPulseSpeed = 1f;

        private SeedSoilRegion[] soilRegions = Array.Empty<SeedSoilRegion>();

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        public override string TaskId => "throw_seeds";
        public string PickupStepId => pickupStepId;
        public string ThrowStepId => throwStepId;
        public string ReturnStepId => returnStepId;

        private void Update()
        {
            if (hasPlayedReturnSound || SimManager == null || SimTask == null || !SimManager.IsRunning)
            {
                return;
            }

            SimTaskObjective returnObjective = SimTask.GetObjective(returnStepId);
            if (returnObjective == null || !returnObjective.IsCompleted)
            {
                return;
            }

            hasPlayedReturnSound = true;
            EventSoundManager.TryPlayAt("garden.bucket.returned", transform.position);
        }

        private void Awake()
        {
            ConfigureSoilRegions();
            EnsureTaskTracker();
            EnsureSeedInteraction();
            EnsureAdaptiveReporter();
        }

        private void OnEnable()
        {
            ConfigureSoilRegions();
            EnsureTaskTracker();
            EnsureSeedInteraction();
            EnsureAdaptiveReporter();
        }

        public override void OnTaskStarted()
        {
            hasPlayedReturnSound = false;
            ConfigureSoilRegions();
            ResetSoilRegions();
            base.OnTaskStarted();
        }

        public override void OnTaskResetToStep(SimTaskObjective step, int stepIndex)
        {
            ResetSoilRegions();
            base.OnTaskResetToStep(step, stepIndex);
        }

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            ResolveDifficultyTargets();
            ConfigureSoilRegions();

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            float requiredSuccessfulThrows = HasSoilRegions
                ? soilRegions.Length * Mathf.Max(1, seedsPerRegion)
                : profile.requiredSuccessfulThrows;
            SimTask?.SetObjectiveMaxValue(throwStepId, requiredSuccessfulThrows);

            if (seedInteraction != null)
            {
                seedInteraction.ApplyDifficulty(profile.seedPickupRadius);
            }

            if (seedThrowingSystem != null)
            {
                seedThrowingSystem.ApplyDifficulty(
                    profile.seedSpawnCount,
                    profile.throwConeHalfAngleDeg);
            }

            if (seedThrowArrow != null)
            {
                seedThrowArrow.ApplyDifficulty(profile.showThrowDirectionArrow);
            }
        }

        public int ClampDifficultyLevel(int level)
        {
            return Mathf.Clamp(level, 1, GetHighestConfiguredDifficultyLevel());
        }

        public bool AllowsSeedInteraction()
        {
            return IsActiveStep(pickupStepId) || IsActiveStep(throwStepId);
        }

        public bool IsPickupStepActive()
        {
            return IsActiveStep(pickupStepId);
        }

        public bool IsThrowStepActive()
        {
            return IsActiveStep(throwStepId);
        }

        public bool HandleSeedsPickedUp()
        {
            return !string.IsNullOrWhiteSpace(pickupStepId) && TryCompleteStep(pickupStepId);
        }

        public bool HandleThrowSuccess(float delta = 1f)
        {
            if (string.IsNullOrWhiteSpace(throwStepId))
            {
                return false;
            }

            float nextProgress = GetThrowProgressValue() + Mathf.Max(0f, delta);
            bool accepted = SetThrowProgress(nextProgress);
            if (accepted && delta > 0f)
            {
                ThrowSucceeded?.Invoke(delta);
            }

            return accepted;
        }

        public bool CompleteThrowStep()
        {
            return !string.IsNullOrWhiteSpace(throwStepId) && TryCompleteStep(throwStepId);
        }

        public bool CompleteReturnStep()
        {
            return !string.IsNullOrWhiteSpace(returnStepId) && TryCompleteStep(returnStepId);
        }

        private void ResolveDifficultyTargets()
        {
            if (!autoFindDifficultyTargets)
            {
                return;
            }

            if (seedInteraction == null)
            {
                seedInteraction = FindFirstObjectByType<SeedInteraction>(FindObjectsInactive.Include);
            }

            if (seedThrowingSystem == null)
            {
                seedThrowingSystem = FindFirstObjectByType<SeedThrowingSystem>(FindObjectsInactive.Include);
            }

            if (seedThrowArrow == null)
            {
                seedThrowArrow = FindFirstObjectByType<SeedThrowArrow>(FindObjectsInactive.Include);
            }
        }

        private bool HasSoilRegions => soilRegions != null && soilRegions.Length > 0;

        private void ConfigureSoilRegions()
        {
            if (soilRegionObjects == null || soilRegionObjects.Length == 0)
            {
                soilRegions = Array.Empty<SeedSoilRegion>();
                return;
            }

            var configuredRegions = new System.Collections.Generic.List<SeedSoilRegion>(soilRegionObjects.Length);
            for (int i = 0; i < soilRegionObjects.Length; i++)
            {
                GameObject regionObject = soilRegionObjects[i];
                if (regionObject == null)
                {
                    continue;
                }

                SeedSoilRegion region = regionObject.GetComponent<SeedSoilRegion>();
                if (region == null)
                {
                    region = regionObject.AddComponent<SeedSoilRegion>();
                }

                region.Configure(
                    this,
                    seedsPerRegion,
                    emptySoilRegionColor,
                    fullSoilRegionColor,
                    soilRegionMetallicPulseSpeed);
                configuredRegions.Add(region);
            }

            soilRegions = configuredRegions.ToArray();
        }

        private void ResetSoilRegions()
        {
            for (int i = 0; i < soilRegions.Length; i++)
            {
                if (soilRegions[i] != null)
                {
                    soilRegions[i].ResetFill();
                }
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

        public bool SetThrowProgress(float value)
        {
            bool hasObjectiveId = !string.IsNullOrWhiteSpace(throwStepId);
            bool hasTask = SimTask != null;
            bool hasManager = SimManager != null;
            bool isRunning = hasManager && SimManager.IsRunning;
            SimTaskObjective objective = hasTask && hasObjectiveId ? SimTask.GetObjective(throwStepId) : null;
            bool hasObjective = objective != null;

            Debug.Log(
                $"[SeedsTaskDriver] SetThrowProgress requested={value:0.##}, " +
                $"throwStepId='{throwStepId}', hasTask={hasTask}, hasObjective={hasObjective}, " +
                $"hasSimManager={hasManager}, simRunning={isRunning}.",
                this);

            Debug.Log(
                $"[SeedsTaskDriver] Current objective='{SimTask?.CurrentObjective?.ObjectiveId}', " +
                $"requested='{throwStepId}'.",
                this);

            if (!hasObjectiveId || !hasTask || !hasManager || !isRunning || !hasObjective)
            {
                Debug.LogWarning("[SeedsTaskDriver] Throw progress was not applied because a task-progress precondition failed.", this);
                return false;
            }

            bool updated = SimTask.SetObjectiveProgress(
                throwStepId,
                Mathf.Max(0f, value),
                SimManager.CurrentClock);

            Debug.Log(
                $"[SeedsTaskDriver] Throw progress update result={updated}, " +
                $"currentValue={objective.CurrentValue:0.##}, maxValue={objective.MaxValue:0.##}.",
                this);
            return updated;
        }

        public float GetThrowProgressValue()
        {
            if (SimTask == null || string.IsNullOrWhiteSpace(throwStepId))
            {
                return 0f;
            }

            SimTaskObjective throwObjective = SimTask.GetObjective(throwStepId);
            return throwObjective != null ? throwObjective.CurrentValue : 0f;
        }

        private void EnsureTaskTracker()
        {
            if (GetComponent<SeedsTaskTracker>() == null)
            {
                gameObject.AddComponent<SeedsTaskTracker>();
            }
        }

        private void EnsureAdaptiveReporter()
        {
            if (GetComponent<SeedsTaskAdaptiveReporter>() == null)
            {
                gameObject.AddComponent<SeedsTaskAdaptiveReporter>();
            }
        }

        private void EnsureSeedInteraction()
        {
            if (FindFirstObjectByType<SeedInteraction>(FindObjectsInactive.Include) == null)
            {
                gameObject.AddComponent<SeedInteraction>();
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
            [Min(1f)] public float requiredSuccessfulThrows = 1f;
            [Min(0f)] public float seedPickupRadius = 0.35f;
            [Min(1)] public int seedSpawnCount = 10;
            [Range(0f, 89f)] public float throwConeHalfAngleDeg = 6f;
            public bool showThrowDirectionArrow = true;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile { level = level };
            }
        }
    }
}
