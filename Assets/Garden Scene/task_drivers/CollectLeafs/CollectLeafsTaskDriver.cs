using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class CollectLeafsTaskDriver : SimTaskDriver
    {
        [SerializeField] private string pickStepId = "pick";
        [SerializeField] private string collectStepId = "throw";
        [SerializeField] private string milestoneStepId = "wp2";

        public override string TaskId => "rake_leafs";

        public bool SetToolHeld(bool isHeld)
        {
            return !string.IsNullOrWhiteSpace(pickStepId) && TrySetStepState(pickStepId, isHeld);
        }

        public bool CollectLeaf(float delta = 1f)
        {
            return !string.IsNullOrWhiteSpace(collectStepId) && TryAddStepProgress(collectStepId, delta);
        }

        public bool AdvanceMilestone(float delta = 1f)
        {
            return !string.IsNullOrWhiteSpace(milestoneStepId) && TryAddStepProgress(milestoneStepId, delta);
        }
    }
}
