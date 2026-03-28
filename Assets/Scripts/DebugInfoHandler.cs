using UnityEngine;

public class DebugInfoHandler : MonoBehaviour
{
    public TMPro.TMP_Text playerTag;
    public TMPro.TMP_Text armTag;
    //public TMPro.TMP_Text timerTag;
    public TMPro.TMP_Text pointsTag;
    public int currentLevel; // 0 for kitchen, 1 for coffee

    private string playerName;
    private string chosenArm;
    private int currPoints;

    void Start()
    {
        playerName = PointsManager.Instance.CurrentUsername;
        chosenArm = PointsManager.Instance.GetAffectedArm();
        if (currentLevel == 1)
            currPoints = PointsManager.Instance.GetSecondLevelPoints();
        else if (currentLevel == 0)
            currPoints = PointsManager.Instance.Points;
        else if (currentLevel == 2)
            currPoints = PointsManager.Instance.ThirdLevelPoints;
        playerTag.text = playerName;
        armTag.text = chosenArm;
        pointsTag.text = currPoints.ToString();
    }

    void Update()
    {
        if (currentLevel == 1)
            currPoints = PointsManager.Instance.GetSecondLevelPoints();
        else if (currentLevel == 2)
            currPoints = PointsManager.Instance.ThirdLevelPoints;
        else if (currentLevel == 0)
            currPoints = PointsManager.Instance.Points;
        pointsTag.text = currPoints.ToString();
    }
}
