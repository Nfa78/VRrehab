using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoffeeFill : MonoBehaviour
{
    public Liquid liquid; // Reference to the Liquid script
    public float drainSpeed = 0.1f; // Units per second to drain

    private bool isDraining = false;

    void Update()
    {
        if (isDraining && liquid.fillAmount > 0)
        {
            liquid.fillAmount = Mathf.Max(0, liquid.fillAmount - drainSpeed * Time.deltaTime);
        }

        // Reset flag
        isDraining = false;
    }

    void OnParticleCollision(GameObject other)
    {
        isDraining = true;
    }
}
