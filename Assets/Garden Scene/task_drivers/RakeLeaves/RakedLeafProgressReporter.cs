using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class RakedLeafProgressReporter : MonoBehaviour
    {
        [SerializeField] private float minSuccessZ = 6.7f;
        [SerializeField] private float maxSuccessZ = 7.8f;
        [SerializeField] private float progressDelta = 1f;
        [SerializeField] private bool requireInsideBandBeforeCounting = true;
        [SerializeField] private bool logDebug;

        private RakeLeavesTaskDriver taskDriver;
        private bool hasBeenInsideBand;
        private bool reported;
        private bool loggedMissingDriver;

        private void OnEnable()
        {
            hasBeenInsideBand = IsInsideSuccessBand();
            ResolveTaskDriver();
        }

        private void Update()
        {
            if (reported)
            {
                return;
            }

            bool insideBand = IsInsideSuccessBand();
            if (insideBand)
            {
                hasBeenInsideBand = true;
                return;
            }

            if (requireInsideBandBeforeCounting && !hasBeenInsideBand)
            {
                return;
            }

            if (!ResolveTaskDriver() || !taskDriver.IsRakeLeavesStepActive())
            {
                return;
            }

            bool updated = taskDriver.RakeLeaves(progressDelta);
            if (!updated)
            {
                return;
            }

            reported = true;

            if (logDebug)
            {
                Debug.Log(
                    $"[RakedLeafProgressReporter] Counted {name} as raked. z={transform.position.z:F2}, valid band={minSuccessZ:F2}-{maxSuccessZ:F2}.",
                    this);
            }
        }

        public void Configure(float minZ, float maxZ, float delta)
        {
            minSuccessZ = Mathf.Min(minZ, maxZ);
            maxSuccessZ = Mathf.Max(minZ, maxZ);
            progressDelta = Mathf.Max(0f, delta);
            hasBeenInsideBand = IsInsideSuccessBand();
        }

        private bool IsInsideSuccessBand()
        {
            float z = transform.position.z;
            return z >= minSuccessZ && z <= maxSuccessZ;
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
