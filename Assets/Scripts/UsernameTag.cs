using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UsernameTag : MonoBehaviour
{
    public TMPro.TMP_Text tagg;
    public int lvl = 0;

    void Start()
    {
        if (lvl == 0)
            tagg.text = PointsManager.Instance.Points.ToString();
        else
            tagg.text = PointsManager.Instance.SecondLevelPoints.ToString();
    }

    void Update()
    {
        if (lvl == 0)
            tagg.text = PointsManager.Instance.Points.ToString();
        else
            tagg.text = PointsManager.Instance.SecondLevelPoints.ToString();
    }
}
