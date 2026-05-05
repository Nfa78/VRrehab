using UnityEngine;

public class WaterSpillSetup : MonoBehaviour
{
    [SerializeField] private Transform waterExitPoint;
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private bool autoCreateParticles = true;
    [SerializeField] private bool boostParticleVisibility = true;
    [SerializeField] private Vector3 localEmissionDirection = Vector3.forward;
    [SerializeField] private float spawnForwardOffset = 0.005f;

    public ParticleSystem WaterParticles => waterParticles;
    public Transform ActiveExitPoint => waterExitPoint != null ? waterExitPoint : transform;

    public void EnsureSetup()
    {
        EnsureParticleSystem();
        ConfigureParticleSystem();
    }

    public void AlignParticlesToExitPoint()
    {
        if (waterParticles == null)
        {
            return;
        }

        Transform exitPoint = ActiveExitPoint;
        Vector3 emissionDirection = GetWorldEmissionDirection();
        Vector3 spawnPosition = exitPoint.position + emissionDirection * spawnForwardOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(emissionDirection, exitPoint.up);
        waterParticles.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }

    private Vector3 GetWorldEmissionDirection()
    {
        Transform exitPoint = ActiveExitPoint;
        Vector3 localDirection = localEmissionDirection.sqrMagnitude > 0.0001f
            ? localEmissionDirection.normalized
            : Vector3.forward;
        return exitPoint.TransformDirection(localDirection).normalized;
    }

    private void EnsureParticleSystem()
    {
        if (waterParticles != null || !autoCreateParticles)
        {
            return;
        }

        Transform parent = ActiveExitPoint;
        Transform existing = parent.Find("AutoWaterParticles");
        if (existing != null)
        {
            waterParticles = existing.GetComponent<ParticleSystem>();
            if (waterParticles != null)
            {
                return;
            }
        }

        GameObject particlesObject = new GameObject("AutoWaterParticles");
        particlesObject.transform.SetParent(parent, false);
        particlesObject.transform.localPosition = Vector3.zero;
        particlesObject.transform.localRotation = Quaternion.identity;

        waterParticles = particlesObject.AddComponent<ParticleSystem>();
    }

    private void ConfigureParticleSystem()
    {
        if (waterParticles == null || !boostParticleVisibility)
        {
            return;
        }

        var main = waterParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.45f;
        main.startSpeed = 3.2f;
        main.startSize = 0.028f;
        main.startColor = new Color(0.45f, 0.75f, 1f, 0.9f);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = waterParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 95f;

        var shape = waterParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3f;
        shape.radius = 0.001f;
        shape.length = 0f;

        var noise = waterParticles.noise;
        noise.enabled = true;
        noise.strength = 0.02f;
        noise.frequency = 0.2f;

        var velocityOverLifetime = waterParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        var colorOverLifetime = waterParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.75f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = colorGradient;

        var sizeOverLifetime = waterParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.8f),
            new Keyframe(0.5f, 0.95f),
            new Keyframe(1f, 0.2f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var collision = waterParticles.collision;
        collision.enabled = false;

        var renderer = waterParticles.GetComponent<ParticleSystemRenderer>();
        renderer.enabled = true;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        Shader particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        }

        if (particleShader != null)
        {
            Material particleMaterial = new Material(particleShader);
            particleMaterial.color = new Color(0.6f, 0.85f, 1f, 0.9f);
            renderer.material = particleMaterial;
        }
    }
}
