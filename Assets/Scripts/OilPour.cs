using UnityEngine;

public class OilPour : MonoBehaviour
{
    [Header("Tilt Settings")]
    [Tooltip("The maximum allowable tilt angle on the Z-axis before the particle system activates.")]
    public int tiltThreshold = 30;

    [Header("Particle System")]
    [Tooltip("The particle system to activate when the tilt threshold is exceeded.")]
    public ParticleSystem oilParticles;

    [Header("Audio Source")]
    [Tooltip("The audio to play when the tilt threshold is exceeded.")]
    public AudioSource audioSource;

    [Header("Debug")]
    [Tooltip("Enable to display the tilt angle in the console.")]
    public bool debugMode = false;

    private bool particlesPlaying = false;

    void Update()
    {
        // Get the local up vector of the GameObject
        Vector3 localUp = transform.forward;

        // Calculate the angle between the local up vector and the global up vector
        float tiltAngle = Vector3.Angle(localUp, Vector3.up);

        // Debug output
        if (debugMode)
        {
            Debug.Log($"Tilt Angle: {tiltAngle}");
        }

        // Trigger the particle system if the tilt angle exceeds the threshold
        if (tiltAngle > tiltThreshold)
        {
            if (!particlesPlaying)
            {
                oilParticles.Play();
                audioSource.Play();
                particlesPlaying = true;
            }
        }
        else
        {
            if (particlesPlaying)
            {
                oilParticles.Stop();
                audioSource.Stop();
                particlesPlaying = false;
            }
        }
    }

    private float CalculatePourAngle()
    {
        return Vector3.Angle(Vector3.down, transform.forward);
    }
}

