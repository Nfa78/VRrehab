using UnityEngine;

[DisallowMultipleComponent]
public class SeedThrowSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject seedCubePrefab;
    [SerializeField] private int spawnCount = 10;
    [SerializeField] private float seedCubeScale = 0.05f;
    [SerializeField] private float spawnOffsetForward = 0.08f;
    [SerializeField] private float forceMin = 1.2f;
    [SerializeField] private float forceMax = 2.2f;
    [SerializeField] private float coneHalfAngleDeg = 6f;
    [SerializeField] private bool useHandForwardDirection = true;
    [SerializeField][Range(0f, 1f)] private float handVelocityDirectionWeight;
    [SerializeField][Min(0f)] private float minimumReleaseSpeedForVelocityDirection = 0.15f;
    [SerializeField] private bool scaleForceByReleaseSpeed = true;
    [SerializeField][Min(0f)] private float releaseSpeedForMinForce = 0.2f;
    [SerializeField][Min(0.01f)] private float releaseSpeedForMaxForce = 1.6f;
    [SerializeField][Min(0f)] private float releaseSpeedForceMultiplierMin = 0.9f;
    [SerializeField][Min(0f)] private float releaseSpeedForceMultiplierMax = 1.45f;
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
        forceMin = Mathf.Max(0f, Mathf.Min(newForceMin, newForceMax));
        forceMax = Mathf.Max(forceMin, Mathf.Max(newForceMin, newForceMax));
        coneHalfAngleDeg = Mathf.Clamp(newConeHalfAngleDeg, 0f, 89f);
    }

    public int SpawnBurst(Transform hand, Vector3 releaseVelocity)
    {
        if (hand == null)
        {
            return 0;
        }

        Vector3 baseDirection = ResolveThrowDirection(hand, releaseVelocity);
        Vector3 spawnOrigin = hand.position + baseDirection * spawnOffsetForward;
        float releaseSpeed = releaseVelocity.magnitude;
        float forceScale = ResolveForceScale(releaseSpeed);

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
            float throwForce = Random.Range(forceMin, forceMax) * forceScale;
            rb.AddForce(spreadDir * throwForce, ForceMode.Impulse);
            spawned++;
        }

        if (logDebug)
        {
            Debug.Log(
                $"[SeedThrowSpawner] Spawn complete. Requested={count}, Spawned={spawned}, ReleaseSpeed={releaseSpeed:0.###}, ForceScale={forceScale:0.###}.",
                this);
        }

        return spawned;
    }

    private Vector3 ResolveThrowDirection(Transform hand, Vector3 releaseVelocity)
    {
        Vector3 handForward = hand.forward.sqrMagnitude > 0.0001f ? hand.forward.normalized : transform.forward;
        bool hasMovementDirection = releaseVelocity.sqrMagnitude >= minimumReleaseSpeedForVelocityDirection * minimumReleaseSpeedForVelocityDirection;

        if (!useHandForwardDirection && hasMovementDirection)
        {
            return releaseVelocity.normalized;
        }

        if (useHandForwardDirection && handVelocityDirectionWeight > 0f && hasMovementDirection)
        {
            return Vector3.Slerp(handForward, releaseVelocity.normalized, handVelocityDirectionWeight).normalized;
        }

        return handForward;
    }

    private float ResolveForceScale(float releaseSpeed)
    {
        if (!scaleForceByReleaseSpeed)
        {
            return 1f;
        }

        if (releaseSpeedForMaxForce <= releaseSpeedForMinForce)
        {
            return releaseSpeedForceMultiplierMax;
        }

        float speedT = Mathf.InverseLerp(releaseSpeedForMinForce, releaseSpeedForMaxForce, releaseSpeed);
        return Mathf.Lerp(releaseSpeedForceMultiplierMin, releaseSpeedForceMultiplierMax, speedT);
    }

    private GameObject CreateSeedInstance(Vector3 position)
    {
        if (seedCubePrefab != null)
        {
            return Instantiate(seedCubePrefab, position, Random.rotation);
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = position;
        cube.transform.rotation = Random.rotation;
        cube.transform.localScale = Vector3.one * seedCubeScale;
        return cube;
    }
}
