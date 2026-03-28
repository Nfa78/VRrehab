using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AttachWithJoint : MonoBehaviour
{
    private Rigidbody objectRigidbody;
    private FixedJoint fixedJoint;
    private Transform originalParent;

    private void Start()
    {
        // Store the Rigidbody and initial parent for easy reattachment
        objectRigidbody = GetComponent<Rigidbody>();
        originalParent = transform.parent;
    }

    public void AttachToHand(Transform handTransform)
    {
        // Enable physics for realistic collision behavior
        objectRigidbody.isKinematic = false;

        // Create a FixedJoint on the hand to attach the object
        fixedJoint = handTransform.gameObject.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = objectRigidbody;
        fixedJoint.breakForce = Mathf.Infinity; // Set break force as needed
        fixedJoint.breakTorque = Mathf.Infinity; // Set break torque as needed

        // Reparent to handTransform to help with orientation
        transform.parent = handTransform;
    }

    public void DetachFromHand()
    {
        // Remove the joint and reset parent
        if (fixedJoint != null)
        {
            Destroy(fixedJoint);
        }

        transform.parent = originalParent;
        //objectRigidbody.isKinematic = true; // Optionally set to kinematic after detaching
    }
}