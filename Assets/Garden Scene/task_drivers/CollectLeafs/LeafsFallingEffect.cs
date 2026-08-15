using UnityEngine;

public class LeafsFallingEffect : MonoBehaviour
{
    [Header("Falling")]
    public float fallSpeed = 1.2f;

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

        if (currentPosition.y < 0) // reset
        {
            ResetToStartPose();
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
    }
}
