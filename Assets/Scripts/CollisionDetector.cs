using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionDetector : MonoBehaviour
{
    public Transform drawer;

    private Rigidbody rb;

    private void OnCollisionEnter(Collision other)
    {
        if(other.collider.CompareTag("Knife"))
        {
            other.gameObject.transform.parent = drawer;
            rb = other.gameObject.GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }
        if (other.collider.CompareTag("Pan"))
        {
            other.gameObject.transform.parent = drawer;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if(other.collider.CompareTag("Knife"))
        {
            other.gameObject.transform.parent = null;
            rb = other.gameObject.GetComponent<Rigidbody>();
            rb.isKinematic = false;
        }
        if (other.collider.CompareTag("Pan"))
        {
            other.gameObject.transform.parent = null;
        }
    }
}
