using System;
using SoundSystem;
using UnityEngine;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RakedLeafHoeImpulse : MonoBehaviour
    {
        public static event Action<RakedLeafHoeImpulse, float> AnyHoeImpulseApplied;

        [SerializeField] private string hoeTag = "Hoe";
        [SerializeField] private float impulse = 0.22f;
        [SerializeField] private bool keepImpulseHorizontal = true;
        [SerializeField] private float minimumImpulseIntervalSeconds = 0.05f;
        [SerializeField, Min(0.1f)] private float scaleDownDurationSeconds = 3f;
        [SerializeField] private bool logDebug;

        private Rigidbody leafRigidbody;
        private float lastImpulseTime = -999f;
        private bool scaleDownStarted;
        private Vector3 initialScale;

        private void Awake()
        {
            ResolveRigidbody();
            initialScale = transform.localScale;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryApplyHoeImpulse(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryApplyHoeImpulse(collision);
        }

        public void Configure(string targetHoeTag, float impulseMagnitude)
        {
            hoeTag = string.IsNullOrWhiteSpace(targetHoeTag) ? "Hoe" : targetHoeTag;
            impulse = Mathf.Max(0f, impulseMagnitude);
        }

        private void TryApplyHoeImpulse(Collision collision)
        {
            if (collision == null || impulse <= 0f || !IsHoeCollision(collision))
            {
                return;
            }

            if (Time.time - lastImpulseTime < minimumImpulseIntervalSeconds)
            {
                return;
            }

            ResolveRigidbody();
            if (leafRigidbody == null || leafRigidbody.isKinematic)
            {
                return;
            }

            if (scaleDownStarted)
            {
                return;
            }

            Vector3 direction = ResolvePushDirection(collision);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            leafRigidbody.AddForce(direction.normalized * impulse, ForceMode.Impulse);
            lastImpulseTime = Time.time;
            scaleDownStarted = true;

            RakedLeafProgressReporter reporter = GetComponent<RakedLeafProgressReporter>();
            reporter?.ReportRaked();
            EventSoundManager.TryPlayAt("garden.leaf.raked", transform.position);
            StartCoroutine(ScaleDownAfterRake());
            AnyHoeImpulseApplied?.Invoke(this, impulse);

            if (logDebug)
            {
                Debug.Log(
                    $"[RakedLeafHoeImpulse] Applied hoe impulse to {name}. Direction={direction.normalized}, impulse={impulse:F2}.",
                    this);
            }
        }

        private System.Collections.IEnumerator ScaleDownAfterRake()
        {
            float duration = Mathf.Max(0.1f, scaleDownDurationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            if (leafRigidbody != null)
            {
                leafRigidbody.isKinematic = true;
                leafRigidbody.detectCollisions = false;
            }
        }

        private Vector3 ResolvePushDirection(Collision collision)
        {
            Vector3 direction = Vector3.zero;

            ContactPoint contact = collision.GetContact(0);
            Rigidbody hoeRigidbody = collision.rigidbody;
            if (hoeRigidbody != null)
            {
                Vector3 hoeVelocity = hoeRigidbody.GetPointVelocity(contact.point);
                Vector3 leafVelocity = leafRigidbody.GetPointVelocity(contact.point);
                direction = hoeVelocity - leafVelocity;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = leafRigidbody.worldCenterOfMass - contact.point;
            }

            if (keepImpulseHorizontal)
            {
                direction.y = 0f;
            }

            return direction;
        }

        private bool IsHoeCollision(Collision collision)
        {
            Transform current = collision.collider != null ? collision.collider.transform : collision.transform;
            while (current != null)
            {
                if (current.CompareTag(hoeTag))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void ResolveRigidbody()
        {
            if (leafRigidbody == null)
            {
                leafRigidbody = GetComponent<Rigidbody>();
            }
        }
    }
}
