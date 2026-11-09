using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marker component used by seed targets to recognize thrown seeds.
/// </summary>
[DisallowMultipleComponent]
public class SeedProjectileMarker : MonoBehaviour
{
    private static readonly List<SeedProjectileMarker> activeMarkers = new List<SeedProjectileMarker>();

    [SerializeField] private int throwId = -1;
    private bool claimedForSoilRegion;
    private bool landingChecked;

    public static IReadOnlyList<SeedProjectileMarker> ActiveMarkers => activeMarkers;
    public int ThrowId => throwId;
    public float ApproximateRadiusWorld => GetApproximateRadiusWorld();
    public bool HasBeenClaimedForSoilRegion => claimedForSoilRegion;

    private void OnEnable()
    {
        if (!activeMarkers.Contains(this))
        {
            activeMarkers.Add(this);
        }
    }

    private void OnDisable()
    {
        activeMarkers.Remove(this);
    }

    private void Update()
    {
        if (landingChecked || transform.position.y > 0f)
        {
            return;
        }

        landingChecked = true;
        SeedSoilRegion[] regions = FindObjectsByType<SeedSoilRegion>(FindObjectsSortMode.None);
        Debug.Log($"[SeedProjectileMarker] {name} landed at {transform.position}; regions={regions.Length}.", this);
        for (int i = 0; i < regions.Length; i++)
        {
            SeedSoilRegion region = regions[i];
            if (region != null && region.ContainsPoint(transform.position))
            {
                region.AcceptSeed(this, transform.position);
                Debug.Log($"[SeedProjectileMarker] Region {region.name} accepted seed.", region);
                return;
            }
        }
        Debug.Log($"[SeedProjectileMarker] No soil region contains {transform.position}.", this);
    }

    public void SetThrowId(int value)
    {
        throwId = value;
        claimedForSoilRegion = false;
    }

    /// <summary>
    /// Claims this seed for a single soil region so a bounced seed cannot score twice.
    /// </summary>
    public bool TryClaimForSoilRegion()
    {
        if (claimedForSoilRegion)
        {
            return false;
        }

        claimedForSoilRegion = true;
        return true;
    }

    private float GetApproximateRadiusWorld()
    {
        if (TryGetComponent<Collider>(out Collider markerCollider))
        {
            Bounds bounds = markerCollider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        if (TryGetComponent<Renderer>(out Renderer markerRenderer))
        {
            Bounds bounds = markerRenderer.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        Vector3 scale = transform.lossyScale;
        return Mathf.Max(scale.x, scale.y, scale.z) * 0.5f;
    }
}
