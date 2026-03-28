using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSystem : MonoBehaviour
{
    public GameObject welcomeCanvas;
    public GameObject helpCanvas;

    public float rotationSpeed = 5f; // Speed of the rotation
    private Coroutine rotateCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Welcome Canvas Active: " + welcomeCanvas.activeInHierarchy);
        
        if (other.CompareTag("MainCamera") && !welcomeCanvas.activeInHierarchy)
        {
            helpCanvas.SetActive(true);

            // Stop the previous rotation coroutine if it's running
            if (rotateCoroutine != null)
            {
                StopCoroutine(rotateCoroutine);
            }

            rotateCoroutine = StartCoroutine(RotateTowardsCamera(other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        helpCanvas.SetActive(false);
    }

    private IEnumerator RotateTowardsCamera(Transform cameraTransform)
    {
        // Calculate the target rotation based only on the Y-axis
        Vector3 targetDirection = cameraTransform.position - transform.position;
        targetDirection.y = 0; // Zero out the Y component to prevent any Y rotation
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        // Smoothly rotate towards the target rotation
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            yield return null; // Wait for the next frame
        }

        // Ensure the final rotation is set
        transform.rotation = targetRotation;
    }
}

