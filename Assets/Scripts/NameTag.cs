using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class NameTag : MonoBehaviour
{
    public TMPro.TMP_Text nameTag;
    // Start is called before the first frame update
    void Start()
    {
        nameTag.text = PointsManager.Instance.CurrentUsername;
    }

    // Update is called once per frame
    void Update()
    {
        nameTag.text = PointsManager.Instance.CurrentUsername;
    }
}
