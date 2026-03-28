using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemTracker : MonoBehaviour
{
    private bool hasPlayedOil = false;
    private bool hasPlayedSpice = false;
    private AudioSource success;
    public float newSmoothness = 1f;
    public Color targetColor = new Color(0.59f, 0.29f, 0f); // color of the carrots after adding spices
    public float colorChangeDuration = 2f; // time to transition from uncooked color to cooked

    void Start()
    {
        // Find the Scene Manager GameObject by name
        GameObject sceneManager = GameObject.Find("Scene Manager");

        if (sceneManager != null)
        {
            // Retrieve the AudioSource component
            success = sceneManager.GetComponent<AudioSource>();

            if (success == null)
            {
                Debug.LogWarning($"AudioSource not found on GameObject Scene Manager.");
            }
        }
        else
        {
            Debug.LogWarning($"GameObject Scene Manager not found in the scene.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Oil"))
        {
            Debug.Log("CARROT PIECE BEING HIT BY OIL!!!");
            if(CollisionManager.RegisterCollision(gameObject))
            {
                ModifySmoothness(other.gameObject);
            }
        } else if(other.gameObject.CompareTag("Spice"))
        {
            if (CollisionManager.RegisterCollision(gameObject))
            {
                StartCoroutine(ChangeColorOverTime(other.gameObject));
            }
        }
    }

    /*private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Oil"))
        {
            Debug.Log("CARROT PIECE BEING HIT BY OIL!!!");
            if (CollisionManager.RegisterCollision(gameObject))
            {
                ModifySmoothness(other.gameObject);
            }
        }
        else if (other.gameObject.CompareTag("Spice"))
        {
            if (CollisionManager.RegisterCollision(gameObject))
            {
                StartCoroutine(ChangeColorOverTime(other.gameObject));
            }
        }
    }*/

    void Update()
    {
        if (CollisionManager.GetUniqueCollisionCount() == 10)
        {
            if(!hasPlayedOil)
            {
                success.Play();
                hasPlayedOil = true;
                CollisionManager.ClearHashSet();
            } else if(hasPlayedOil && !hasPlayedSpice)
            {
                success.Play();
                hasPlayedSpice = true;
            }    
        }
    }

    private void ModifySmoothness(GameObject obj) // function to make the oiled carrots look sleek
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            foreach (Material material in renderer.materials) // For objects with multiple materials
            {
                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", newSmoothness);
                    Debug.Log($"Smoothness of {obj.name} set to {newSmoothness}");
                }
                else
                {
                    Debug.LogWarning($"Material on {obj.name} does not have a '_Smoothness' property.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"No Renderer found on {obj.name}");
        }
    }

    private IEnumerator ChangeColorOverTime(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                {
                    Color startColor = material.color; // Get the current color from the material
                    float elapsedTime = 0f;

                    while (elapsedTime < colorChangeDuration)
                    {
                        elapsedTime += Time.deltaTime;
                        material.color = Color.Lerp(startColor, targetColor, elapsedTime / colorChangeDuration);
                        yield return null;
                    }

                    material.color = targetColor; // Ensure the final color is exact
                    Debug.Log($"Color of {obj.name} changed to brown.");
                }
                else
                {
                    Debug.LogWarning($"Material on {obj.name} does not have a '_Color' property.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"No Renderer found on {obj.name}");
        }
    }
}
