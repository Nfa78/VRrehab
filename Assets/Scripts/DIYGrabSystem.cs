using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DIYGrabSystem : MonoBehaviour
{
    private bool isGrabbed = false;
    private GameObject hand;

    void Update()
    {
        if(isGrabbed)
        {
            AddParent();
        }
        else
        {
            RemoveParent();
        }
    }

    public void AddParent()
    {
        this.transform.SetParent(hand.transform);
    }

    public void RemoveParent()
    {
        this.transform.SetParent(null);
    }

    private void OnCollisionEnter(Collision other)
    {
        if(other.gameObject.layer == 6)
        {
            isGrabbed = true;
            hand = other.gameObject;
        }
    }
    private void OnCollisionExit(Collision other)
    {
        if(other.gameObject.layer == 6)
        {
            isGrabbed = false;
        }
    }
}
