using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

public class FlowerPot : MonoBehaviour
{
    [SerializeField] private SimObjectiveInteraction simObjectiveInteraction;
    [SerializeField] private WaterPlantsTaskDriver taskDriver;
    [SerializeField] private bool logWaterHits = true;
    [SerializeField] private float particleHitCooldown = 0.25f;

    private float nextAllowedHitTime;
    private readonly List<ColliderSizeState> colliderSizeStates = new List<ColliderSizeState>();

    private void Awake()
    {
        if (simObjectiveInteraction == null)
        {
            simObjectiveInteraction = GetComponent<SimObjectiveInteraction>();
        }

        if (taskDriver == null)
        {
            taskDriver = FindFirstObjectByType<WaterPlantsTaskDriver>();
        }

        if (simObjectiveInteraction == null)
        {
            Debug.LogWarning("FlowerPot has no SimObjectiveInteraction assigned.", this);
        }

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider == null)
        {
            Debug.LogWarning("FlowerPot has no Collider on the same GameObject. Particle collisions may never fire here.", this);
        }
        else
        {
            if (ownCollider.isTrigger)
            {
                ownCollider.isTrigger = false;
                Debug.LogWarning(
                    $"[FlowerPot] Switched {ownCollider.GetType().Name} on {name} to isTrigger=false because particle collisions require a non-trigger collider.",
                    this);
            }

            if (logWaterHits)
            {
                Debug.Log($"[FlowerPot] Using collider {ownCollider.GetType().Name} on {name}. isTrigger={ownCollider.isTrigger}", this);
            }
        }
    }

    public void ApplyDifficulty(float hitboxScale)
    {
        CacheColliderSizeStatesIfNeeded();

        float scale = Mathf.Max(0.01f, hitboxScale);
        for (int i = 0; i < colliderSizeStates.Count; i++)
        {
            colliderSizeStates[i].Apply(scale);
        }
    }

    private void OnParticleCollision(GameObject other)
    {
        if (logWaterHits)
        {
            Debug.Log($"[FlowerPot] OnParticleCollision from {other.name}.", this);
        }

        TryHandleWaterParticle(other);
    }

    public void HandleWaterTrigger(WaterSpillTrigger waterTrigger, Collider hitCollider)
    {
        if (!logWaterHits)
        {
            return;
        }

        Debug.Log(
            $"[FlowerPot] Ignored deprecated trigger hit from {(waterTrigger != null ? waterTrigger.name : "null")} via {(hitCollider != null ? hitCollider.name : "null")}. Real watering now uses particle collisions.",
            this);
    }

    private void TryHandleWaterParticle(GameObject other)
    {
        if (other == null)
        {
            return;
        }

        ParticleSystem waterParticles = other.GetComponent<ParticleSystem>();
        WaterSpill waterSpill = other.GetComponentInParent<WaterSpill>();
        if (waterParticles == null || waterSpill == null)
        {
            if (logWaterHits)
            {
                Debug.Log($"[FlowerPot] Ignored particle collision from {other.name} because it is not an active water particle system.", this);
            }

            return;
        }

        if (Time.time < nextAllowedHitTime)
        {
            return;
        }

        nextAllowedHitTime = Time.time + particleHitCooldown;

        if (taskDriver == null && simObjectiveInteraction == null)
        {
            if (logWaterHits)
            {
                Debug.LogWarning($"FlowerPot received particle hit from {other.name}, but no water task driver or objective interaction is assigned.", this);
            }

            return;
        }

        string targetStepId = simObjectiveInteraction != null ? simObjectiveInteraction.ObjectiveId : string.Empty;
        bool updated = taskDriver != null
            ? taskDriver.WaterPlant(targetStepId)
            : simObjectiveInteraction.AddObjectiveProgress(1f);
        if (logWaterHits)
        {
            Debug.Log(
                $"[FlowerPot] Water particle hit from {other.name}. Water task update {(updated ? "succeeded" : "was rejected")}.",
                this);
        }
    }

    private void CacheColliderSizeStatesIfNeeded()
    {
        if (colliderSizeStates.Count > 0)
        {
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null)
            {
                continue;
            }

            ColliderSizeState state = ColliderSizeState.TryCreate(targetCollider);
            if (state.IsValid)
            {
                colliderSizeStates.Add(state);
            }
        }
    }

    private readonly struct ColliderSizeState
    {
        private readonly BoxCollider boxCollider;
        private readonly SphereCollider sphereCollider;
        private readonly CapsuleCollider capsuleCollider;
        private readonly Vector3 boxSize;
        private readonly float sphereRadius;
        private readonly float capsuleRadius;
        private readonly float capsuleHeight;

        private ColliderSizeState(
            BoxCollider boxCollider,
            SphereCollider sphereCollider,
            CapsuleCollider capsuleCollider,
            Vector3 boxSize,
            float sphereRadius,
            float capsuleRadius,
            float capsuleHeight)
        {
            this.boxCollider = boxCollider;
            this.sphereCollider = sphereCollider;
            this.capsuleCollider = capsuleCollider;
            this.boxSize = boxSize;
            this.sphereRadius = sphereRadius;
            this.capsuleRadius = capsuleRadius;
            this.capsuleHeight = capsuleHeight;
        }

        public bool IsValid => boxCollider != null || sphereCollider != null || capsuleCollider != null;

        public static ColliderSizeState TryCreate(Collider targetCollider)
        {
            BoxCollider box = targetCollider as BoxCollider;
            if (box != null)
            {
                return new ColliderSizeState(box, null, null, box.size, 0f, 0f, 0f);
            }

            SphereCollider sphere = targetCollider as SphereCollider;
            if (sphere != null)
            {
                return new ColliderSizeState(null, sphere, null, Vector3.zero, sphere.radius, 0f, 0f);
            }

            CapsuleCollider capsule = targetCollider as CapsuleCollider;
            if (capsule != null)
            {
                return new ColliderSizeState(null, null, capsule, Vector3.zero, 0f, capsule.radius, capsule.height);
            }

            return default;
        }

        public void Apply(float scale)
        {
            if (boxCollider != null)
            {
                boxCollider.size = boxSize * scale;
            }

            if (sphereCollider != null)
            {
                sphereCollider.radius = sphereRadius * scale;
            }

            if (capsuleCollider != null)
            {
                capsuleCollider.radius = capsuleRadius * scale;
                capsuleCollider.height = capsuleHeight * scale;
            }
        }
    }
}
