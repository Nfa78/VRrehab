using UnityEngine;

public class ScalerEffect : MonoBehaviour
{
    public float scaler;

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.localScale += Vector3.one * Mathf.Sin(Time.deltaTime) * scaler;
    }
}
