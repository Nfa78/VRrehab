using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StarsHandler : MonoBehaviour
{
    public Sprite[] starSprites;
    public Image img;
    public int lvl; // 0 for Kitchen lvl, 1 for Coffee lvl, 2 for Living Room lvl
    private bool loggedMissingRefs;

    void Update()
    {
        if (img == null || starSprites == null || starSprites.Length == 0 || PointsManager.Instance == null)
        {
            if (!loggedMissingRefs)
            {
                Debug.LogWarning($"{nameof(StarsHandler)}: Missing Image, star sprites, or PointsManager.", this);
                loggedMissingRefs = true;
            }
            return;
        }

        int points;
        if (lvl == 0)
        {
            points = PointsManager.Instance.Points;
        }
        else if (lvl == 1)
        {
            points = PointsManager.Instance.SecondLevelPoints;
        }
        else
        {
            points = PointsManager.Instance.ThirdLevelPoints;
        }

        int index = Mathf.Clamp(points / 5, 0, starSprites.Length - 1);
        img.sprite = starSprites[index];
    }
}
