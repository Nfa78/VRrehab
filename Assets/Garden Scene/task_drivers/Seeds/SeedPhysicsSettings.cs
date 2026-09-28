using UnityEngine;

/// <summary>
/// Optional shared profile for the physical behaviour of every seed in a burst.
/// Create one from Assets/Create/VR Stroke Rehab/Seed Physics Settings and assign it
/// to SeedThrowSpawner to keep seeds consistent across scenes and task difficulties.
/// </summary>
[CreateAssetMenu(fileName = "SeedPhysicsSettings", menuName = "VR Stroke Rehab/Seed Physics Settings")]
public sealed class SeedPhysicsSettings : ScriptableObject
{
    [Header("Rigidbody")]
    [SerializeField, Min(0.0001f)] private float massKg = 0.003f;
    [SerializeField] private bool useGravity = true;
    [SerializeField, Min(0f)] private float linearDamping = 0.12f;
    [SerializeField, Min(0f)] private float angularDamping = 1.75f;
    [SerializeField] private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Header("Aerodynamics")]
    [SerializeField] private bool enableAerodynamicDrag = true;
    [SerializeField, Min(0f)] private float dragCoefficient = 0.8f;
    [SerializeField, Min(0.000001f)] private float referenceAreaSquareMeters = 0.0015f;

    public bool EnableAerodynamicDrag => enableAerodynamicDrag;
    public float DragCoefficient => dragCoefficient;
    public float ReferenceAreaSquareMeters => referenceAreaSquareMeters;

    public void ApplyTo(Rigidbody rigidbody)
    {
        if (rigidbody == null)
        {
            return;
        }

        rigidbody.mass = massKg;
        rigidbody.useGravity = useGravity;
        rigidbody.linearDamping = linearDamping;
        rigidbody.angularDamping = angularDamping;
        rigidbody.collisionDetectionMode = collisionDetectionMode;
        rigidbody.interpolation = interpolation;
    }
}
