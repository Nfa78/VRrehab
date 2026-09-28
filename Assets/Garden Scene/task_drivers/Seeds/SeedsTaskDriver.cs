using System;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SeedsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickupStepId = "pick_seeds";
        [SerializeField] private string throwStepId = "throw_seeds";
        [SerializeField] private string returnStepId = "return_bucket";

        [Header("Difficulty Targets")]
        [SerializeField] private bool autoFindDifficultyTargets = true;
        [SerializeField] private SeedPickupState seedPickupState;
        [SerializeField] private SeedGateSequence seedGateSequence;
        [SerializeField] private SeedThrowSpawner seedThrowSpawner;
        [SerializeField] private SeedThrowArrow seedThrowArrow;

        [Header("Soil Regions")]
        [Tooltip("Assign up to four transparent soil cubes. They configure themselves as seed targets at runtime.")]
        [SerializeField] private GameObject[] soilRegionObjects = new GameObject[4];
        [SerializeField] [Min(1)] private int seedsPerRegion = 5;
        [SerializeField] private Color emptySoilRegionColor = new Color(0.25f, 0.55f, 1f, 0.2f);
        [SerializeField] private Color fullSoilRegionColor = new Color(0.25f, 1f, 0.4f, 0.5f);
        [SerializeField] [Min(0.01f)] private float soilRegionMetallicPulseSpeed = 1f;
        [SerializeField] private bool disableLegacyGatesWhenUsingSoilRegions = true;

        private SeedSoilRegion[] soilRegions = Array.Empty<SeedSoilRegion>();

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        public override string TaskId => "throw_seeds";
        public string PickupStepId => pickupStepId;
        public string ThrowStepId => throwStepId;
        public string ReturnStepId => returnStepId;

        private void Awake()
        {
            ConfigureSoilRegions();
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
        }

        private void OnEnable()
        {
            ConfigureSoilRegions();
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
        }

        public override void OnTaskStarted()
        {
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

            if (seedPickupState != null)
            {
                seedPickupState.ApplyDifficulty(profile.seedPickupRadius);
            }

            if (seedGateSequence != null && !HasSoilRegions)
            {
                seedGateSequence.ApplyDifficulty(
                    profile.activeGateCount,
                    profile.resetSequenceOnWrongGate,
                    profile.gateRadiusScale);
            }

            if (seedThrowSpawner != null)
            {
                seedThrowSpawner.ApplyDifficulty(
                    profile.seedSpawnCount,
                    profile.throwForceMin,
                    profile.throwForceMax,
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
            return SetThrowProgress(nextProgress);
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

            if (seedPickupState == null)
            {
                seedPickupState = FindFirstObjectByType<SeedPickupState>(FindObjectsInactive.Include);
            }

            if (seedGateSequence == null)
            {
                seedGateSequence = FindFirstObjectByType<SeedGateSequence>(FindObjectsInactive.Include);
            }

            if (seedThrowSpawner == null)
            {
                seedThrowSpawner = FindFirstObjectByType<SeedThrowSpawner>(FindObjectsInactive.Include);
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
                SetLegacyGateSequenceActive(true);
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
            SetLegacyGateSequenceActive(!HasSoilRegions);
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

        private void SetLegacyGateSequenceActive(bool active)
        {
            if (!disableLegacyGatesWhenUsingSoilRegions)
            {
                return;
            }

            ResolveDifficultyTargets();
            if (seedGateSequence != null && seedGateSequence.gameObject.activeSelf != active)
            {
                seedGateSequence.gameObject.SetActive(active);
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
            return !string.IsNullOrWhiteSpace(throwStepId) &&
                   SimTask != null &&
                   SimManager != null &&
                   SimManager.IsRunning &&
                   SimTask.SetObjectiveProgress(throwStepId, Mathf.Max(0f, value), SimManager.CurrentClock);
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
            [Min(0.01f)] public float gateRadiusScale = 1f;
            [Min(0)] public int activeGateCount = 0;
            public bool resetSequenceOnWrongGate = true;
            [Min(1)] public int seedSpawnCount = 10;
            [Min(0f)] public float throwForceMin = 1.2f;
            [Min(0f)] public float throwForceMax = 2.2f;
            [Range(0f, 89f)] public float throwConeHalfAngleDeg = 6f;
            public bool showThrowDirectionArrow = true;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile { level = level };
            }
        }
    }
}
