using UnityEngine;

public enum SeedThrowPoseSourceKind
{
    Unknown,
    HandInteractor
}

public readonly struct SeedThrowRelease
{
    public SeedThrowRelease(
        Pose releasePose,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        SeedThrowPoseSourceKind sourceKind,
        bool isMeasured,
        int sampleCount)
    {
        ReleasePose = releasePose;
        LinearVelocity = linearVelocity;
        AngularVelocity = angularVelocity;
        SourceKind = sourceKind;
        IsMeasured = isMeasured;
        SampleCount = sampleCount;
    }

    public Pose ReleasePose { get; }
    public Vector3 LinearVelocity { get; }
    public Vector3 AngularVelocity { get; }
    public SeedThrowPoseSourceKind SourceKind { get; }
    public bool IsMeasured { get; }
    public int SampleCount { get; }
}

public readonly struct SeedThrowLaunch
{
    public SeedThrowLaunch(Vector3 velocity, bool usedAssistance)
    {
        Velocity = velocity;
        UsedAssistance = usedAssistance;
    }

    public Vector3 Velocity { get; }
    public bool UsedAssistance { get; }
}

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
    [SerializeField][Min(0f)] private float minimumMeasuredReleaseSpeed = 0.25f;
    [SerializeField][Min(0f)] private float releaseVelocityMultiplier = 1f;
    [Tooltip("Minimum visible launch speed for a low-speed but reliable hand release.")]
    [SerializeField][Min(0f)] private float weakReleaseAssistedLaunchSpeed = 1.25f;
    [Tooltip("Minimum launch speed when hand tracking cannot provide a reliable release velocity.")]
    [SerializeField][Min(0f)] private float invalidTrackingAssistedLaunchSpeed = 1.8f;
    [Tooltip("Prevents an assisted fallback from immediately launching into the floor.")]
    [SerializeField][Range(-1f, 0f)] private float minimumAssistedDirectionY = -0.15f;
    [SerializeField][Min(0.01f)] private float maximumLaunchSpeed = 4.5f;
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

    public void ApplyDifficulty(int newSpawnCount, float newConeHalfAngleDeg)
    {
        spawnCount = Mathf.Max(1, newSpawnCount);
        coneHalfAngleDeg = Mathf.Clamp(newConeHalfAngleDeg, 0f, 89f);
    }

    public int SpawnBurst(Transform hand, SeedThrowRelease release, out SeedThrowLaunch launch)
    {
        if (hand == null)
        {
            launch = new SeedThrowLaunch(Vector3.zero, false);
            return 0;
        }

        Pose releasePose = release.ReleasePose;
        if (!IsFinite(releasePose.position) || !IsFinite(releasePose.rotation))
        {
            releasePose = new Pose(hand.position, hand.rotation);
        }

        Vector3 fallbackDirection = releasePose.rotation * Vector3.forward;
        Vector3 baseDirection = ResolveThrowDirection(fallbackDirection, release.LinearVelocity);
        Vector3 spawnOrigin = releasePose.position +
                              releasePose.rotation * releasePointLocalOffset +
                              baseDirection * spawnClearanceForward;
        float releaseSpeed = release.LinearVelocity.magnitude;
        Vector3 baseLaunchVelocity = ResolveLaunchVelocity(release, baseDirection, out bool usedAssistance);
        launch = new SeedThrowLaunch(baseLaunchVelocity, usedAssistance);

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
                release.AngularVelocity + Random.insideUnitSphere * scatterSpeed * 8f,
                maximumAngularSpeed);
            spawned++;
        }

        if (logDebug)
        {
            Debug.Log(
                $"[SeedThrowSpawner] Spawn complete. Requested={count}, Spawned={spawned}, " +
                $"ReleaseSpeed={releaseSpeed:0.###}, LaunchSpeed={baseLaunchVelocity.magnitude:0.###}, " +
                $"Source={release.SourceKind}, Assisted={usedAssistance}.",
                this);
        }

        if (spawned > 0)
        {
            SeedSowingVfx.EmitReleaseFlecks(spawnOrigin, baseDirection, Mathf.Clamp(spawned, 3, 8));
        }

        return spawned;
    }

    private Vector3 ResolveThrowDirection(Vector3 fallbackDirection, Vector3 releaseVelocity)
    {
        Vector3 handForward = fallbackDirection.sqrMagnitude > 0.0001f
            ? fallbackDirection.normalized
            : transform.forward;
        bool hasMovementDirection = releaseVelocity.sqrMagnitude >= minimumMeasuredReleaseSpeed * minimumMeasuredReleaseSpeed;
        if (hasMovementDirection)
        {
            return releaseVelocity.normalized;
        }

        return handForward;
    }

    private Vector3 ResolveLaunchVelocity(
        SeedThrowRelease release,
        Vector3 baseDirection,
        out bool usedAssistance)
    {
        float measuredSpeed = release.LinearVelocity.magnitude;
        if (release.IsMeasured && measuredSpeed >= minimumMeasuredReleaseSpeed)
        {
            usedAssistance = false;
            return Vector3.ClampMagnitude(release.LinearVelocity * releaseVelocityMultiplier, maximumLaunchSpeed);
        }

        usedAssistance = true;
        Vector3 direction = measuredSpeed > 0.02f
            ? release.LinearVelocity.normalized
            : ClampAssistedDirection(baseDirection);
        float assistedSpeed = release.IsMeasured
            ? weakReleaseAssistedLaunchSpeed
            : invalidTrackingAssistedLaunchSpeed;
        float finalSpeed = Mathf.Max(measuredSpeed * releaseVelocityMultiplier, assistedSpeed);
        return direction * Mathf.Min(finalSpeed, maximumLaunchSpeed);
    }

    private Vector3 ClampAssistedDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        if (direction.y < minimumAssistedDirectionY)
        {
            direction.y = minimumAssistedDirectionY;
            direction.Normalize();
        }

        return direction;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
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
