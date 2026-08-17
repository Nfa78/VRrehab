using TaskSystem;
using UnityEngine;

public class LeafsFallingEffect : MonoBehaviour
{
    [Header("Falling")]
    public float fallSpeed = 1.2f;
    public float resetYThreshold = 1f;

    [Header("Fallen Leaf Physics Copy")]
    public bool spawnPhysicsCopyBeforeReset = true;
    public Transform spawnedLeafParent;
    public float spawnedLeafLifetimeSeconds;
    public Vector3 fallbackBoxColliderSize = new Vector3(0.25f, 0.03f, 0.25f);

    [Header("Rake Progress")]
    public bool addRakeProgressReporterToPhysicsCopy = true;
    public float rakedLeafMinZ = 6.7f;
    public float rakedLeafMaxZ = 7.8f;
    public float rakedLeafProgressDelta = 1f;

    [Header("Rake Collision")]
    public bool addHoeImpulseToPhysicsCopy = true;
    public string hoeTag = "Hoe";
    public float hoeCollisionImpulse = 0.18f;

    [Header("Sway")]
    public float swayAmount = 1f;
    public float swaySpeed = 2f;

    [Header("Forward / Back Drift")]
    public float depthAmount = 0.5f;
    public float depthSpeed = 1.3f;

    [Header("Rotation")]
    public float flipAmount = 60f;
    public float flipSpeed = 2.5f;
    public float yawSpeed = 30f;

    private Vector3 currentPosition;
    private Vector3 initialPosition;
    private Quaternion startRotation;
    private float offset;
    private bool initialized;
    private bool simulationActive = true;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;

    public bool IsSimulationActive => simulationActive;

    public void ApplyCatchDifficulty(float newFallSpeed, float newSwayAmount, float newDepthAmount)
    {
        fallSpeed = Mathf.Max(0f, newFallSpeed);
        swayAmount = Mathf.Max(0f, newSwayAmount);
        depthAmount = Mathf.Max(0f, newDepthAmount);
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    void Start()
    {
        EnsureInitialized();
        ResetToStartPose();
    }

    public void SetSimulationActive(bool active, bool resetPose = false)
    {
        EnsureInitialized();
        simulationActive = active;
        enabled = active;
        SetRenderersEnabled(active);
        SetCollidersEnabled(active);

        if (resetPose)
        {
            ResetToStartPose();
        }
    }

    public void ResetToStartPose()
    {
        EnsureInitialized();
        currentPosition = initialPosition;
        transform.position = initialPosition;
        transform.rotation = startRotation;
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        currentPosition = transform.position;
        startRotation = transform.rotation;
        initialPosition = currentPosition;
        offset = Random.Range(0f, 100f);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        initialized = true;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = enabled;
            }
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = enabled;
            }
        }
    }

    void Update()
    {
        if (!simulationActive)
        {
            return;
        }

        float t = Time.time + offset;

        // Horizontal swaying
        float xMovement =
            Mathf.Sin(t * swaySpeed) * swayAmount;

        // Forward/back movement
        float zMovement =
            Mathf.Sin(t * depthSpeed + 1.5f) * depthAmount;

        // Falling
        currentPosition.y -= fallSpeed * Time.deltaTime;

        transform.position = new Vector3(
            currentPosition.x + xMovement,
            currentPosition.y,
            currentPosition.z + zMovement
        );

        // Leaf tilting / flipping
        float pitch =
            Mathf.Sin(t * flipSpeed) * flipAmount;

        float roll =
            Mathf.Cos(t * flipSpeed * 0.8f) * flipAmount;

        float yaw =
            t * yawSpeed;

        transform.rotation =
            startRotation *
            Quaternion.Euler(pitch, yaw, roll);

        if (currentPosition.y < resetYThreshold)
        {
            SpawnPhysicsCopyBeforeReset();
            ResetToStartPose();
        }
    }

    private void SpawnPhysicsCopyBeforeReset()
    {
        if (!spawnPhysicsCopyBeforeReset)
        {
            return;
        }

        Transform copyParent = spawnedLeafParent != null ? spawnedLeafParent : transform.parent;
        GameObject copy = Instantiate(gameObject, transform.position, transform.rotation, copyParent);
        copy.name = $"{gameObject.name}_Fallen";

        LeafsFallingEffect fallingEffect = copy.GetComponent<LeafsFallingEffect>();
        if (fallingEffect != null)
        {
            fallingEffect.enabled = false;
            Destroy(fallingEffect);
        }

        Rigidbody rigidbody = copy.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = copy.AddComponent<Rigidbody>();
        }

        rigidbody.isKinematic = false;
        rigidbody.useGravity = true;

        BoxCollider boxCollider = copy.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = copy.AddComponent<BoxCollider>();
        }

        ConfigureBoxCollider(copy.transform, boxCollider);
        ConfigureRakeProgressReporter(copy);
        ConfigureHoeImpulse(copy);

        if (spawnedLeafLifetimeSeconds > 0f)
        {
            Destroy(copy, spawnedLeafLifetimeSeconds);
        }
    }

    private void ConfigureRakeProgressReporter(GameObject copy)
    {
        if (!addRakeProgressReporterToPhysicsCopy)
        {
            return;
        }

        RakedLeafProgressReporter reporter = copy.GetComponent<RakedLeafProgressReporter>();
        if (reporter == null)
        {
            reporter = copy.AddComponent<RakedLeafProgressReporter>();
        }

        reporter.Configure(rakedLeafMinZ, rakedLeafMaxZ, rakedLeafProgressDelta);
    }

    private void ConfigureHoeImpulse(GameObject copy)
    {
        if (!addHoeImpulseToPhysicsCopy)
        {
            return;
        }

        RakedLeafHoeImpulse impulse = copy.GetComponent<RakedLeafHoeImpulse>();
        if (impulse == null)
        {
            impulse = copy.AddComponent<RakedLeafHoeImpulse>();
        }

        impulse.Configure(hoeTag, hoeCollisionImpulse);
    }

    private void ConfigureBoxCollider(Transform copyTransform, BoxCollider boxCollider)
    {
        Renderer[] renderers = copyTransform.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds localBounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;

            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(min.x, min.y, min.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(min.x, min.y, max.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(min.x, max.y, min.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(min.x, max.y, max.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(max.x, min.y, min.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(max.x, min.y, max.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(max.x, max.y, min.z));
            EncapsulateWorldPoint(copyTransform, ref localBounds, ref hasBounds, new Vector3(max.x, max.y, max.z));
        }

        if (hasBounds)
        {
            boxCollider.center = localBounds.center;
            boxCollider.size = localBounds.size;
            return;
        }

        boxCollider.center = Vector3.zero;
        boxCollider.size = fallbackBoxColliderSize;
    }

    private static void EncapsulateWorldPoint(
        Transform root,
        ref Bounds localBounds,
        ref bool hasBounds,
        Vector3 worldPoint)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }
}
