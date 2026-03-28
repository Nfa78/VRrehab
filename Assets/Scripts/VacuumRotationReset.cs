using UnityEngine;

public class VacuumRotationReset : MonoBehaviour
{
    [Tooltip("How far from upright the vacuum can tilt before resetting (in degrees)")]
    public float maxTiltAngle = 60f;

    [Tooltip("Time (in seconds) the vacuum must be tipped over before resetting")]
    public float resetDelay = 2f;

    [Tooltip("Optional: Position to reset to if you want it to return to a specific place")]
    public Vector3 resetPosition;
    public bool useFixedPosition = false;

    private float tippedTime = 0f;
    private Quaternion uprightRotation;

    void Start()
    {
        // Save the upright rotation for resetting
        uprightRotation = transform.rotation;
        if (!useFixedPosition)
            resetPosition = transform.position;
    }

    void Update()
    {
        // Check angle between vacuum's up vector and world up
        float angle = Vector3.Angle(transform.up, Vector3.up);

        if (angle > maxTiltAngle)
        {
            tippedTime += Time.deltaTime;

            if (tippedTime >= resetDelay)
            {
                ResetVacuum();
                tippedTime = 0f;
            }
        }
        else
        {
            // Reset timer if it recovers before delay
            tippedTime = 0f;
        }
    }

    void ResetVacuum()
    {
        // Reset position and rotation
        transform.position = resetPosition;
        transform.rotation = uprightRotation;

        // Optional: also reset velocity if it's a Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
