using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class RakedLeafProgressReporter : MonoBehaviour
    {
        [SerializeField] private float progressDelta = 1f;
        [SerializeField] private bool logDebug;

        private RakeLeavesTaskDriver taskDriver;
        private bool reported;
        private bool loggedMissingDriver;

        private void OnEnable()
        {
            ResolveTaskDriver();
        }

        public bool ReportRaked()
        {
            if (reported || !ResolveTaskDriver() || !taskDriver.IsRakeLeavesStepActive())
            {
                return false;
            }

            bool updated = taskDriver.RakeLeaves(this, progressDelta);
            if (!updated)
            {
                return false;
            }

            reported = true;

            if (logDebug)
            {
                Debug.Log(
                    $"[RakedLeafProgressReporter] Counted {name} as raked from hoe impulse.",
                    this);
            }

            return true;
        }

        public void Configure(float delta)
        {
            progressDelta = Mathf.Max(0f, delta);
        }

        private bool ResolveTaskDriver()
        {
            if (taskDriver != null)
            {
                return true;
            }

            taskDriver = FindFirstObjectByType<RakeLeavesTaskDriver>();
            if (taskDriver != null)
            {
                return true;
            }

            if (!loggedMissingDriver)
            {
                loggedMissingDriver = true;
                Debug.LogWarning(
                    $"[RakedLeafProgressReporter] {name} cannot report rake progress because no RakeLeavesTaskDriver was found.",
                    this);
            }

            return false;
        }
    }
}
