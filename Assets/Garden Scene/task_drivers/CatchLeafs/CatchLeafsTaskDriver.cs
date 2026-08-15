using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class CatchLeafsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickupStepId = "pick_bucket";
        [SerializeField] private string catchStepId = "catch_leafs";

        public override string TaskId => "catch_leafs";

        public bool AllowsBucketCatchFlow()
        {
            return IsActiveStep(pickupStepId) || IsActiveStep(catchStepId);
        }

        public bool IsPickupStepActive()
        {
            return IsActiveStep(pickupStepId);
        }

        public bool IsCatchStepActive()
        {
            return IsActiveStep(catchStepId);
        }

        public bool CompletePickupStep()
        {
            return !string.IsNullOrWhiteSpace(pickupStepId) && TryCompleteStep(pickupStepId);
        }

        public bool CatchLeaf(float delta = 1f)
        {
            return !string.IsNullOrWhiteSpace(catchStepId) && TryAddStepProgress(catchStepId, delta);
        }
    }
}
