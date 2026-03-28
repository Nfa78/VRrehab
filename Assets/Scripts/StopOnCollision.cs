using UnityEngine;

public class StopOnCollision : MonoBehaviour
{
    private Rigidbody rb;
    private bool isHeld = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnGrab()
    {
        isHeld = true;
        rb.isKinematic = false;
    }

    public void OnRelease()
    {
        isHeld = false;
        //rb.isKinematic = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isHeld)
        {
            // Stop movement when hitting a wall
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Optionally, you can lock the position for more rigid stopping
            rb.constraints = RigidbodyConstraints.FreezePositionX |
                             RigidbodyConstraints.FreezePositionY |
                             RigidbodyConstraints.FreezePositionZ;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (isHeld)
        {
            // Unlock position constraints when no longer colliding
            rb.constraints = RigidbodyConstraints.None;
        }
    }
}
