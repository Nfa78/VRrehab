using UnityEngine;

public class TrackPositions : MonoBehaviour
{

    public Transform trgt;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (trgt != null)
            transform.position = trgt.position;
    }
}
