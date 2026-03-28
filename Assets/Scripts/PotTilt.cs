using UnityEngine;

public class PotTilt : MonoBehaviour
{
    [SerializeField] private Transform liquidCylinder; // Reference to the cylinder transform
    [SerializeField] private ParticleSystem pourParticles; // Reference to the particle system
    [SerializeField] private float minTiltAngleAtFull = 30f; // Minimum tilt angle when the cylinder is full
    [SerializeField] private float maxTiltAngleAtEmpty = 70f; // Maximum tilt angle when the cylinder is empty
    [SerializeField] private float drainRate = 0.1f; // Rate at which liquid drains when pouring

    private const float minLiquidHeightThreshold = 0.001f; // Threshold to consider as empty

    private bool isFilling = false; // To check if the cylinder is currently being filled

    public AudioSource waterSound;
    private bool isPlaying = false;

    private Renderer cylinderRenderer;

    private void Start()
    {
        cylinderRenderer = liquidCylinder.GetComponent<Renderer>();
    }

    private void Update()
    {
        // Get the current height of the liquid in case it's being modified by another script
        float currentLiquidHeight = liquidCylinder.localScale.y;

        // Calculate the dynamic minTiltAngle based on the current height of the liquid
        float tiltAngleThreshold = Mathf.Lerp(minTiltAngleAtFull, maxTiltAngleAtEmpty, 1 - currentLiquidHeight);

        // Check the tilt angle of the pot
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        // Drain only if the tilt angle is above the threshold and there’s liquid left
        if (tiltAngle > tiltAngleThreshold && currentLiquidHeight > minLiquidHeightThreshold)
        {
            // Start or continue pouring
            if (!pourParticles.isPlaying)
                pourParticles.Play();

            // Drain the liquid height over time
            currentLiquidHeight -= drainRate * Time.deltaTime;
            currentLiquidHeight = Mathf.Max(currentLiquidHeight, 0); // Clamp to 0

            if (!isPlaying)
            {
                waterSound.Play();
                isPlaying = true;
            }

            UpdateLiquidHeight(currentLiquidHeight);
        }
        else
        {
            // Stop pouring if not tilted enough
            if (pourParticles.isPlaying)
                pourParticles.Stop();
            waterSound.Pause();
            isPlaying = false;
        }

        // If the liquid height is very close to zero and we're not filling, stop pouring and hide the cylinder
        if (currentLiquidHeight <= minLiquidHeightThreshold && !isFilling)
        {
            if (pourParticles.isPlaying)
                pourParticles.Stop();
            waterSound.Pause();
            isPlaying = false;

            // Only hide the cylinder when it's empty and not being filled
            liquidCylinder.localScale = new Vector3(liquidCylinder.localScale.x, 0.0f, liquidCylinder.localScale.z);

            // Ensure cylinder renderer is disabled when liquid is "empty"
            if (cylinderRenderer.enabled)
            {
                cylinderRenderer.enabled = false;
            }
        }
    }

    private void UpdateLiquidHeight(float newHeight)
    {
        // Update the cylinder's scale and position to match the current liquid height and keep bottom anchored
        liquidCylinder.localScale = new Vector3(
            liquidCylinder.localScale.x,
            newHeight,
            liquidCylinder.localScale.z
        );

        // Adjust position to keep the bottom anchored
        liquidCylinder.localPosition = new Vector3(
            liquidCylinder.localPosition.x,
            newHeight / 2f,
            liquidCylinder.localPosition.z
        );
    }

    // This method is called by the other script to signal that the cylinder is filling
    public void SetFillingState(bool isFilling)
    {
        this.isFilling = isFilling;
    }
}
