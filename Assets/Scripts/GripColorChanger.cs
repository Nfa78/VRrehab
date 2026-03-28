using UnityEngine;
using System;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;

public class GripColorChanger : MonoBehaviour
{
    public HandGrabAPI handGrabAPI; // Assign the HandGrabAPI component in the inspector
    public Material targetMaterial; // Assign the material to change

    private Color startColor = Color.blue;
    private Color endColor = Color.yellow;

    void Update()
    {
        if (handGrabAPI == null || targetMaterial == null) return;

        // Define the GrabbingRule (e.g., using all fingers for palm grab)
        GrabbingRule grabbingRule = GrabbingRule.DefaultPalmRule;

        // Get the grab strength using HandGrabAPI, specify the fingers and whether to include currently grabbing fingers
        float grabStrength = handGrabAPI.GetHandPalmScore(grabbingRule, true);

        // Lerp between blue and yellow based on grab strength
        Color newColor = Color.Lerp(startColor, endColor, grabStrength);

        // Apply the color to the material
        targetMaterial.color = newColor;
    }
}
