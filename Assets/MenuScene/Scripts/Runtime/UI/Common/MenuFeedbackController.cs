using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class MenuFeedbackController : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private float maxVisibleDuration = 5f;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;

        private CanvasGroup canvasGroup;
        private Coroutine visibilityRoutine;

        private void Awake()
        {
            EnsureInitialized();
            HideImmediate();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        public void ShowInfo(string message)
        {
            EnsureInitialized();
            Show(message, infoColor);
        }

        public void ShowSuccess(string message)
        {
            EnsureInitialized();
            Show(message, successColor);
        }

        public void ShowWarning(string message)
        {
            EnsureInitialized();
            Show(message, warningColor);
        }

        public void ShowError(string message)
        {
            EnsureInitialized();
            Show(message, errorColor);
        }

        public void Clear()
        {
            EnsureInitialized();

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }

            HideImmediate();
        }

        public void Hide()
        {
            EnsureInitialized();
            StartHide(false);
        }

        public bool HasRequiredReferences(out string message)
        {
            if (messageLabel == null)
            {
                message = "Message label is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void AutoAssignReferences()
        {
            if (messageLabel == null)
            {
                var labelTransform = transform.Find("MessageLabel");
                if (labelTransform != null)
                {
                    messageLabel = labelTransform.GetComponent<TMP_Text>();
                }
            }

            if (closeButton == null)
            {
                closeButton = FindChildButton("Close");
            }

            if (closeButton == null)
            {
                closeButton = FindChildButton("CloseButton");
            }

            if (closeButton == null)
            {
                closeButton = FindChildButtonRecursive("Close");
            }

            if (closeButton == null)
            {
                closeButton = FindChildButtonRecursive("CloseButton");
            }
        }

        private void Show(string message, Color color)
        {
            if (messageLabel == null)
            {
                return;
            }

            messageLabel.text = message;
            messageLabel.color = color;
            StartShow();
        }

        private void SetRootActive(bool active)
        {
            var target = root != null ? root : gameObject;
            if (target != null && CanToggleRootActive(target))
            {
                target.SetActive(active);
            }
        }

        private void HandleCloseClicked()
        {
            Hide();
        }

        private void StartShow()
        {
            EnsureHostIsActive();
            StopVisibilityRoutine();
            visibilityRoutine = StartCoroutine(ShowRoutine());
        }

        private void StartHide(bool immediate)
        {
            EnsureHostIsActive();
            StopVisibilityRoutine();

            if (immediate)
            {
                HideImmediate();
                return;
            }

            visibilityRoutine = StartCoroutine(HideRoutine());
        }

        private void HideImmediate()
        {
            EnsureInitialized();
            StopVisibilityRoutine();
            SetCanvasAlpha(0f);
            SetRootActive(false);
        }

        private IEnumerator ShowRoutine()
        {
            float clampedTotalDuration = Mathf.Max(0f, maxVisibleDuration);
            float clampedFadeDuration = Mathf.Clamp(fadeDuration, 0f, clampedTotalDuration * 0.5f);
            float visibleDelay = Mathf.Max(0f, clampedTotalDuration - (clampedFadeDuration * 2f));

            SetRootActive(true);
            SetCanvasAlpha(0f);

            if (clampedFadeDuration > 0f)
            {
                yield return FadeCanvas(0f, 1f, clampedFadeDuration);
            }
            else
            {
                SetCanvasAlpha(1f);
            }

            if (visibleDelay > 0f)
            {
                yield return new WaitForSeconds(visibleDelay);
            }

            if (clampedFadeDuration > 0f)
            {
                yield return FadeCanvas(1f, 0f, clampedFadeDuration);
            }
            else
            {
                SetCanvasAlpha(0f);
            }

            SetRootActive(false);
            visibilityRoutine = null;
        }

        private IEnumerator HideRoutine()
        {
            if (canvasGroup == null || fadeDuration <= 0f)
            {
                HideImmediate();
                yield break;
            }

            yield return FadeCanvas(canvasGroup.alpha, 0f, fadeDuration);
            SetRootActive(false);
            visibilityRoutine = null;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetCanvasAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetCanvasAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetCanvasAlpha(to);
        }

        private void StopVisibilityRoutine()
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }
        }

        private CanvasGroup GetOrAddCanvasGroup()
        {
            var target = root != null ? root : gameObject;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
            bool isVisible = alpha > 0.001f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        private Button FindChildButton(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private Button FindChildButtonRecursive(string childName)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == childName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private bool CanToggleRootActive(GameObject target)
        {
            return target != gameObject;
        }

        private void EnsureInitialized()
        {
            AutoAssignReferences();
            if (canvasGroup == null)
            {
                canvasGroup = GetOrAddCanvasGroup();
            }
        }

        private void EnsureHostIsActive()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            var target = root != null ? root : gameObject;
            if (target != null && target != gameObject && !target.activeSelf)
            {
                target.SetActive(true);
            }
        }
    }
}
