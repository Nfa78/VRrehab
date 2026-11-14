using UnityEngine;

public class Swipe : MonoBehaviour
{

    public Vector3 dest;
    Vector3 src;
    public bool slide = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        src = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (slide)
        {
            transform.position = Vector3.Lerp(transform.position, dest, Time.deltaTime);
        }
    }

    public void Slide()
    {
        slide = true;
    }
}
