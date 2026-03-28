using UnityEngine;

public class PinchColorChanger : MonoBehaviour
{
    public OVRHand hand; // Assign the OVRHand component in the inspector
    public OVRHand.HandFinger finger = OVRHand.HandFinger.Ring; // Use OVRHand.HandFinger
    public Material targetMaterial; // Assign the material to change

    private Color startColor = Color.blue;
    private Color endColor = Color.yellow;

    void Update()
    {
        if (hand == null || targetMaterial == null) return;

        // Get the pinch strength of the specified finger
        float pinchStrength = hand.GetFingerPinchStrength(finger);

        // Lerp between blue and yellow based on pinch strength
        Color newColor = Color.Lerp(startColor, endColor, pinchStrength);

        // Apply the color to the material
        targetMaterial.color = newColor;
    }
}
