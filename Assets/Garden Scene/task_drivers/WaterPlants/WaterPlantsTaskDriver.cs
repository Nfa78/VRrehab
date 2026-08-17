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

        public override string TaskId => "water_plants";

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            SimTask?.SetObjectiveMaxValue(firstPlantStepId, profile.requiredWaterHitsPerPlant);
            SimTask?.SetObjectiveMaxValue(secondPlantStepId, profile.requiredWaterHitsPerPlant);

            ResolveDifficultyTargets();

            for (int i = 0; i < waterSpills.Length; i++)
            {
                if (waterSpills[i] != null)
                {
                    waterSpills[i].ApplyDifficulty(profile.spillDuration, profile.tiltThreshold);
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

        public bool SetWaterCanHeld(bool isHeld)
        {
            return !string.IsNullOrWhiteSpace(pickStepId) && TrySetStepState(pickStepId, isHeld);
        }

        public bool CompletePickStep()
        {
            return !string.IsNullOrWhiteSpace(pickStepId) && TryCompleteStep(pickStepId);
        }

        public bool WaterPlant(string stepId, float delta = 1f)
        {
            string targetStepId = stepId;
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

            return !string.IsNullOrWhiteSpace(targetStepId) && TryAddStepProgress(targetStepId, delta);
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

        private bool IsReturnZoneForThisTask(TSObjectiveReturnZone returnZone)
        {
            return returnZone != null &&
                   !string.IsNullOrWhiteSpace(returnStepId) &&
                   string.Equals(returnZone.ObjectiveId, returnStepId, StringComparison.Ordinal);
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
            [Min(1f)] public float requiredWaterHitsPerPlant = 1f;
            [Min(0.01f)] public float waterTriggerRadius = 0.2f;
            [Min(0.01f)] public float waterTriggerLength = 1f;
            [Min(0.01f)] public float plantHitboxScale = 1f;
            [Range(0f, 180f)] public float tiltThreshold = 35f;
            [Min(0.01f)] public float spillDuration = 1.1f;
            [Min(0f)] public float returnZoneRadius = 0.35f;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile { level = level };
            }
        }
    }
}
