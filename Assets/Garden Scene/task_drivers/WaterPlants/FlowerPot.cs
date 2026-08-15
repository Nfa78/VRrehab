using TaskSystem;
using UnityEngine;

public class FlowerPot : MonoBehaviour
{
    [SerializeField] private SimObjectiveInteraction simObjectiveInteraction;
    [SerializeField] private WaterPlantsTaskDriver taskDriver;
    [SerializeField] private bool logWaterHits = true;
    [SerializeField] private float particleHitCooldown = 0.25f;

    private float nextAllowedHitTime;

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
}
