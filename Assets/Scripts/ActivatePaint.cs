using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActivatePaint : MonoBehaviour
{
    public AudioSource paintSplash;
    public GameObject paintOnBrush;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Paint"))
        {
            paintSplash.Play();
            paintOnBrush.SetActive(true);
            //paintingScript.enabled = true;
        }
    }
}
