using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEditor.Localization.Plugins.XLIFF.V20;
public class ReportCollisions : MonoBehaviour
{

    List<GameObject> children;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        r_spreadToAllChildren();
    }

    void r_spreadToAllChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).GetComponent<ReportCollisions>() == null)
                transform.GetChild(i).gameObject.AddComponent<ReportCollisions>();
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Objects Collided :: ==> " + gameObject.name + "collided with " + collision.gameObject.name);
    }
}
