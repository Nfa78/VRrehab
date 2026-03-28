using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class AntiGlitchSystem : MonoBehaviour
{
    public GameObject carrot;

    private HandGrabInteractable otherthingy;
    private bool loggedMissingInteractable;

    private void Start()
    {
        if (carrot == null)
        {
            Debug.LogError($"{nameof(AntiGlitchSystem)}: Carrot reference is missing.", this);
            enabled = false;
            return;
        }

        otherthingy = carrot.GetComponent<HandGrabInteractable>();
        if (otherthingy == null)
        {
            Debug.LogWarning($"{nameof(AntiGlitchSystem)}: No HandGrabInteractable found on carrot.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pan") || other.CompareTag("Knife"))
            return;

        var interactable = other.gameObject.GetComponentInChildren<HandGrabInteractable>();
        if (interactable == null)
        {
            if (!loggedMissingInteractable)
            {
                Debug.LogWarning($"{nameof(AntiGlitchSystem)}: No HandGrabInteractable found in trigger object.", other);
                loggedMissingInteractable = true;
            }
            return;
        }

        interactable.enabled = false;
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pan"))
            return;
        var interactable = other.gameObject.GetComponentInChildren<HandGrabInteractable>();
        if (interactable == null)
            return;

        interactable.enabled = true;
    }
}
