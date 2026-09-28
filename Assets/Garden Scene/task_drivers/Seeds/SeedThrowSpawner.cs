using UnityEngine;

[DisallowMultipleComponent]
public class SeedThrowSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject seedCubePrefab;
    [SerializeField] private int spawnCount = 10;
    [Tooltip("World-space length of a seed. 12 mm is visible in VR without looking like a thrown pebble.")]
    [SerializeField][Min(0.002f)] private float seedVisualLengthMeters = 0.012f;
    [SerializeField] private Vector3 seedVisualAspect = new Vector3(0.65f, 0.45f, 1f);
    [SerializeField] private bool overridePrefabVisualScale = true;
    [SerializeField] private Color seedColor = new Color(0.33f, 0.16f, 0.055f, 1f);
    [SerializeField] private Vector3 releasePointLocalOffset = new Vector3(0f, 0f, 0.08f);
    [SerializeField][Min(0f)] private float spawnClearanceForward = 0.025f;

    [Header("Physical Launch")]
    [SerializeField][Min(0f)] private float minimumMeasuredReleaseSpeed = 0.15f;
    [SerializeField][Min(0f)] private float releaseVelocityMultiplier = 1f;
    [SerializeField][Min(0f)] private float minimumFallbackLaunchSpeed = 0.35f;
    [SerializeField][Min(0.01f)] private float maximumLaunchSpeed = 6f;
    [SerializeField][Range(0f, 45f)] private float coneHalfAngleDeg = 4f;
    [SerializeField][Min(0f)] private float scatterSpeedMin = 0.02f;
    [SerializeField][Min(0f)] private float scatterSpeedMax = 0.16f;
    [SerializeField][Min(0f)] private float maximumAngularSpeed = 35f;

    [Header("Seed Physics")]
    [Tooltip("Optional shared asset. When assigned, it overrides the fallback Rigidbody and air-drag values below.")]
    [SerializeField] private SeedPhysicsSettings physicsSettings;
    [SerializeField][Min(0.0001f)] private float fallbackMassKg = 0.003f;
    [SerializeField][Min(0f)] private float fallbackLinearDamping = 0.12f;
    [SerializeField][Min(0f)] private float fallbackAngularDamping = 1.75f;
    [SerializeField] private bool fallbackEnableAerodynamicDrag = true;
    [SerializeField][Min(0f)] private float fallbackDragCoefficient = 0.8f;
    [SerializeField][Min(0.000001f)] private float fallbackReferenceAreaSquareMeters = 0.0015f;
    [SerializeField] private float spawnLifetimeSeconds = 8f;
    [SerializeField] private bool destroySeedsAfterLifetime = true;

    [Header("Seed VFX")]
    [SerializeField] private bool autoAttachSeedFlightVfx = true;
    [SerializeField] private bool logDebug;

    private int _nextThrowId = 1;

    public void ApplyDifficulty(
        int newSpawnCount,
        float newForceMin,
        float newForceMax,
        float newConeHalfAngleDeg)
    {
        spawnCount = Mathf.Max(1, newSpawnCount);
        coneHalfAngleDeg = Mathf.Clamp(newConeHalfAngleDeg, 0f, 89f);

        // These two arguments are deliberately retained for compatibility with the
        // existing difficulty profile. Launch speed now comes from measured hand
        // motion, so difficulty must not silently replace it with an impulse range.
        _ = newForceMin;
        _ = newForceMax;
    }

    public int SpawnBurst(Transform hand, Vector3 releaseVelocity)
    {
        return SpawnBurst(hand, releaseVelocity, Vector3.zero);
    }

    public int SpawnBurst(Transform hand, Vector3 releaseVelocity, Vector3 releaseAngularVelocity)
    {
        if (hand == null)
        {
            return 0;
        }

        Vector3 baseDirection = ResolveThrowDirection(hand, releaseVelocity);
        Vector3 spawnOrigin = hand.TransformPoint(releasePointLocalOffset) + baseDirection * spawnClearanceForward;
        float releaseSpeed = releaseVelocity.magnitude;
        Vector3 baseLaunchVelocity = ResolveLaunchVelocity(baseDirection, releaseVelocity);

        int count = Mathf.Max(1, spawnCount);
        int spawned = 0;
        int throwId = _nextThrowId++;

        for (int i = 0; i < count; i++)
        {
            Quaternion spreadRotation = Random.rotationUniform;
            Vector3 spreadDir = Vector3.RotateTowards(
                baseDirection,
                spreadRotation * baseDirection,
                coneHalfAngleDeg * Mathf.Deg2Rad * Random.value,
                0f).normalized;

            GameObject seed = CreateSeedInstance(spawnOrigin);
            if (seed == null)
            {
                continue;
            }

            Rigidbody rb = seed.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = seed.AddComponent<Rigidbody>();
            }

            ConfigureSeedPhysics(seed, rb);

            SeedProjectileMarker marker = seed.GetComponent<SeedProjectileMarker>();
            if (marker == null)
            {
                marker = seed.AddComponent<SeedProjectileMarker>();
            }
            marker.SetThrowId(throwId);

            if (autoAttachSeedFlightVfx)
            {
                SeedFlightVfx vfx = seed.GetComponent<SeedFlightVfx>();
                if (vfx == null)
                {
                    vfx = seed.AddComponent<SeedFlightVfx>();
                }
                vfx.Initialize(spawnLifetimeSeconds);
            }
            else if (destroySeedsAfterLifetime && spawnLifetimeSeconds > 0f)
            {
                Destroy(seed, spawnLifetimeSeconds);
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            float scatterSpeed = Random.Range(
                Mathf.Min(scatterSpeedMin, scatterSpeedMax),
                Mathf.Max(scatterSpeedMin, scatterSpeedMax));
            Vector3 scatterVelocity = (spreadDir - baseDirection) * baseLaunchVelocity.magnitude + spreadDir * scatterSpeed;
            rb.linearVelocity = Vector3.ClampMagnitude(baseLaunchVelocity + scatterVelocity, maximumLaunchSpeed);
            rb.angularVelocity = Vector3.ClampMagnitude(
                releaseAngularVelocity + Random.insideUnitSphere * scatterSpeed * 8f,
                maximumAngularSpeed);
            spawned++;
        }

        if (logDebug)
        {
            Debug.Log(
                $"[SeedThrowSpawner] Spawn complete. Requested={count}, Spawned={spawned}, " +
                $"ReleaseSpeed={releaseSpeed:0.###}, LaunchSpeed={baseLaunchVelocity.magnitude:0.###}.",
                this);
        }

        if (spawned > 0)
        {
            SeedSowingVfx.EmitReleaseFlecks(spawnOrigin, baseDirection, Mathf.Clamp(spawned, 3, 8));
        }

        return spawned;
    }

    private Vector3 ResolveThrowDirection(Transform hand, Vector3 releaseVelocity)
    {
        Vector3 handForward = hand.forward.sqrMagnitude > 0.0001f ? hand.forward.normalized : transform.forward;
        bool hasMovementDirection = releaseVelocity.sqrMagnitude >= minimumMeasuredReleaseSpeed * minimumMeasuredReleaseSpeed;
        if (hasMovementDirection)
        {
            return releaseVelocity.normalized;
        }

        return handForward;
    }

    private Vector3 ResolveLaunchVelocity(Vector3 baseDirection, Vector3 releaseVelocity)
    {
        float measuredSpeed = releaseVelocity.magnitude;
        if (measuredSpeed >= minimumMeasuredReleaseSpeed)
        {
            return Vector3.ClampMagnitude(releaseVelocity * releaseVelocityMultiplier, maximumLaunchSpeed);
        }

        // A small configurable assist lets users complete a release gesture without a
        // stationary hand producing a seed cloud at the origin.
        float fallbackSpeed = Mathf.Min(minimumFallbackLaunchSpeed, maximumLaunchSpeed);
        return baseDirection * fallbackSpeed;
    }

    private void ConfigureSeedPhysics(GameObject seed, Rigidbody rb)
    {
        if (physicsSettings != null)
        {
            physicsSettings.ApplyTo(rb);
        }
        else
        {
            rb.mass = fallbackMassKg;
            rb.useGravity = true;
            rb.linearDamping = fallbackLinearDamping;
            rb.angularDamping = fallbackAngularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        SeedAerodynamics aerodynamics = seed.GetComponent<SeedAerodynamics>();
        bool enableAerodynamics = physicsSettings != null
            ? physicsSettings.EnableAerodynamicDrag
            : fallbackEnableAerodynamicDrag;
        if (!enableAerodynamics && aerodynamics == null)
        {
            return;
        }

        if (aerodynamics == null)
        {
            aerodynamics = seed.AddComponent<SeedAerodynamics>();
        }

        aerodynamics.Configure(
            enableAerodynamics,
            physicsSettings != null ? physicsSettings.DragCoefficient : fallbackDragCoefficient,
            physicsSettings != null ? physicsSettings.ReferenceAreaSquareMeters : fallbackReferenceAreaSquareMeters);
    }

    private GameObject CreateSeedInstance(Vector3 position)
    {
        if (seedCubePrefab != null)
        {
            GameObject seed = Instantiate(seedCubePrefab, position, Random.rotation);
            ApplyVisualScale(seed);
            return seed;
        }

        GameObject seedFallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        seedFallback.transform.position = position;
        seedFallback.transform.rotation = Random.rotation;
        ApplyVisualScale(seedFallback);
        ApplyFallbackSeedMaterial(seedFallback);
        return seedFallback;
    }

    private void ApplyVisualScale(GameObject seed)
    {
        if (seed == null || !overridePrefabVisualScale)
        {
            return;
        }

        Vector3 clampedAspect = new Vector3(
            Mathf.Max(0.05f, seedVisualAspect.x),
            Mathf.Max(0.05f, seedVisualAspect.y),
            Mathf.Max(0.05f, seedVisualAspect.z));
        seed.transform.localScale = clampedAspect * seedVisualLengthMeters;
    }

    private void ApplyFallbackSeedMaterial(GameObject seed)
    {
        if (seed == null || !seed.TryGetComponent<Renderer>(out Renderer renderer))
        {
            return;
        }

        Material material = SeedSowingVfx.GetSeedMaterial(seedColor);
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
