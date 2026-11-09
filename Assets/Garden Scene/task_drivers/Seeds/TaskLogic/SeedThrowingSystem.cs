using System.Collections;
using SoundSystem;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class SeedThrowingSystem : MonoBehaviour
{
    [System.Serializable]
    private struct SceneReferences
    {
        public SeedInteraction seedInteraction;
        public GameObject seedPrefab;
        public Transform impactCircle;
        public LayerMask groundMask;
    }

    [System.Serializable]
    private struct SeedSettings
    {
        public int seedCount;
        public float landingRadius;
        public float seedLifetime;
        public float seedSizeMeters;
        public Vector3 seedAspect;
    }

    [System.Serializable]
    private struct PlantingAreaSettings
    {
        public float areaMinX;
        public float areaMaxX;
        public float areaMinZ;
        public float areaMaxZ;
        public SeedSoilRegion[] regions;
    }

    [System.Serializable]
    private struct PreviewSettings
    {
        public Color trajectoryColor;
        public float trajectoryWidth;
        public float previewSpeed;
        public float previewTime;
        public int points;
        public float impactCirclePulseDuration;
    }

    [System.Serializable]
    private struct HeightThrowSettings
    {
        public float handHeightForMinDistance;
        public float handHeightForMaxDistance;
        public float minThrowDistance;
        public float maxThrowDistance;
        public float heightResponse;
    }

    [System.Serializable]
    private struct DebugSettings
    {
        public bool logSeedFlow;
    }

    [SerializeField] private SceneReferences references;
    [SerializeField] private SeedSettings seeds = new SeedSettings
    {
        seedCount = 10,
        landingRadius = 0.25f,
        seedLifetime = 8f,
        seedSizeMeters = 0.012f,
        seedAspect = new Vector3(0.65f, 0.45f, 1f)
    };
    [SerializeField] private PlantingAreaSettings plantingArea = new PlantingAreaSettings
    {
        areaMinX = -0.67f,
        areaMaxX = 0.35f,
        areaMinZ = 1.7f,
        areaMaxZ = 2.7f,
        regions = new SeedSoilRegion[4]
    };
    [SerializeField] private PreviewSettings preview = new PreviewSettings
    {
        trajectoryColor = new Color(0.15f, 0.85f, 1f, 0.9f),
        trajectoryWidth = 0.008f,
        previewSpeed = 2f,
        previewTime = 2f,
        points = 24,
        impactCirclePulseDuration = 3f
    };
    [SerializeField] private HeightThrowSettings heightThrow = new HeightThrowSettings
    {
        handHeightForMinDistance = 0.8f,
        handHeightForMaxDistance = 1.4f,
        minThrowDistance = 1f,
        maxThrowDistance = 3f,
        heightResponse = 0.5f
    };
    [SerializeField] private DebugSettings debug;

    // Legacy serialized fields. These allow existing scenes to migrate from the
    // previous flat Inspector layout into the grouped settings above.
    [SerializeField, HideInInspector] private SeedInteraction seedInteraction;
    [SerializeField, HideInInspector] private GameObject seedPrefab;
    [SerializeField, HideInInspector] private int seedCount;
    [SerializeField, HideInInspector] private float landingRadius;
    [SerializeField, HideInInspector] private float areaMinX;
    [SerializeField, HideInInspector] private float areaMaxX;
    [SerializeField, HideInInspector] private float areaMinZ;
    [SerializeField, HideInInspector] private float areaMaxZ;
    [SerializeField, HideInInspector] private SeedSoilRegion[] regions;
    [SerializeField, HideInInspector] private float seedLifetime;
    [SerializeField, HideInInspector] private float seedSizeMeters;
    [SerializeField, HideInInspector] private Vector3 seedAspect;
    [SerializeField, HideInInspector] private Transform impactCircle;
    [SerializeField, HideInInspector] private LayerMask groundMask;
    [SerializeField, HideInInspector] private Color trajectoryColor;
    [SerializeField, HideInInspector] private float trajectoryWidth;
    [SerializeField, HideInInspector] private float previewSpeed;
    [SerializeField, HideInInspector] private float previewTime;
    [SerializeField, HideInInspector] private int points;
    [SerializeField, HideInInspector] private bool logSeedFlow;

    private LineRenderer line;
    private Hand releasedHand;
    private Vector3 previewStart;
    private Vector3 previewVelocity;
    private Vector3 previewImpactPoint;
    private float previewImpactTime;
    private bool previewValid;
    private Vector3 impactCircleDefaultScale;
    private Color impactCircleDefaultColor = Color.white;
    private Renderer impactCircleRenderer;
    private float impactCircleLocalRadius;
    private float impactCirclePulseTime;

    public void ApplyDifficulty(int newSeedCount, float newLandingRadius)
    {
        seeds.seedCount = Mathf.Max(1, newSeedCount);
        seeds.landingRadius = Mathf.Max(0.01f, newLandingRadius);
    }

    private void Awake()
    {
        MigrateLegacySettings();
        EnsureSettingsAreValid();
        line = GetComponent<LineRenderer>();
        if (references.seedInteraction == null) references.seedInteraction = FindFirstObjectByType<SeedInteraction>();
        ResolveRegions();
        line.positionCount = preview.points;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.startWidth = preview.trajectoryWidth;
        line.endWidth = preview.trajectoryWidth * 0.35f;
        line.startColor = preview.trajectoryColor;
        line.endColor = new Color(preview.trajectoryColor.r, preview.trajectoryColor.g, preview.trajectoryColor.b, 0.05f);
        line.numCapVertices = 4;
        line.enabled = false;
        if (references.impactCircle != null)
        {
            Vector3 circleScale = references.impactCircle.localScale;
            circleScale.x = seeds.landingRadius * 2f;
            circleScale.z = seeds.landingRadius * 2f;
            references.impactCircle.localScale = circleScale;
            impactCircleDefaultScale = circleScale;
            impactCircleRenderer = references.impactCircle.GetComponentInChildren<Renderer>();
            if (impactCircleRenderer != null)
            {
                impactCircleDefaultColor = impactCircleRenderer.material.color;
                Bounds localBounds = impactCircleRenderer.localBounds;
                impactCircleLocalRadius = Mathf.Min(
                    localBounds.extents.x,
                    localBounds.extents.z);
            }
            references.impactCircle.gameObject.SetActive(false);
        }
    }

    private void MigrateLegacySettings()
    {
        bool hasLegacySettings = seedPrefab != null || impactCircle != null ||
                                 seedCount > 0 || points > 0 || groundMask.value != 0;
        if (!hasLegacySettings)
            return;

        references.seedInteraction = seedInteraction;
        references.seedPrefab = seedPrefab;
        references.impactCircle = impactCircle;
        references.groundMask = groundMask;

        seeds.seedCount = seedCount;
        seeds.landingRadius = landingRadius;
        seeds.seedLifetime = seedLifetime;
        seeds.seedSizeMeters = seedSizeMeters;
        seeds.seedAspect = seedAspect;

        plantingArea.areaMinX = areaMinX;
        plantingArea.areaMaxX = areaMaxX;
        plantingArea.areaMinZ = areaMinZ;
        plantingArea.areaMaxZ = areaMaxZ;
        plantingArea.regions = regions;

        preview.trajectoryColor = trajectoryColor;
        preview.trajectoryWidth = trajectoryWidth;
        preview.previewSpeed = previewSpeed;
        preview.previewTime = previewTime;
        preview.points = points;
        debug.logSeedFlow = logSeedFlow;
    }

    private void EnsureSettingsAreValid()
    {
        seeds.seedCount = Mathf.Max(1, seeds.seedCount);
        seeds.landingRadius = Mathf.Max(0.01f, seeds.landingRadius);
        seeds.seedLifetime = Mathf.Max(0f, seeds.seedLifetime);
        seeds.seedSizeMeters = Mathf.Max(0.002f, seeds.seedSizeMeters);
        if (seeds.seedAspect == Vector3.zero)
            seeds.seedAspect = new Vector3(0.65f, 0.45f, 1f);

        if (plantingArea.areaMinX == 0f && plantingArea.areaMaxX == 0f)
        {
            plantingArea.areaMinX = -0.67f;
            plantingArea.areaMaxX = 0.35f;
        }

        if (plantingArea.areaMinZ == 0f && plantingArea.areaMaxZ == 0f)
        {
            plantingArea.areaMinZ = 1.7f;
            plantingArea.areaMaxZ = 2.7f;
        }

        preview.trajectoryWidth = Mathf.Max(0.001f, preview.trajectoryWidth);
        preview.previewSpeed = Mathf.Max(0.1f, preview.previewSpeed);
        preview.previewTime = Mathf.Max(0.1f, preview.previewTime);
        preview.points = Mathf.Max(2, preview.points);
        preview.impactCirclePulseDuration = Mathf.Max(0.1f, preview.impactCirclePulseDuration);

        if (preview.trajectoryColor == Color.clear)
            preview.trajectoryColor = new Color(0.15f, 0.85f, 1f, 0.9f);

        heightThrow.handHeightForMaxDistance = Mathf.Max(
            heightThrow.handHeightForMinDistance + 0.01f,
            heightThrow.handHeightForMaxDistance);
        heightThrow.minThrowDistance = Mathf.Max(0.01f, heightThrow.minThrowDistance);
        heightThrow.maxThrowDistance = Mathf.Max(
            heightThrow.minThrowDistance,
            heightThrow.maxThrowDistance);
        heightThrow.heightResponse = Mathf.Max(0.01f, heightThrow.heightResponse);
    }

    private void Update()
    {
        Hand hand = references.seedInteraction != null ? references.seedInteraction.GrabbingHand : null;
        if (hand != null)
        {
            releasedHand = hand;
            DrawPreview(hand);
        }
        else
        {
            HidePreview();
            if (releasedHand != null) Throw(releasedHand);
            releasedHand = null;
        }

        AnimateImpactCircle();
    }

    private void DrawPreview(Hand hand)
    {
        if (hand.TrackedAnchor == null) return;
        Vector3 start = hand.TrackedAnchor.position;
        float throwDistance = CalculateThrowDistance(start.y);
        float distanceScale = throwDistance / 2f;
        Vector3 velocity = hand.TrackedAnchor.forward * preview.previewSpeed * distanceScale;
        Vector3? impact = null;
        previewStart = start;
        previewVelocity = velocity;
        previewImpactTime = preview.previewTime;
        previewValid = false;
        line.enabled = true;
        bool finished = false;
        for (int i = 0; i < preview.points; i++)
        {
            float t = preview.previewTime * i / (preview.points - 1f);
            Vector3 current = start + velocity * t + 0.5f * Physics.gravity * t * t;
            if (!finished && i > 0 && Physics.Linecast(line.GetPosition(i - 1), current, out RaycastHit hit, references.groundMask))
            {
                current = hit.point;
                impact = hit.point;
                previewImpactPoint = hit.point;
                previewImpactTime = t;
                previewValid = true;
                finished = true;
            }
            else if (finished)
            {
                current = impact.Value;
            }
            line.SetPosition(i, current);
        }
        if (references.impactCircle != null && impact.HasValue)
        {
            references.impactCircle.position = impact.Value + Vector3.up * 0.002f;
            if (!references.impactCircle.gameObject.activeSelf)
                impactCirclePulseTime = 0f;
            references.impactCircle.gameObject.SetActive(true);
        }
    }

    private void Throw(Hand hand)
    {
        if (hand.TrackedAnchor == null) return;
        Transform anchor = hand.TrackedAnchor;
        Vector3 start = anchor.position;
        if (!previewValid)
        {
            EventSoundManager.TryPlayAt("garden.seed.missed", start);
            return;
        }
        Vector3 center = previewImpactPoint;
        EventSoundManager.TryPlayAt("garden.seed.thrown", start);

        float flightTime = previewImpactTime;
        for (int i = 0; i < Mathf.Max(1, seeds.seedCount); i++)
        {
            Vector2 randomOffset = references.impactCircle != null
                ? Random.insideUnitCircle * GetImpactCircleLocalRadius()
                : Random.insideUnitCircle * seeds.landingRadius;
            Vector3 target = references.impactCircle != null
                ? references.impactCircle.TransformPoint(
                    new Vector3(randomOffset.x, 0f, randomOffset.y))
                : center + Vector3.ProjectOnPlane(anchor.right, Vector3.up).normalized * randomOffset.x
                         + Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized * randomOffset.y;
            GameObject seed = references.seedPrefab != null
                ? Instantiate(references.seedPrefab, start, Random.rotation)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seed.transform.position = start;
            seed.transform.localScale = seeds.seedAspect * seeds.seedSizeMeters;
            if (seed.GetComponent<SeedProjectileMarker>() == null)
                seed.AddComponent<SeedProjectileMarker>();
            Rigidbody body = seed.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            if (debug.logSeedFlow) Debug.Log($"[SeedThrowingSystem] Spawned seed {i + 1}/{seeds.seedCount} at {start}.", seed);
            StartCoroutine(AnimateSeed(seed, center, target, flightTime, previewVelocity));
        }
    }

    private IEnumerator AnimateSeed(GameObject seed, Vector3 center, Vector3 target, float duration, Vector3 velocity)
    {
        Vector3 start = seed.transform.position;
        float elapsed = 0f;
        while (seed != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 basePoint = start + velocity * (t * duration) +
                                0.5f * Physics.gravity * (t * duration) * (t * duration);
            seed.transform.position = basePoint + (target - center) * t * t;
            if (seed.transform.position.y <= 0f)
            {
                seed.transform.position = new Vector3(seed.transform.position.x, 0f, seed.transform.position.z);
                LandSeed(seed);
                yield break;
            }
            yield return null;
        }

        if (seed == null) yield break;
        seed.transform.position = target;
        LandSeed(seed);
    }

    private void LandSeed(GameObject seed)
    {
        if (seeds.seedLifetime > 0f) Destroy(seed, seeds.seedLifetime);
    }

    private void ResolveRegions()
    {
        if (plantingArea.regions == null || plantingArea.regions.Length != 4)
            plantingArea.regions = new SeedSoilRegion[4];
        SeedSoilRegion[] found = FindObjectsByType<SeedSoilRegion>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Vector3 p = found[i].transform.position;
            int column = p.x < (plantingArea.areaMinX + plantingArea.areaMaxX) * 0.5f ? 0 : 1;
            int row = p.z < (plantingArea.areaMinZ + plantingArea.areaMaxZ) * 0.5f ? 0 : 1;
            plantingArea.regions[row * 2 + column] ??= found[i];
        }
    }

    private SeedSoilRegion FindRegionAt(Vector3 point)
    {
        if (point.x < plantingArea.areaMinX || point.x > plantingArea.areaMaxX ||
            point.z < plantingArea.areaMinZ || point.z > plantingArea.areaMaxZ)
        {
            Debug.LogWarning($"Seed landed outside planting area: {point}", this);
            return null;
        }

        if (plantingArea.regions == null || plantingArea.regions.Length < 4 ||
            plantingArea.regions[0] == null || plantingArea.regions[1] == null ||
            plantingArea.regions[2] == null || plantingArea.regions[3] == null)
        {
            ResolveRegions();
        }

        int column = point.x < (plantingArea.areaMinX + plantingArea.areaMaxX) * 0.5f ? 0 : 1;
        int row = point.z < (plantingArea.areaMinZ + plantingArea.areaMaxZ) * 0.5f ? 0 : 1;
        SeedSoilRegion region = plantingArea.regions[row * 2 + column];
        if (region == null)
            Debug.LogWarning($"No SeedSoilRegion assigned for planting quarter {row * 2 + column + 1} at {point}.", this);
        return region;
    }

    private bool TryGetImpact(Vector3 start, Vector3 velocity, out Vector3 impact)
    {
        Vector3 previous = start;
        for (int i = 1; i <= preview.points; i++)
        {
            float t = preview.previewTime * i / preview.points;
            Vector3 current = start + velocity * t + 0.5f * Physics.gravity * t * t;
            if (Physics.Linecast(previous, current, out RaycastHit hit, references.groundMask))
            { impact = hit.point; return true; }
            previous = current;
        }
        impact = default;
        return false;
    }

    private void HidePreview()
    {
        line.enabled = false;
        if (references.impactCircle != null) references.impactCircle.gameObject.SetActive(false);
    }

    private void AnimateImpactCircle()
    {
        if (references.impactCircle == null || !references.impactCircle.gameObject.activeSelf)
            return;

        impactCirclePulseTime += Time.deltaTime;
        float normalizedPulse = Mathf.PingPong(
            impactCirclePulseTime / preview.impactCirclePulseDuration, 1f);

        float scaleMultiplier = Mathf.Lerp(0.3f, 2f, normalizedPulse);
        references.impactCircle.localScale = impactCircleDefaultScale * scaleMultiplier;

        if (impactCircleRenderer != null)
        {
            impactCircleRenderer.material.color = Color.Lerp(
                impactCircleDefaultColor,
                Color.red,
                normalizedPulse);
        }
    }

    private float GetImpactCircleLocalRadius()
    {
        if (impactCircleLocalRadius > 0f)
            return impactCircleLocalRadius;

        return seeds.landingRadius / 2f;
    }

    private float CalculateThrowDistance(float handHeight)
    {
        float minDistance = Mathf.Min(heightThrow.minThrowDistance, heightThrow.maxThrowDistance);
        float maxDistance = Mathf.Max(heightThrow.minThrowDistance, heightThrow.maxThrowDistance);
        float minHeight = Mathf.Min(heightThrow.handHeightForMinDistance, heightThrow.handHeightForMaxDistance);
        float maxHeight = Mathf.Max(heightThrow.handHeightForMinDistance, heightThrow.handHeightForMaxDistance);

        float heightRange = maxHeight - minHeight;
        if (heightRange <= Mathf.Epsilon)
            return (minDistance + maxDistance) * 0.5f;

        float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, handHeight);
        normalizedHeight = Mathf.Pow(normalizedHeight, heightThrow.heightResponse);
        return Mathf.Lerp(minDistance, maxDistance, normalizedHeight);
    }
}
