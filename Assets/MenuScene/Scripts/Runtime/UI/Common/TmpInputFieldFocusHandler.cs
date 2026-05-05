using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VRStrokeRehab.MenuScene
{
    [RequireComponent(typeof(TMP_InputField))]
    public class TmpInputFieldFocusHandler : MonoBehaviour, IPointerClickHandler, ISelectHandler
    {
        private TMP_InputField inputField;

        private void Awake()
        {
            inputField = GetComponent<TMP_InputField>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Focus(eventData);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Activate();
        }

        private void Focus(BaseEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject, eventData);
            }

            Activate();
        }

        private void Activate()
        {
            if (inputField == null || !inputField.interactable)
            {
                return;
            }

            inputField.Select();
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }
    }
}
