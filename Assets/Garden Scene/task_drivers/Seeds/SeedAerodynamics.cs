using UnityEngine;

/// <summary>
/// Applies inexpensive quadratic air drag to a spawned seed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SeedAerodynamics : MonoBehaviour
{
    [SerializeField] private bool dragEnabled = true;
    [SerializeField, Min(0f)] private float airDensityKgPerCubicMeter = 1.225f;
    [SerializeField, Min(0f)] private float dragCoefficient = 0.8f;
    [SerializeField, Min(0.000001f)] private float referenceAreaSquareMeters = 0.0015f;

    private Rigidbody _rigidbody;

    public void Configure(bool enableDrag, float newDragCoefficient, float newReferenceAreaSquareMeters)
    {
        dragEnabled = enableDrag;
        dragCoefficient = Mathf.Max(0f, newDragCoefficient);
        referenceAreaSquareMeters = Mathf.Max(0.000001f, newReferenceAreaSquareMeters);
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!dragEnabled || _rigidbody == null || _rigidbody.isKinematic)
        {
            return;
        }

        Vector3 velocity = _rigidbody.linearVelocity;
        float speedSquared = velocity.sqrMagnitude;
        if (speedSquared <= 0.0001f)
        {
            return;
        }

        // Fd = 0.5 * rho * Cd * A * v^2, opposite the direction of travel.
        float forceMagnitude = 0.5f * airDensityKgPerCubicMeter * dragCoefficient * referenceAreaSquareMeters * speedSquared;
        _rigidbody.AddForce(-velocity.normalized * forceMagnitude, ForceMode.Force);
    }
}
