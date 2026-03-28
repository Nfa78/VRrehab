using UnityEngine;

public class WaterFill : MonoBehaviour
{
    public Transform cylinder; // Reference to the cylinder (water) inside the pot
    public ParticleSystem faucetParticleSystem; // Reference to the particle system
    public float fillSpeed = 0.1f; // Speed at which the water fills
    public float maxFillScale = 1.0f; // Maximum y-scale the water can reach
    private Renderer cylinderRenderer;

    [SerializeField] private PotTilt potTiltHandler; // Reference to PotTiltHandler

    private bool isParticleColliding = false; // Track if the particle system is still colliding

    private void Start()
    {
        // Set the initial scale and make the cylinder invisible
        cylinder.localScale = new Vector3(cylinder.localScale.x, 0.0f, cylinder.localScale.z);
        cylinderRenderer = cylinder.GetComponent<Renderer>();
        cylinderRenderer.enabled = false; // Start with the cylinder invisible
    }

    private void OnParticleCollision(GameObject other)
    {
        // Check if the particle hitting is from the faucet
        if (other == faucetParticleSystem.gameObject)
        {
            if (!isParticleColliding)
            {
                isParticleColliding = true;
                potTiltHandler.SetFillingState(true);
                potTiltHandler.enabled = false; // Disable PotTiltHandler during filling to prevent interference
            }

            // Enable the renderer as soon as particles hit to make it visible
            if (!cylinderRenderer.enabled)
                cylinderRenderer.enabled = true;

            // Gradually increase the y-scale within max scale limits
            if (cylinder.localScale.y < maxFillScale)
            {
                float newYScale = cylinder.localScale.y + fillSpeed * Time.deltaTime;
                newYScale = Mathf.Min(newYScale, maxFillScale); // Ensure it doesn't exceed the max scale
                cylinder.localScale = new Vector3(cylinder.localScale.x, newYScale, cylinder.localScale.z);

                // Adjust the position to keep the bottom anchored (height / 2)
                cylinder.localPosition = new Vector3(cylinder.localPosition.x, newYScale / 2, cylinder.localPosition.z);
            }
        }
    }

    private void Update()
    {
        // If particles are no longer colliding, re-enable the PotTiltHandler
        if (isParticleColliding && !faucetParticleSystem.isPlaying)
        {
            potTiltHandler.enabled = true; // Re-enable PotTiltHandler after particles stop colliding
            isParticleColliding = false; // Reset collision tracking
        }

        // Re-enable PotTiltHandler if the cylinder has filled to max
        if (cylinder.localScale.y >= maxFillScale && !potTiltHandler.enabled)
        {
            potTiltHandler.enabled = true;
        }
    }
}
