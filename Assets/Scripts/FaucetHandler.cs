using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaucetHandler : MonoBehaviour
{
    public ParticleSystem water;
    public GameObject faucetHandle;
    public AudioSource waterSound;

    private bool isPlaying = false;

    void Update()
    {
        // Check if faucet handle is within 10 degrees of facing left
        if (Vector3.Angle(faucetHandle.transform.up, Vector3.left) < 10)
        {
            if (!isPlaying)
            {
                water.Play();
                waterSound.Play();
                isPlaying = true;
            }
        }
        else
        {
            if (isPlaying)
            {
                water.Stop();
                waterSound.Pause();
                isPlaying = false;
            }
        }
    }
}

