using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 1f;

    void Start()
    {
        // Start fully transparent
        SetAlpha(0f);
    }

    public void FadeToBlackAndTeleport(Transform player, Transform target)
    {
        StartCoroutine(FadeRoutine(player, target));
    }

    private IEnumerator FadeRoutine(Transform player, Transform target)
    {
        yield return StartCoroutine(Fade(1f)); // Fade to black

        // Teleport to position and rotation
        player.position = target.position;
        player.rotation = target.rotation;

        yield return StartCoroutine(Fade(0f)); // Fade back in
    }

    IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    void SetAlpha(float a)
    {
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
}