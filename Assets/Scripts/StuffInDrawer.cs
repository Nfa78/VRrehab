using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StuffInDrawer : MonoBehaviour
{
    private Transform target; // The object this one sticks to
    private bool isSticking = false;
    private Vector3 positionOffset;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Drawer") && !isSticking)
        {
            target = collision.transform;
            positionOffset = target.InverseTransformPoint(transform.position);
            isSticking = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Drawer") && isSticking && collision.transform == target)
        {
            target = null;
            positionOffset = Vector3.zero;
            isSticking = false;
        }
    }

    private void FixedUpdate()
    {
        if (isSticking && target != null)
        {
            // Manually synchronize position
            transform.position = target.TransformPoint(positionOffset);
        }
    }
}
