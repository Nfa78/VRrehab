using UnityEngine;

/// <summary>
/// Optional per-seed visual enhancer for throw readability.
/// </summary>
[DisallowMultipleComponent]
public class SeedFlightVfx : MonoBehaviour
{
    [Header("Trail")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private Color trailColor = new Color(1f, 0.9f, 0.35f, 0.9f);
    [SerializeField] private float trailTime = 0.35f;
    [SerializeField] private float trailStartWidth = 0.014f;
    [SerializeField] private float trailEndWidth = 0.0025f;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private bool autodestructAfterLifetime = true;
    [SerializeField] private float lifetimeSeconds = 8f;

    private bool _initialized;

    public void Initialize(float lifetimeOverrideSeconds = -1f)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (enableTrail)
        {
            EnsureTrailRenderer();
        }

        float lifetime = lifetimeOverrideSeconds > 0f ? lifetimeOverrideSeconds : lifetimeSeconds;
        if (autodestructAfterLifetime && lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void EnsureTrailRenderer()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.time = Mathf.Max(0.01f, trailTime);
        trail.startWidth = Mathf.Max(0.001f, trailStartWidth);
        trail.endWidth = Mathf.Max(0.0005f, trailEndWidth);
        trail.minVertexDistance = 0.01f;
        trail.numCapVertices = 2;
        trail.numCornerVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        if (trailMaterial != null)
        {
            trail.material = trailMaterial;
        }
    }
}
