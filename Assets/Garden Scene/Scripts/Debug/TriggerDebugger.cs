using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ManualDebug
{
    public GameObject g1;
    public GameObject g2;
}

public class TriggerDebugger : MonoBehaviour
{
    private const string DefaultDetectTag = "HandAnchor";

    public List<string> detectTags = new List<string> { DefaultDetectTag };

    public enum Method { Trigger, Distance, Both }
    public Method method;
    public bool ManualDebug;
    public ManualDebug manualDebug;

    [Header("Distance Detection")]
    [SerializeField][Min(0f)] private float parasiteRadius = 1.4f;
    [SerializeField] private bool drawDistanceLines = true;
    [SerializeField] private bool skipDetectTagObjects = true;

    private List<List<GameObject>> objectsPerTag = new List<List<GameObject>>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EnsureDetectTags();
        Debug.Log("Trigger denbugger on ! METHOD : " + method.ToString());
        getObjectsPerTag();
        PlantParasites();
        Debug.Log("FINISHED SETUP");

    }

    void getObjectsPerTag()
    {
        objectsPerTag.Clear();

        foreach (string tag in detectTags)
        {
            objectsPerTag.Add(GetObjectsWithTag(tag));
        }
    }

    private void EnsureDetectTags()
    {
        if (detectTags == null)
        {
            detectTags = new List<string>();
        }

        if (detectTags.Count == 0)
        {
            detectTags.Add(DefaultDetectTag);
        }
    }

    List<GameObject> GetObjectsWithTag(string tag)
    {
        List<GameObject> temp = new List<GameObject>();

        if (string.IsNullOrWhiteSpace(tag))
        {
            return temp;
        }

        try
        {
            temp.AddRange(GameObject.FindGameObjectsWithTag(tag));
        }
        catch (UnityException exception)
        {
            Debug.LogWarning($"TriggerDebugger cannot find objects with tag '{tag}': {exception.Message}", this);
        }

        return temp;
    }

    void PlantParasites()
    {
        bool useTriggerDetection = method == Method.Trigger || method == Method.Both;
        bool useDistanceDetection = method == Method.Distance || method == Method.Both;

        Collider[] collider = FindObjectsByType<Collider>(FindObjectsSortMode.InstanceID);
        foreach (Collider c in collider)
        {
            if (c == null || (skipDetectTagObjects && IsDetectTargetCollider(c)))
            {
                continue;
            }

            TriggerDebuggerParasite tdp = c.gameObject.GetComponent<TriggerDebuggerParasite>();
            if (tdp == null)
            {
                tdp = c.gameObject.AddComponent<TriggerDebuggerParasite>();
            }

            tdp.Configure(parasiteRadius, useTriggerDetection, useDistanceDetection, drawDistanceLines);
            // Debug.Log("PLanted parasite at -> " + c.gameObject.name);
            for (int i = 0; i < detectTags.Count; i++)
            {
                if (i >= objectsPerTag.Count || objectsPerTag[i].Count == 0)
                {
                    tdp.Set(detectTags[i]);
                    Debug.Log("Set " + detectTags[i] + " with dynamic target lookup");
                    continue;
                }

                for (int y = 0; y < objectsPerTag[i].Count; y++)
                {
                    tdp.Set(detectTags[i], objectsPerTag[i][y]);
                    Debug.Log("Set " + detectTags[i] + " " + objectsPerTag[i][y]);
                }
            }
        }
    }

    private bool IsDetectTargetCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        for (int tagIndex = 0; tagIndex < objectsPerTag.Count; tagIndex++)
        {
            List<GameObject> taggedObjects = objectsPerTag[tagIndex];
            for (int objectIndex = 0; objectIndex < taggedObjects.Count; objectIndex++)
            {
                GameObject target = taggedObjects[objectIndex];
                if (target == null)
                {
                    continue;
                }

                Transform colliderTransform = collider.transform;
                Transform targetTransform = target.transform;
                if (colliderTransform == targetTransform ||
                    colliderTransform.IsChildOf(targetTransform) ||
                    targetTransform.IsChildOf(colliderTransform))
                {
                    return true;
                }
            }
        }

        return false;
    }


    // Update is called once per frame
    void Update()
    {

    }
}
