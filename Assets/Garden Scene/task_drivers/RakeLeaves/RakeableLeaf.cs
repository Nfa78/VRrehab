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
        private bool hasBeenRaked;
        private Vector3 initialScale;
        private Coroutine scaleDownCoroutine;

        public bool IsInPile => isInPile;
        public Vector3 PlannedPosition => targetPosition;

        public void Initialize(RakeLeafFieldController fieldOwner, Vector3 startPosition)
        {
            owner = fieldOwner;
            groundY = startPosition.y;
            targetPosition = startPosition;
            isInPile = false;
            hasBeenRaked = false;
            initialScale = transform.localScale;
            transform.position = startPosition;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f));
            enabled = true;
        }

        public bool Rake(Vector3 direction, float distance, Bounds fieldBounds)
        {
            if (isInPile || direction.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return false;
            }

            Vector3 next = targetPosition + direction.normalized * distance;
            next.x = Mathf.Clamp(next.x, fieldBounds.min.x, fieldBounds.max.x);
            next.z = Mathf.Clamp(next.z, fieldBounds.min.z, fieldBounds.max.z);
            next.y = groundY;
            targetPosition = next;
            return true;
        }

        public void BeginRakeCooldown(float durationSeconds)
        {
            if (hasBeenRaked)
            {
                return;
            }

            hasBeenRaked = true;
            isInPile = true;
            if (scaleDownCoroutine != null)
            {
                StopCoroutine(scaleDownCoroutine);
            }

            scaleDownCoroutine = StartCoroutine(ScaleDownRoutine(durationSeconds));
        }

        private System.Collections.IEnumerator ScaleDownRoutine(float durationSeconds)
        {
            float duration = Mathf.Max(0.1f, durationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            scaleDownCoroutine = null;
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
