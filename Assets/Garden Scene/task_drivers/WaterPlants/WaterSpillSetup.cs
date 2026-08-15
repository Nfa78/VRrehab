using UnityEngine;

public class WaterSpillSetup : MonoBehaviour
{
    [SerializeField] private Transform waterExitPoint;
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private WaterSpillTrigger waterTrigger;
    [SerializeField] private bool autoCreateParticles = true;
    [SerializeField] private bool boostParticleVisibility = true;
    [SerializeField] private Vector3 localEmissionDirection = Vector3.forward;
    [SerializeField] private float spawnForwardOffset = 0.005f;
    [SerializeField] private float triggerForwardOffset = 0.35f;
    [SerializeField] private float triggerLength = 1f;
    [SerializeField] private float triggerRadius = 0.2f;
    [SerializeField] private bool logSetup = true;

    public ParticleSystem WaterParticles => waterParticles;
    public WaterSpillTrigger WaterTrigger => waterTrigger;
    public Transform ActiveExitPoint => waterExitPoint != null ? waterExitPoint : transform;

    public void EnsureSetup()
    {
        EnsureParticleSystem();
        CleanupLegacyTriggerVolume();
        ConfigureParticleSystem();

        if (logSetup)
        {
            Debug.Log(
                $"[WaterSpillSetup] EnsureSetup on {name}. ExitPoint={ActiveExitPoint.name}, Particles={(waterParticles != null ? waterParticles.name : "null")}.",
                this);
        }
    }

    public void AlignParticlesToExitPoint()
    {
        if (waterParticles == null)
        {
            return;
        }

        Transform exitPoint = ActiveExitPoint;
        Vector3 emissionDirection = GetWorldEmissionDirection();
        Vector3 particlePosition = exitPoint.position + emissionDirection * spawnForwardOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(emissionDirection, exitPoint.up);
        waterParticles.transform.SetPositionAndRotation(particlePosition, spawnRotation);
    }

    private Vector3 GetWorldEmissionDirection()
    {
        Transform exitPoint = ActiveExitPoint;
        Vector3 localDirection = localEmissionDirection.sqrMagnitude > 0.0001f
            ? localEmissionDirection.normalized
            : Vector3.forward;
        return exitPoint.TransformDirection(localDirection).normalized;
    }

    private void EnsureParticleSystem()
    {
        if (waterParticles != null || !autoCreateParticles)
        {
            if (logSetup && waterParticles != null)
            {
                Debug.Log($"[WaterSpillSetup] Reusing assigned particle system {waterParticles.name} on {name}.", this);
            }

            return;
        }

        Transform parent = ActiveExitPoint;
        Transform existing = parent.Find("AutoWaterParticles");
        if (existing != null)
        {
            waterParticles = existing.GetComponent<ParticleSystem>();
            if (waterParticles != null)
            {
                if (logSetup)
                {
                    Debug.Log($"[WaterSpillSetup] Reusing existing AutoWaterParticles under {parent.name}.", this);
                }

                return;
            }
        }

        GameObject particlesObject = new GameObject("AutoWaterParticles");
        particlesObject.transform.SetParent(parent, false);
        particlesObject.transform.localPosition = Vector3.zero;
        particlesObject.transform.localRotation = Quaternion.identity;

        waterParticles = particlesObject.AddComponent<ParticleSystem>();

        if (logSetup)
        {
            Debug.Log($"[WaterSpillSetup] Created AutoWaterParticles under {parent.name}.", this);
        }
    }

    private void ConfigureParticleSystem()
    {
        if (waterParticles == null)
        {
            return;
        }

        var collision = waterParticles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.collidesWith = Physics.AllLayers;
        collision.enableDynamicColliders = true;
        collision.sendCollisionMessages = true;
        collision.radiusScale = 0.85f;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 0f;

        if (!boostParticleVisibility)
        {
            return;
        }

        var main = waterParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.45f;
        main.startSpeed = 3.2f;
        main.startSize = 0.028f;
        main.startColor = new Color(0.45f, 0.75f, 1f, 0.9f);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = waterParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 95f;

        var shape = waterParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3f;
        shape.radius = 0.001f;
        shape.length = 0f;

        var noise = waterParticles.noise;
        noise.enabled = true;
        noise.strength = 0.02f;
        noise.frequency = 0.2f;

        var velocityOverLifetime = waterParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        var colorOverLifetime = waterParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.75f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = colorGradient;

        var sizeOverLifetime = waterParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.8f),
            new Keyframe(0.5f, 0.95f),
            new Keyframe(1f, 0.2f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = waterParticles.GetComponent<ParticleSystemRenderer>();
        renderer.enabled = true;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        Shader particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        }

