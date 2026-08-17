using System;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class CatchLeafsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickupStepId = "pick_bucket";
        [SerializeField] private string catchStepId = "catch_leafs";
        [SerializeField] private string returnStepId = "return_bucket";

        [Header("Difficulty Targets")]
        [SerializeField] private bool autoFindDifficultyTargets = true;
        [SerializeField] private BucketLeafCatchSystem bucketLeafCatchSystem;

        [Header("Difficulty Profiles")]
        [SerializeField] private DifficultyProfile[] difficultyProfiles = CreateDefaultDifficultyProfiles();

        public override string TaskId => "catch_leafs";

        public override void ApplyDifficulty(int level)
        {
            DifficultyProfile profile = ResolveDifficultyProfile(level);
            if (profile == null)
            {
                return;
            }

            SimTask?.SetTimeLimitSeconds(profile.timeLimitSeconds);
            SimTask?.SetObjectiveMaxValue(catchStepId, profile.requiredCaughtLeaves);

            ResolveDifficultyTargets();
            if (bucketLeafCatchSystem != null)
            {
                bucketLeafCatchSystem.ApplyDifficulty(
                    profile.leafRespawnDelaySeconds,
                    profile.leafFallSpeed,
                    profile.leafSwayAmount,
                    profile.leafDepthAmount);
            }
        }

        public bool AllowsBucketCatchFlow()
        {
            return IsActiveStep(pickupStepId) || IsActiveStep(catchStepId) || IsActiveStep(returnStepId);
        }

        public bool IsPickupStepActive()
        {
            return IsActiveStep(pickupStepId);
        }

        public bool IsCatchStepActive()
        {
            return IsActiveStep(catchStepId);
        }

        public bool IsReturnStepActive()
        {
            return IsActiveStep(returnStepId);
        }

        public bool CompletePickupStep()
        {
            return !string.IsNullOrWhiteSpace(pickupStepId) && TryCompleteStep(pickupStepId);
        }

        public bool CatchLeaf(float delta = 1f)
        {
            return !string.IsNullOrWhiteSpace(catchStepId) && TryAddStepProgress(catchStepId, delta);
        }

        public bool CompleteReturnStep()
        {
            return !string.IsNullOrWhiteSpace(returnStepId) && TryCompleteStep(returnStepId);
        }

        private void ResolveDifficultyTargets()
        {
            if (!autoFindDifficultyTargets || bucketLeafCatchSystem != null)
            {
                return;
            }

            bucketLeafCatchSystem = FindFirstObjectByType<BucketLeafCatchSystem>(FindObjectsInactive.Include);
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
            [Min(0f)] public float timeLimitSeconds = 20f;
            [Min(1f)] public float requiredCaughtLeaves = 5f;
            [Min(0f)] public float leafFallSpeed = 0.3f;
            [Min(0f)] public float leafSwayAmount = 0.5f;
            [Min(0f)] public float leafDepthAmount = 0.3f;
            [Min(0f)] public float leafRespawnDelaySeconds = 0.35f;

            public static DifficultyProfile Current(int level)
            {
                return new DifficultyProfile { level = level };
            }
        }
    }
}
