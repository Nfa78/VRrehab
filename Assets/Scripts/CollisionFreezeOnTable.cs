using UnityEngine;

public class CollisionFreezeOnTable : MonoBehaviour
{
    private Rigidbody rb;
    private int tableLayer; // Set this to your table's layer

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        tableLayer = LayerMask.NameToLayer("Table"); // Make sure "Table" layer exists
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == tableLayer)
        {
            FreezeMotion();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.layer == tableLayer)
        {
            UnfreezeMotion();
        }
    }

    private void FreezeMotion()
    {
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
    }

    private void UnfreezeMotion()
    {
        rb.constraints = RigidbodyConstraints.None;
    }
}
