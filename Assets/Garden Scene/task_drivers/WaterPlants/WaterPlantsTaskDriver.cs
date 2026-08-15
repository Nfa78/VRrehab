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

        public override string TaskId => "water_plants";

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
    }
}
