using UnityEngine;

namespace TaskSystem
{
    /// <summary>
    /// A visual leaf driven by the raking task. It deliberately has no physics
    /// body or enabled collider: the player, stones, and terrain must never be
    /// able to push or trap a leaf.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RakeableLeaf : MonoBehaviour
    {
        private RakeLeafFieldController owner;
        private Vector3 targetPosition;
        private float groundY;
        private bool isInPile;

        public bool IsInPile => isInPile;
        public Vector3 PlannedPosition => targetPosition;

        public void Initialize(RakeLeafFieldController fieldOwner, Vector3 startPosition)
        {
            owner = fieldOwner;
            groundY = startPosition.y;
            targetPosition = startPosition;
            isInPile = false;
            transform.position = startPosition;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f));
            enabled = true;
        }

        public void Rake(Vector3 direction, float distance, Bounds fieldBounds)
        {
            if (isInPile || direction.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return;
            }

            Vector3 next = targetPosition + direction.normalized * distance;
            next.x = Mathf.Clamp(next.x, fieldBounds.min.x, fieldBounds.max.x);
            next.z = Mathf.Clamp(next.z, fieldBounds.min.z, fieldBounds.max.z);
            next.y = groundY;
            targetPosition = next;
        }

        public void MoveToPile(Vector3 pilePosition)
        {
            if (isInPile)
            {
                return;
            }

            isInPile = true;
            targetPosition = pilePosition;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-15f, 15f));
        }

        private void Update()
        {
            if (owner == null)
            {
                return;
            }

            // A small interpolation reads as a leaf sliding over grass while
            // retaining deterministic, obstacle-free task behaviour.
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                owner.LeafVisualSpeed * Time.deltaTime);
        }
    }
}
