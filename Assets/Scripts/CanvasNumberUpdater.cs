using UnityEngine;

public class CanvasNumberUpdater : MonoBehaviour
{
    [HideInInspector]
    public int currentTally = 0;

    public TMPro.TMP_Text canvasText;

    public void UpdateTallyOnClipboard()
    {
        // Update the localized progress number (e.g., "1/10", "2/10", etc.)
        canvasText.text = System.Text.RegularExpressions.Regex.Replace(canvasText.text, @"(\()([^\:]+:\s*)\d+/\d+(\))",
        match => match.Groups[1].Value + match.Groups[2].Value + currentTally + "/10" + match.Groups[3].Value);
    }
}