        if (particleShader != null)
        {
            Material particleMaterial = new Material(particleShader);
            particleMaterial.color = new Color(0.6f, 0.85f, 1f, 0.9f);
            renderer.material = particleMaterial;
        }
    }

    private void EnsureTriggerVolume()
    {
        if (waterParticles == null)
        {
            return;
        }

        CleanupLegacyTriggerComponents();

        Transform triggerParent = ActiveExitPoint;
        Transform existingTrigger = triggerParent.Find("AutoWaterTrigger");
        if (existingTrigger != null)
        {
            waterTrigger = existingTrigger.GetComponent<WaterSpillTrigger>();
        }

        GameObject triggerObject;
        if (waterTrigger == null)
        {
            triggerObject = existingTrigger != null ? existingTrigger.gameObject : new GameObject("AutoWaterTrigger");
            triggerObject.transform.SetParent(triggerParent, false);
            triggerObject.transform.localPosition = Vector3.zero;
            triggerObject.transform.localRotation = Quaternion.identity;
            waterTrigger = triggerObject.GetComponent<WaterSpillTrigger>();
            if (waterTrigger == null)
            {
                waterTrigger = triggerObject.AddComponent<WaterSpillTrigger>();
            }
        }
        else
        {
            triggerObject = waterTrigger.gameObject;
        }

        triggerObject.transform.SetParent(triggerParent, false);

        CapsuleCollider triggerCollider = triggerObject.GetComponent<CapsuleCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = triggerObject.AddComponent<CapsuleCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
        triggerCollider.direction = 2;
        triggerCollider.radius = triggerRadius;
        triggerCollider.height = Mathf.Max(triggerLength, triggerRadius * 2f);
        triggerCollider.center = new Vector3(0f, 0f, triggerCollider.height * 0.5f);
        IgnoreSelfColliders(triggerCollider);

        Rigidbody triggerBody = triggerObject.GetComponent<Rigidbody>();
        if (triggerBody == null)
        {
            triggerBody = triggerObject.AddComponent<Rigidbody>();
        }

        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        triggerBody.detectCollisions = true;

        if (logSetup)
        {
            Debug.Log(
                $"[WaterSpillSetup] Trigger ready on {triggerObject.name}. isTrigger={triggerCollider.isTrigger}, enabled={triggerCollider.enabled}, radius={triggerCollider.radius}, height={triggerCollider.height}, triggerForwardOffset={triggerForwardOffset}, rigidbody={triggerBody.name}.",
                triggerObject);
        }
    }

    private void CleanupLegacyTriggerVolume()
    {
        if (waterTrigger != null)
        {
            GameObject triggerObject = waterTrigger.gameObject;
            waterTrigger = null;
            if (triggerObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(triggerObject);
                }
                else
                {
                    DestroyImmediate(triggerObject);
                }
            }
        }

        Transform existingTrigger = ActiveExitPoint.Find("AutoWaterTrigger");
        if (existingTrigger == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existingTrigger.gameObject);
            return;
        }

        DestroyImmediate(existingTrigger.gameObject);
    }

    private void CleanupLegacyTriggerComponents()
    {
        if (waterParticles == null)
        {
            return;
        }

        GameObject legacyObject = waterParticles.gameObject;
        if (legacyObject == null)
        {
            return;
        }

        RemoveComponentIfPresent<WaterSpillTrigger>(legacyObject);
        RemoveComponentIfPresent<CapsuleCollider>(legacyObject);

        Rigidbody body = legacyObject.GetComponent<Rigidbody>();
        if (body != null)
        {
            RemoveComponentIfPresent<Rigidbody>(legacyObject);
        }
    }

    private static void RemoveComponentIfPresent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
            return;
        }

        DestroyImmediate(component);
    }

    private void IgnoreSelfColliders(Collider triggerCollider)
    {
        if (triggerCollider == null)
        {
            return;
        }

        Collider[] selfColliders = GetComponentsInParent<Collider>(true);
        for (int i = 0; i < selfColliders.Length; i++)
        {
            Collider selfCollider = selfColliders[i];
            if (selfCollider == null || selfCollider == triggerCollider)
            {
                continue;
            }

            Physics.IgnoreCollision(triggerCollider, selfCollider, true);
        }

        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider childCollider = childColliders[i];
            if (childCollider == null || childCollider == triggerCollider)
            {
                continue;
            }

            Physics.IgnoreCollision(triggerCollider, childCollider, true);
        }
    }
}
