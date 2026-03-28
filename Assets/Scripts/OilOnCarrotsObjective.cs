using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OilOnCarrotsObjective : MonoBehaviour
{
    public ParticleSystem oil;
    private int numPieces = 0; // number of carrot pieces that have been oiled up
    public AudioSource success;
    private bool hasPlayed = false;
    public Image checkbox;
    public Sprite checkedBox;
    public GameObject stoveKnobArrow;
    private bool pouringOil = false;
    public float oilPouringDuration = 2f;
    private float elapsedTime = 0f;

    void OnParticleCollision(GameObject other)
    {
        Debug.Log($"Particle collided with: {other.name}");
        if (other.CompareTag("Carrot"))
        {
            other.GetComponent<MeshRenderer>().material.SetFloat("_GlossMapScale", 0.9f);
            numPieces++;
            Debug.Log("HIT CARROT WITH OIL!!!!!!!!");
            if((numPieces >= 10) && !hasPlayed)
            {
                success.Play();
                checkbox.sprite = checkedBox;
                stoveKnobArrow.SetActive(true);
                hasPlayed = true;

            }
        }
        if(other.CompareTag("Pan"))
        {
            Debug.Log("HIT PAN WITH OIL!!!!!!");
            pouringOil = true;

        } else
        {
            pouringOil = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(pouringOil)
        {
            elapsedTime += Time.deltaTime;
        }
        if((elapsedTime >= oilPouringDuration) && !hasPlayed)
        {
            success.Play();
            checkbox.sprite = checkedBox;
            stoveKnobArrow.SetActive(true);
            hasPlayed = true;
        }
    }
}
