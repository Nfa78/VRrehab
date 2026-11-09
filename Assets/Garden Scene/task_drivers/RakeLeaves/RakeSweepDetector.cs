using UnityEngine;

namespace TaskSystem
{
    /// <summary>
    /// Converts the rake head's deliberate planar movement into a sweep. This
    /// intentionally uses no collision callbacks, making it robust with hands
    /// and unaffected by decorative stone colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RakeSweepDetector : MonoBehaviour
    {
        [SerializeField] private Transform rakeRoot;
        [SerializeField] private RakeLeafFieldController fieldController;
        [SerializeField, Min(0.05f)] private float sweepWidth = 0.65f;
        [SerializeField, Min(0.02f)] private float sweepDepth = 0.22f;
        [SerializeField, Min(0f)] private float minimumSweepSpeed = 0.18f;

        private Collider[] rakeColliders;
        private Vector3 previousCenter;
        private bool hasPreviousCenter;

        public void Configure(Transform root, RakeLeafFieldController controller)
        {
            rakeRoot = root;
            fieldController = controller;
            rakeColliders = null;
            hasPreviousCenter = false;
        }

        private void FixedUpdate()
        {
            if (fieldController == null || !fieldController.CanAcceptRakeSweeps)
            {
                hasPreviousCenter = false;
                return;
            }

            Vector3 center = ResolveRakeHeadCenter();
            if (!hasPreviousCenter)
            {
                previousCenter = center;
                hasPreviousCenter = true;
                return;
            }

            Vector3 movement = center - previousCenter;
            movement.y = 0f;
            previousCenter = center;

            float speed = movement.magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            if (speed < minimumSweepSpeed)
            {
                return;
            }

            fieldController.ProcessSweep(center, movement.normalized, speed, sweepWidth, sweepDepth);
        }

        private Vector3 ResolveRakeHeadCenter()
        {
            if (rakeRoot == null)
            {
                rakeRoot = transform;
            }

            if (rakeColliders == null)
            {
                rakeColliders = rakeRoot.GetComponentsInChildren<Collider>(true);
            }

            Collider lowestCollider = null;
            float lowestY = float.PositiveInfinity;
            for (int i = 0; i < rakeColliders.Length; i++)
            {
                Collider candidate = rakeColliders[i];
                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                float candidateY = candidate.bounds.center.y - candidate.bounds.extents.y;
                if (candidateY < lowestY)
                {
                    lowestY = candidateY;
                    lowestCollider = candidate;
                }
            }

            return lowestCollider != null ? lowestCollider.bounds.center : rakeRoot.position;
        }
    }
}
