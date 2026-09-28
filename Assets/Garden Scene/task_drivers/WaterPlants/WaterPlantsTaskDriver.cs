using System;
using TriggerSystem;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class WaterPlantsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickStepId = "pick";
        [SerializeField] private string firstPlantStepId = "wp1";
        [SerializeField] private string secondPlantStepId = "wp2";
        [SerializeField] private string returnStepId = "return_can";

        [Header("Difficulty Targets")]
        [SerializeField] private bool autoFindDifficultyTargets = true;
        [SerializeField] private WaterSpill[] waterSpills;
        [SerializeField] private WaterSpillSetup[] waterSpillSetups;
        [SerializeField] private FlowerPot[] flowerPots;
        [SerializeField] private TSObjectiveReturnZone[] returnZones;

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        [Header("Adaptive Difficulty")]
        [SerializeField] private bool increaseDifficultyAfterFastFirstPlant = true;
        [SerializeField] [Min(0.01f)] private float firstPlantFastCompletionThresholdSeconds = 10f;

        private bool hasAdjustedDifficultyAfterFirstPlant;

        public override string TaskId => "water_plants";
        public string PickStepId => pickStepId;
        public string FirstPlantStepId => firstPlantStepId;
        public string SecondPlantStepId => secondPlantStepId;
        public string ReturnStepId => returnStepId;

        private void Awake()
        {
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
        }

        private void OnEnable()
        {
            EnsureTaskTracker();
            EnsureAdaptiveReporter();
        }

        public override void OnTaskStarted()
        {
            hasAdjustedDifficultyAfterFirstPlant = false;
            base.OnTaskStarted();
        }

        public override void OnTaskStopped()
        {
            hasAdjustedDifficultyAfterFirstPlant = false;
            base.OnTaskStopped();
        }

        public override void OnTaskCompleted()
        {
            hasAdjustedDifficultyAfterFirstPlant = false;
            base.OnTaskCompleted();
        }

        public override void OnTaskFailed(string failureReason)
        {
            hasAdjustedDifficultyAfterFirstPlant = false;
            base.OnTaskFailed(failureReason);
        }

        public override void OnStepChanged(SimTaskObjective step, int stepIndex)
        {
            base.OnStepChanged(step, stepIndex);

            if (!increaseDifficultyAfterFastFirstPlant || hasAdjustedDifficultyAfterFirstPlant)
            {
                return;
            }

            if (step == null || !string.Equals(step.ObjectiveId, secondPlantStepId, StringComparison.Ordinal))
            {
                return;
            }

            TryIncreaseDifficultyAfterFastFirstPlant();
        }

        public override void OnTaskResetToStep(SimTaskObjective step, int stepIndex)
        {
            hasAdjustedDifficultyAfterFirstPlant = false;
            base.OnTaskResetToStep(step, stepIndex);
        }

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            float requiredWateringSecondsPerPlant = profile.requiredWateringSecondsPerPlant > 0f
                ? Mathf.Max(0.25f, profile.requiredWateringSecondsPerPlant)
                : 3f;

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            SimTask?.SetObjectiveMaxValue(firstPlantStepId, requiredWateringSecondsPerPlant);
            SimTask?.SetObjectiveMaxValue(secondPlantStepId, requiredWateringSecondsPerPlant);

            ResolveDifficultyTargets();

            for (int i = 0; i < waterSpills.Length; i++)
            {
                if (waterSpills[i] != null)
                {
                    waterSpills[i].ApplyDifficulty(profile.pourStartDelay, profile.tiltThreshold);
                }
            }

            for (int i = 0; i < waterSpillSetups.Length; i++)
            {
                if (waterSpillSetups[i] != null)
                {
                    waterSpillSetups[i].ApplyDifficulty(profile.waterTriggerRadius, profile.waterTriggerLength);
                }
            }

            for (int i = 0; i < flowerPots.Length; i++)
            {
                if (flowerPots[i] != null)
                {
                    flowerPots[i].ApplyDifficulty(profile.plantHitboxScale);
                }
            }

            for (int i = 0; i < returnZones.Length; i++)
            {
                if (returnZones[i] != null && IsReturnZoneForThisTask(returnZones[i]))
                {
                    returnZones[i].ApplyDifficulty(profile.returnZoneRadius);
                }
            }
        }

        public int ClampDifficultyLevel(int level)
        {
            return Mathf.Clamp(level, 1, GetHighestConfiguredDifficultyLevel());
        }

        public bool SetWaterCanHeld(bool isHeld)
        {
            return !string.IsNullOrWhiteSpace(pickStepId) && TrySetStepState(pickStepId, isHeld);
        }

        public bool CompletePickStep()
        {
            return !string.IsNullOrWhiteSpace(pickStepId) && TryCompleteStep(pickStepId);
        }

        public bool WaterPlant(string stepId, float wateringSeconds)
        {
            if (SimManager == null || SimTask == null || !SimManager.IsRunning)
            {
                return false;
            }

            if (!TryResolveWaterTargetStepId(stepId, out string targetStepId))
            {
                return false;
            }

            return TryAddStepProgress(targetStepId, Mathf.Max(0.01f, wateringSeconds));
        }

        public bool CompleteReturnStep()
        {
            return !string.IsNullOrWhiteSpace(returnStepId) && TryCompleteStep(returnStepId);
        }

        private void TryIncreaseDifficultyAfterFastFirstPlant()
        {
            SimTaskObjective firstPlantObjective = SimTask?.GetObjective(firstPlantStepId);
            if (firstPlantObjective == null || !firstPlantObjective.IsCompleted)
            {
                return;
            }

            hasAdjustedDifficultyAfterFirstPlant = true;

            float completionSeconds = Mathf.Max(0f, firstPlantObjective.CompletedAtSeconds - firstPlantObjective.StartedAtSeconds);
            float thresholdSeconds = Mathf.Max(0.01f, firstPlantFastCompletionThresholdSeconds);
            if (completionSeconds >= thresholdSeconds)
            {
                return;
            }

            int nextDifficultyLevel = Mathf.Min(DifficultyLevel + 1, GetHighestConfiguredDifficultyLevel());
            if (nextDifficultyLevel <= DifficultyLevel)
            {
                return;
            }

            DifficultyLevel = nextDifficultyLevel;
            ApplyDifficulty(nextDifficultyLevel);
        }

        private void ResolveDifficultyTargets()
        {
            if (!autoFindDifficultyTargets)
            {
                return;
            }

            if (waterSpills == null || waterSpills.Length == 0)
            {
                waterSpills = FindObjectsByType<WaterSpill>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (waterSpillSetups == null || waterSpillSetups.Length == 0)
            {
                waterSpillSetups = FindObjectsByType<WaterSpillSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (flowerPots == null || flowerPots.Length == 0)
            {
                flowerPots = FindObjectsByType<FlowerPot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (returnZones == null || returnZones.Length == 0)
            {
                returnZones = FindObjectsByType<TSObjectiveReturnZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        private bool TryResolveWaterTargetStepId(string stepId, out string targetStepId)
        {
            targetStepId = stepId;
            if (string.IsNullOrWhiteSpace(targetStepId))
            {
                if (IsActiveStep(firstPlantStepId))
                {
                    targetStepId = firstPlantStepId;
                }
                else if (IsActiveStep(secondPlantStepId))
                {
                    targetStepId = secondPlantStepId;
                }
            }

            return !string.IsNullOrWhiteSpace(targetStepId) && IsActiveStep(targetStepId);
        }

        private bool IsReturnZoneForThisTask(TSObjectiveReturnZone returnZone)
        {
            return returnZone != null &&
                   !string.IsNullOrWhiteSpace(returnStepId) &&
                   string.Equals(returnZone.ObjectiveId, returnStepId, StringComparison.Ordinal);
        }

        private void EnsureTaskTracker()
        {
            if (GetComponent<WateringTaskTracker>() == null)
            {
                gameObject.AddComponent<WateringTaskTracker>();
            }
        }

        private void EnsureAdaptiveReporter()
        {
            if (GetComponent<WateringTaskAdaptiveReporter>() == null)
            {
                gameObject.AddComponent<WateringTaskAdaptiveReporter>();
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
            [Min(0.25f)] public float requiredWateringSecondsPerPlant = 3f;
            [Min(0.01f)] public float waterTriggerRadius = 0.2f;
            [Min(0.01f)] public float waterTriggerLength = 1f;
            [Min(0.01f)] public float plantHitboxScale = 1f;
            [Range(0f, 180f)] public float tiltThreshold = 35f;
            [Min(0f)] public float pourStartDelay = 0.15f;
            [Min(0f)] public float returnZoneRadius = 0.35f;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile
                {
                    level = level,
                    plantHitboxScale = ResolveDefaultPlantHitboxScale(level)
                };
            }

            private static float ResolveDefaultPlantHitboxScale(int level)
            {
                switch (Mathf.Max(1, level))
                {
                    case 1:
                        return 2f;
                    case 2:
                        return 1.3333334f;
                    default:
                        return 1f;
                }
            }
        }
    }
}
