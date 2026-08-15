using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

[DisallowMultipleComponent]
public class BucketHandlePhysicsSetup : MonoBehaviour
{
    [SerializeField] private Transform handle;
    [SerializeField] private Transform handleJointRoot;
    [SerializeField] private Transform leftPivot;
    [SerializeField] private Transform rightPivot;

    [SerializeField] private float minAngle = -160f;
    [SerializeField] private float maxAngle = 25f;
    [SerializeField] private Vector3 fallbackAxis = Vector3.forward;
    [SerializeField] private bool enableHandleBucketCollision = true;
    [SerializeField] private bool autoWireOnAwake = true;

    private void Awake()
    {
        if (autoWireOnAwake)
        {
            SetupBucketPhysics();
        }
    }

    private void Reset()
    {
        AutoAssignReferences();
    }

    [ContextMenu("Setup Bucket Physics")]
    public void SetupBucketPhysics()
    {
        AutoAssignReferences();

        if (handle == null || handleJointRoot == null)
        {
            Debug.LogWarning($"{nameof(BucketHandlePhysicsSetup)}: Missing handle or handleJointRoot reference.", this);
            return;
        }

        Rigidbody bucketBody = GetComponent<Rigidbody>();
        if (bucketBody == null)
        {
            Debug.LogWarning($"{nameof(BucketHandlePhysicsSetup)}: Bucket requires a Rigidbody.", this);
            return;
        }

        Rigidbody handleBody = handleJointRoot.GetComponent<Rigidbody>();
        if (handleBody == null)
        {
            Debug.LogWarning($"{nameof(BucketHandlePhysicsSetup)}: Handle joint root requires a Rigidbody.", this);
            return;
        }

        HingeJoint hinge = handleJointRoot.GetComponent<HingeJoint>();
        if (hinge == null)
        {
            Debug.LogWarning($"{nameof(BucketHandlePhysicsSetup)}: Handle joint root requires a HingeJoint.", this);
            return;
        }

        hinge.connectedBody = bucketBody;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.enableCollision = enableHandleBucketCollision;
        hinge.anchor = Vector3.zero;

        Vector3 worldAnchor = GetWorldAnchor();
        Vector3 worldAxis = GetWorldAxis();
        hinge.connectedAnchor = transform.InverseTransformPoint(worldAnchor);
        hinge.axis = handleJointRoot.InverseTransformDirection(worldAxis).normalized;

        hinge.useLimits = true;
        JointLimits limits = hinge.limits;
        limits.min = minAngle;
        limits.max = maxAngle;
        hinge.limits = limits;

        ConfigureHandleInteractions(handleJointRoot, handleBody);
    }

    private void AutoAssignReferences()
    {
        if (handle == null)
        {
            handle = FindChildContaining("handle");
        }

        if (handleJointRoot == null)
        {
            handleJointRoot = FindChildContaining("handlephysicsroot");
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

        return handleJointRoot != null ? handleJointRoot.position : transform.position;
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

        Vector3 worldFallback = transform.TransformDirection(fallbackAxis);
        return worldFallback.sqrMagnitude > 0.0001f ? worldFallback.normalized : Vector3.up;
    }

    private void ConfigureHandleInteractions(Transform physicsHandle, Rigidbody handleBody)
    {
        if (handle == null || physicsHandle == null || handleBody == null)
        {
            return;
        }

        Grabbable grabbable = handle.GetComponent<Grabbable>();
        if (grabbable != null)
        {
            grabbable.InjectOptionalTargetTransform(physicsHandle);
            grabbable.InjectOptionalRigidbody(handleBody);
        }

        HandGrabInteractable handGrab = handle.GetComponent<HandGrabInteractable>();
        if (handGrab != null)
        {
            handGrab.InjectRigidbody(handleBody);
            if (grabbable != null)
            {
                handGrab.InjectOptionalPointableElement(grabbable);
            }
        }
    }
}
