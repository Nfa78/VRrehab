using UnityEngine;

[DisallowMultipleComponent]
public class BucketHandlePhysicsSetup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform handle;
    [SerializeField] private Transform leftPivot;
    [SerializeField] private Transform rightPivot;

    [Header("Body Rigidbody")]
    [SerializeField] private float bucketMass = 2.5f;
    [SerializeField] private float bucketDrag = 0.2f;
    [SerializeField] private float bucketAngularDrag = 2f;

    [Header("Handle Rigidbody")]
    [SerializeField] private float handleMass = 0.2f;
    [SerializeField] private float handleDrag = 0.05f;
    [SerializeField] private float handleAngularDrag = 0.8f;

    [Header("Hinge")]
    [SerializeField] private Vector3 fallbackAxis = Vector3.forward;
    [SerializeField] private float minAngle = -25f;
    [SerializeField] private float maxAngle = 160f;
    [SerializeField] private bool useSpring;
    [SerializeField] private float spring = 8f;
    [SerializeField] private float damper = 1.2f;
    [SerializeField] private float targetAngle = 0f;

    [Header("Auto Setup")]
    [SerializeField] private bool autoAddConvexMeshColliders = true;

    private void Reset()
    {
        AutoAssignReferences();
    }

    [ContextMenu("Setup Bucket Physics")]
    public void SetupBucketPhysics()
    {
        AutoAssignReferences();

        if (handle == null)
        {
            Debug.LogWarning($"{nameof(BucketHandlePhysicsSetup)}: No handle transform assigned.", this);
            return;
        }

        Rigidbody bucketBody = GetOrAddComponent<Rigidbody>(gameObject);
        bucketBody.mass = bucketMass;
        bucketBody.linearDamping = bucketDrag;
        bucketBody.angularDamping = bucketAngularDrag;
        bucketBody.interpolation = RigidbodyInterpolation.Interpolate;
        bucketBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (autoAddConvexMeshColliders)
        {
            EnsureConvexMeshCollider(bodyVisual);
            EnsureConvexMeshCollider(handle);
        }

        Rigidbody handleBody = GetOrAddComponent<Rigidbody>(handle.gameObject);
        handleBody.mass = handleMass;
        handleBody.linearDamping = handleDrag;
        handleBody.angularDamping = handleAngularDrag;
        handleBody.interpolation = RigidbodyInterpolation.Interpolate;
        handleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        HingeJoint hinge = GetOrAddComponent<HingeJoint>(handle.gameObject);
        hinge.connectedBody = bucketBody;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.enableCollision = false;

        Vector3 worldAnchor = GetWorldAnchor();
        Vector3 worldAxis = GetWorldAxis();

        hinge.anchor = handle.InverseTransformPoint(worldAnchor);
        hinge.connectedAnchor = transform.InverseTransformPoint(worldAnchor);
        hinge.axis = handle.InverseTransformDirection(worldAxis).normalized;

        hinge.useLimits = true;
        JointLimits limits = hinge.limits;
        limits.min = minAngle;
        limits.max = maxAngle;
        hinge.limits = limits;

        hinge.useSpring = useSpring;
        if (useSpring)
        {
            JointSpring jointSpring = hinge.spring;
            jointSpring.spring = spring;
            jointSpring.damper = damper;
            jointSpring.targetPosition = targetAngle;
            hinge.spring = jointSpring;
        }
    }

    private void AutoAssignReferences()
    {
        if (bodyVisual == null)
        {
            bodyVisual = FindChildContaining("body");
        }

        if (handle == null)
        {
            handle = FindChildContaining("handle");
        }
    }

    private Transform FindChildContaining(string partialName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform)
            {
                continue;
            }

            if (child.name.ToLowerInvariant().Contains(partialName))
            {
                return child;
            }
        }

        return null;
    }

    private Vector3 GetWorldAnchor()
    {
        if (leftPivot != null && rightPivot != null)
        {
            return (leftPivot.position + rightPivot.position) * 0.5f;
        }

        return handle != null ? handle.position : transform.position;
    }

    private Vector3 GetWorldAxis()
    {
        if (leftPivot != null && rightPivot != null)
        {
            Vector3 pivotAxis = rightPivot.position - leftPivot.position;
            if (pivotAxis.sqrMagnitude > 0.0001f)
            {
                return pivotAxis.normalized;
            }
        }

        return transform.TransformDirection(fallbackAxis).normalized;
    }

    private static void EnsureConvexMeshCollider(Transform target)
    {
        if (target == null || target.GetComponent<Collider>() != null)
        {
            return;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        MeshCollider meshCollider = target.gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = true;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        if (!target.TryGetComponent(out T component))
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
