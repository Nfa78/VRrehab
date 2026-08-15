using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marker component used by seed gates to recognize thrown seeds.
/// </summary>
[DisallowMultipleComponent]
public class SeedProjectileMarker : MonoBehaviour
{
    private static readonly List<SeedProjectileMarker> activeMarkers = new List<SeedProjectileMarker>();

    [SerializeField] private int throwId = -1;

    public static IReadOnlyList<SeedProjectileMarker> ActiveMarkers => activeMarkers;
    public int ThrowId => throwId;
    public float ApproximateRadiusWorld => GetApproximateRadiusWorld();

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

    public void SetThrowId(int value)
    {
        throwId = value;
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
