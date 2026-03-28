using UnityEngine;

public class OVRPerformanceTuner : MonoBehaviour
{
    void Start()
    {
        if (OVRManager.instance != null)
        {
            // Set CPU & GPU performance levels using enum values
            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;

            OVRManager.useDynamicFoveatedRendering = true;

            // Set refresh rate to the highest available
            float[] availableRates = OVRManager.display.displayFrequenciesAvailable;
            if (availableRates.Length > 0)
            {
                OVRManager.display.displayFrequency = availableRates[availableRates.Length - 1]; // Use highest available rate
                Debug.Log("Set display refresh rate to: " + OVRManager.display.displayFrequency);
            }
            else
            {
                Debug.LogWarning("No available refresh rates found.");
            }

            Debug.Log("OVR Performance settings applied.");
        }
        else
        {
            Debug.LogWarning("OVRManager instance not found.");
        }
    }
}
