using UnityEngine;

public class OutlinePulse : MonoBehaviour
{
    private Renderer rend;
    private MaterialPropertyBlock propBlock;

    [SerializeField] private float minThickness = 0.02f;
    [SerializeField] private float maxThickness = 0.045f;
    [SerializeField] private float speed = 2f; // Controls how fast it pulses

    void Awake()
    {
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        // Use sine wave to oscillate between 0 and 1
        float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;

        // Map it to your desired thickness range
        float thickness = Mathf.Lerp(minThickness, maxThickness, t);

        // Update property block
        rend.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_Outline_Thickness", thickness);
        rend.SetPropertyBlock(propBlock);
    }
}
