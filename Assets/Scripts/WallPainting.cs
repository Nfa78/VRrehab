using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallPainting : MonoBehaviour
{
    public GameObject paintPrefab;
    public float rayDistance = 0.2f;

    private Vector3 contactPos;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        Debug.DrawRay(transform.position, transform.up * 2f, Color.red);
    }


    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("PaintableWall"))
        {
            Debug.Log("Triggered! Casting ray to detect surface...");
            var contact = collision.contacts[0];
            contactPos = contact.point; //this is the Vector3 position of the point of contact
            Quaternion rotation = Quaternion.LookRotation(Vector3.right, -collision.gameObject.transform.right);
            Instantiate(paintPrefab, contactPos, rotation);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PaintableWall"))
            audioSource.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("PaintableWall"))
            audioSource.Pause();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("PaintableWall"))
        {
            Debug.Log("Triggered! Casting ray to detect surface...");
            var collisionPoint = other.ClosestPoint(transform.position);

            float xRotation = transform.eulerAngles.x;
            Vector3 rotationPoint = new Vector3(xRotation, 0f, 90f);
            Quaternion rotation = Quaternion.Euler(rotationPoint); // Convert to Quaternion

            Instantiate(paintPrefab, collisionPoint + new Vector3(0.001f, 0, 0), rotation);
        }

    }
}
