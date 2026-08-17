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

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        public override string TaskId => "throw_seeds";

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            SimTask?.SetObjectiveMaxValue(throwStepId, profile.requiredSuccessfulThrows);

            ResolveDifficultyTargets();

            if (seedPickupState != null)
            {
                seedPickupState.ApplyDifficulty(profile.seedPickupRadius);
            }

            if (seedGateSequence != null)
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
            return !string.IsNullOrWhiteSpace(throwStepId) && TryAddStepProgress(throwStepId, delta);
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
