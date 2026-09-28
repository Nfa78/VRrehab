using TaskSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SeedSoilRegion : MonoBehaviour
{
    [SerializeField] private Collider regionCollider;
    [SerializeField] private Renderer regionRenderer;
    [SerializeField] [Min(1)] private int capacity = 5;
    [SerializeField] private Color emptyColor = new Color(0.25f, 0.55f, 1f, 0.2f);
    [SerializeField] private Color fullColor = new Color(0.25f, 1f, 0.4f, 0.5f);
    [SerializeField] [Min(0.01f)] private float metallicPulseSpeed = 1f;
    [SerializeField] private int acceptedSeedCount;

    private SeedsTaskDriver taskDriver;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

    public int Capacity => capacity;
    public int AcceptedSeedCount => acceptedSeedCount;
    public bool IsFull => acceptedSeedCount >= capacity;

    private void Awake()
    {
        EnsureReferences();
        ApplyVisualState();
    }

    private void Update()
    {
        ApplyVisualState();
    }

    public void Configure(
        SeedsTaskDriver newTaskDriver,
        int newCapacity,
        Color newEmptyColor,
        Color newFullColor,
        float newMetallicPulseSpeed)
    {
        taskDriver = newTaskDriver;
        capacity = Mathf.Max(1, newCapacity);
        emptyColor = newEmptyColor;
        fullColor = newFullColor;
        metallicPulseSpeed = Mathf.Max(0.01f, newMetallicPulseSpeed);

        EnsureReferences();
        ResetFill();
    }

    public void ResetFill()
    {
        acceptedSeedCount = 0;
        ApplyVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || IsFull || taskDriver == null || !taskDriver.IsThrowStepActive())
        {
            return;
        }

        SeedProjectileMarker marker = other.GetComponentInParent<SeedProjectileMarker>();
        if (marker == null || !marker.TryClaimForSoilRegion())
        {
            return;
        }

        if (!taskDriver.HandleThrowSuccess())
        {
            return;
        }

        acceptedSeedCount++;
        Vector3 contactPoint = regionCollider != null
            ? regionCollider.ClosestPoint(marker.transform.position)
            : marker.transform.position;
        SeedSowingVfx.EmitSoilContact(contactPoint, transform.up);
        SetSeedAsPlanted(marker, contactPoint);
        ApplyVisualState();
    }

    private static void SetSeedAsPlanted(SeedProjectileMarker marker, Vector3 contactPoint)
    {
        if (marker == null)
        {
            return;
        }

        if (marker.TryGetComponent<Rigidbody>(out Rigidbody seedRigidbody))
        {
            seedRigidbody.linearVelocity = Vector3.zero;
            seedRigidbody.angularVelocity = Vector3.zero;
            seedRigidbody.isKinematic = true;
        }

        marker.transform.position = contactPoint + Vector3.up * 0.002f;
        Object.Destroy(marker.gameObject, 1.5f);
    }

    private void EnsureReferences()
    {
        if (regionCollider == null)
        {
            regionCollider = GetComponent<Collider>();
        }

        if (regionRenderer == null)
        {
            regionRenderer = GetComponent<Renderer>();
            if (regionRenderer == null)
            {
                regionRenderer = GetComponentInChildren<Renderer>(true);
            }
        }

        if (regionCollider != null)
        {
            regionCollider.isTrigger = true;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ApplyVisualState()
    {
        if (regionRenderer == null)
        {
            return;
        }

        float fill = Mathf.Clamp01((float)acceptedSeedCount / Mathf.Max(1, capacity));
        Color color = Color.Lerp(emptyColor, fullColor, fill);
        float metallic = Mathf.PingPong(Time.time * metallicPulseSpeed, 1f);

        regionRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetFloat(MetallicId, metallic);
        regionRenderer.SetPropertyBlock(propertyBlock);
    }
}
