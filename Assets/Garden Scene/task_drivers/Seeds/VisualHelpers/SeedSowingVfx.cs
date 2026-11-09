using UnityEngine;

/// <summary>
/// Small, non-glowing feedback bursts for sowing. The material is created with a
/// URP particle shader at runtime so the effect cannot fall back to magenta.
/// </summary>
public static class SeedSowingVfx
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material _particleMaterial;
    private static Material _seedMaterial;

    public static void EmitReleaseFlecks(Vector3 position, Vector3 direction, int count)
    {
        ParticleSystem particles = CreateBurst("SeedReleaseFlecks", position, Quaternion.LookRotation(SafeDirection(direction)));
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.42f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.009f);
        main.gravityModifier = 0.55f;
        main.startColor = new Color(0.55f, 0.36f, 0.16f, 0.7f);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.008f;

        particles.Play();
        particles.Emit(Mathf.Max(1, count));
    }

    public static void EmitSoilContact(Vector3 position, Vector3 surfaceNormal)
    {
        ParticleSystem particles = CreateBurst("SeedSoilContact", position + SafeDirection(surfaceNormal) * 0.002f, Quaternion.LookRotation(SafeDirection(surfaceNormal)));
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.26f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        main.gravityModifier = 1.4f;
        main.startColor = new Color(0.26f, 0.13f, 0.045f, 0.75f);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 42f;
        shape.radius = 0.012f;

        particles.Play();
        particles.Emit(5);
    }

    public static Material GetSeedMaterial(Color color)
    {
        if (_seedMaterial == null)
        {
            Shader shader = FindLitShader();
            if (shader == null)
            {
                return null;
            }

            _seedMaterial = new Material(shader)
            {
                name = "RuntimeSeedMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(_seedMaterial, color);
            if (_seedMaterial.HasProperty("_Smoothness"))
            {
                _seedMaterial.SetFloat("_Smoothness", 0.18f);
            }
        }

        return _seedMaterial;
    }

    private static ParticleSystem CreateBurst(string effectName, Vector3 position, Quaternion rotation)
    {
        Material material = GetParticleMaterial();
        if (material == null)
        {
            return null;
        }

        GameObject effect = new GameObject(effectName);
        effect.transform.SetPositionAndRotation(position, rotation);
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = material;

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;

        var emission = particles.emission;
        emission.enabled = false;

        Object.Destroy(effect, 0.7f);
        return particles;
    }

    private static Material GetParticleMaterial()
    {
        if (_particleMaterial != null)
        {
            return _particleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
        {
            return null;
        }

        _particleMaterial = new Material(shader)
        {
            name = "RuntimeSeedParticleMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        SetMaterialColor(_particleMaterial, Color.white);
        return _particleMaterial;
    }

    private static Shader FindLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit") ??
               Shader.Find("Standard") ??
               Shader.Find("Sprites/Default");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }
    }

    private static Vector3 SafeDirection(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }
}
