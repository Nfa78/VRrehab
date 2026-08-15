using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class SeedsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickupStepId = "pick_seeds";
        [SerializeField] private string throwStepId = "throw_seeds";
        [SerializeField] private string returnStepId = "return_bucket";

        public override string TaskId => "throw_seeds";

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
    }
}
