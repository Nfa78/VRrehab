using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CanvasActivator : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
    private GameObject canvasObject; // Reference to the external Canvas object

    void Start()
    {
        // Try to get the XRSocketInteractor component on this GameObject
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        
        // If socketInteractor exists, register the Select Entered and Select Exited events
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnSelectEntered);
            //socketInteractor.selectExited.AddListener(OnSelectExited);
        }
        else
        {
            Debug.LogWarning("XRSocketInteractor component not found on this GameObject.");
        }
    }

    // Method to set the canvas from the LoadCharacter script
    public void SetCanvas(GameObject canvas)
    {
        canvasObject = canvas;
        Debug.Log("Canvas set in CanvasActivator: " + canvasObject.name);
        canvasObject.SetActive(false); // Make sure it's initially inactive
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log("Object selected, activating canvas.");
        // Activate the canvas when an object is selected in the socket
        if (canvasObject != null)
        {
            canvasObject.SetActive(true);
            Debug.Log("Canvas activated: " + canvasObject.name + " Active: " + canvasObject.activeSelf);
        }
    }

    /*private void OnSelectExited(SelectExitEventArgs args)
    {
        Debug.Log("Object deselected, deactivating canvas.");
        // Deactivate the canvas when the object is removed from the socket
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
    }*/

    private void OnDestroy()
    {
        // Only remove listeners if socketInteractor is not null
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnSelectEntered);
            //socketInteractor.selectExited.RemoveListener(OnSelectExited);
        }
    }
}
