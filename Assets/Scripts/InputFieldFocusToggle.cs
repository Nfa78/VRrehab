using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InputFieldFocusToggle : MonoBehaviour, ISelectHandler
{
    public GameObject targetObject;  // The GameObject you want to appear
    private InputField inputField;

    void Start()
    {
        inputField = GetComponent<InputField>();
    }

    // Called when the input field is selected (focused)
    public void OnSelect(BaseEventData eventData)
    {
        if (targetObject != null)
            targetObject.SetActive(true);
    }
}
