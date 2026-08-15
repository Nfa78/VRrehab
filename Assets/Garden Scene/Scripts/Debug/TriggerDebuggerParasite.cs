using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TriggerDebuggerParasite : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField][Min(0f)] private float detectionRadius = 1.4f;
    [SerializeField] private bool useTriggerDetection = true;
    [SerializeField] private bool useDistanceDetection = true;
    [SerializeField] private float targetRefreshInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool drawDistanceLines = true;
    [SerializeField] private bool logRadiusEnterExit = true;
    [SerializeField] private bool logRadiusStay;
    [SerializeField] private float stayLogInterval = 0.5f;
    [SerializeField] private Color radiusGizmoColor = new Color(0.2f, 0.5f, 1f, 0.35f);

    private readonly List<string> detectTags = new List<string>();
    private readonly List<GameObject> targets = new List<GameObject>();
    private readonly List<LineRenderer> _lineRenderers = new List<LineRenderer>();
    private readonly List<bool> _targetInsideRadius = new List<bool>();
    private readonly List<float> _nextStayLogTimes = new List<float>();
    private float _nextTargetRefreshTime;

    private static readonly Dictionary<string, TaggedObjectCache> TaggedObjectCaches = new Dictionary<string, TaggedObjectCache>();

    private const float LineWidth = 0.03f;
    private static readonly Color LineColor = Color.blue;

    private sealed class TaggedObjectCache
    {
        public float NextRefreshTime;
        public GameObject[] Objects = Array.Empty<GameObject>();
    }

    public float DetectionRadius
    {
        get => detectionRadius;
        set => detectionRadius = Mathf.Max(0f, value);
    }

    void Start()
    {
        if (detectTags.Count == 0)
        {
            Debug.LogError("No init was called upon script spawn");
        }
    }

    public TriggerDebuggerParasite Configure(
        float radius,
        bool enableTriggerDetection,
        bool enableDistanceDetection,
        bool enableDistanceLines)
    {
        DetectionRadius = radius;
        useTriggerDetection = enableTriggerDetection;
        useDistanceDetection = enableDistanceDetection;
        drawDistanceLines = enableDistanceLines;
        return this;
    }

    public TriggerDebuggerParasite Set(string tag, GameObject _target = null)
    {
        RegisterTag(tag);

        if (_target != null)
        {
            AddTarget(tag, _target);
        }

        return this;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerDetection || other == null)
        {
            return;
        }

        if (IsDetectedTagObject(other.gameObject))
        {
            Debug.Log("[" + gameObject.name + "] " + other.name + " IS IN TRIGGER ");
        }
    }

    void Update()
    {
        if (!useDistanceDetection)
        {
            DisableAllLines();
            return;
        }

        RefreshTargetsByTag();

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i] == null)
            {
                RemoveTargetAt(i);
                continue;
            }

            Vector3 sourcePoint = transform.position;
            Vector3 targetPoint = targets[i].transform.position;
            float distance = Vector3.Distance(sourcePoint, targetPoint);
            bool isInsideRadius = distance <= detectionRadius;

            if (isInsideRadius)
            {
                HandleRadiusStayOrEnter(i, distance);

                if (drawDistanceLines)
                {
                    UpdateLine(i, sourcePoint, targetPoint);
                }
                else
                {
                    DisableLine(i);
                }
            }
            else
            {
                HandleRadiusExit(i, distance);
                DisableLine(i);
            }
        }
    }

    private void RegisterTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || detectTags.Contains(tag))
        {
            return;
        }

        detectTags.Add(tag);
    }

    private void AddTarget(string tag, GameObject targetObject)
    {
        if (targetObject == null || targetObject == gameObject || ContainsTarget(targetObject))
        {
            return;
        }

        targets.Add(targetObject);
        _lineRenderers.Add(null);
        _targetInsideRadius.Add(false);
        _nextStayLogTimes.Add(0f);
    }

    private bool ContainsTarget(GameObject targetObject)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == targetObject)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshTargetsByTag()
    {
        if (detectTags.Count == 0)
        {
            return;
        }

        if (targetRefreshInterval > 0f && Time.time < _nextTargetRefreshTime)
        {
            return;
        }

        _nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);

        for (int i = 0; i < detectTags.Count; i++)
        {
            string tag = detectTags[i];
            GameObject[] taggedObjects = FindGameObjectsWithTagSafe(tag);
            for (int targetIndex = 0; targetIndex < taggedObjects.Length; targetIndex++)
            {
                AddTarget(tag, taggedObjects[targetIndex]);
            }
        }
    }

    private GameObject[] FindGameObjectsWithTagSafe(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return Array.Empty<GameObject>();
        }

        if (!TaggedObjectCaches.TryGetValue(tag, out TaggedObjectCache cache))
        {
            cache = new TaggedObjectCache();
            TaggedObjectCaches.Add(tag, cache);
        }

        if (targetRefreshInterval > 0f && Time.time < cache.NextRefreshTime)
        {
            return cache.Objects;
        }

        try
        {
            cache.Objects = GameObject.FindGameObjectsWithTag(tag);
            cache.NextRefreshTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
            return cache.Objects;
        }
        catch (UnityException exception)
        {
            Debug.LogWarning($"[{gameObject.name}] Cannot find objects with tag '{tag}': {exception.Message}", this);
            cache.Objects = Array.Empty<GameObject>();
            cache.NextRefreshTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
            return Array.Empty<GameObject>();
        }
    }

    private bool IsDetectedTagObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        for (int i = 0; i < detectTags.Count; i++)
        {
            string tag = detectTags[i];
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (HasTag(candidate, tag))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasTag(GameObject candidate, string tag)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(candidate.tag, tag, StringComparison.Ordinal);
    }

    private void HandleRadiusStayOrEnter(int index, float distance)
    {
        if (!_targetInsideRadius[index])
        {
            _targetInsideRadius[index] = true;
            _nextStayLogTimes[index] = Time.time + stayLogInterval;

            if (logRadiusEnterExit)
            {
                Debug.Log(
                    $"[{gameObject.name}] {targets[index].name} ENTERED RADIUS distance={distance:0.###} radius={detectionRadius:0.###}",
                    this);
            }

            return;
        }

        if (!logRadiusStay || Time.time < _nextStayLogTimes[index])
        {
            return;
        }

        _nextStayLogTimes[index] = Time.time + stayLogInterval;
        Debug.Log(
            $"[{gameObject.name}] {targets[index].name} IS INSIDE RADIUS distance={distance:0.###} radius={detectionRadius:0.###}",
            this);
    }

    private void HandleRadiusExit(int index, float distance)
    {
        if (!_targetInsideRadius[index])
        {
            return;
        }

        _targetInsideRadius[index] = false;
        if (logRadiusEnterExit)
        {
            Debug.Log(
                $"[{gameObject.name}] {targets[index].name} EXITED RADIUS distance={distance:0.###} radius={detectionRadius:0.###}",
                this);
        }
    }

    private void UpdateLine(int index, Vector3 sourcePosition, Vector3 targetPosition)
    {
        LineRenderer lineRenderer = GetOrCreateLineRenderer(index);
        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, sourcePosition);
        lineRenderer.SetPosition(1, targetPosition);
    }

    private LineRenderer GetOrCreateLineRenderer(int index)
    {
        if (_lineRenderers[index] != null)
        {
            return _lineRenderers[index];
        }

        GameObject lineObject = new GameObject($"TriggerDebugLine_{index}");
        lineObject.transform.SetParent(transform, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = LineColor;
        lineRenderer.endColor = LineColor;
        lineRenderer.startWidth = LineWidth;
        lineRenderer.endWidth = LineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;

        _lineRenderers[index] = lineRenderer;
        return lineRenderer;
    }

    private void DisableLine(int index)
    {
        if (index < 0 || index >= _lineRenderers.Count || _lineRenderers[index] == null)
        {
            return;
        }

        _lineRenderers[index].enabled = false;
    }

    private void DisableAllLines()
    {
        for (int i = 0; i < _lineRenderers.Count; i++)
        {
            DisableLine(i);
        }
    }

    private void RemoveTargetAt(int index)
    {
        if (index < 0 || index >= targets.Count)
        {
            return;
        }

        if (_lineRenderers[index] != null)
        {
            Destroy(_lineRenderers[index].gameObject);
        }

        targets.RemoveAt(index);
        _lineRenderers.RemoveAt(index);
        _targetInsideRadius.RemoveAt(index);
        _nextStayLogTimes.RemoveAt(index);
    }

    private void OnDrawGizmosSelected()
    {
        if (detectionRadius <= 0f)
        {
            return;
        }

        Vector3 center = transform.position;

        Gizmos.color = radiusGizmoColor;
        Gizmos.DrawWireSphere(center, detectionRadius);
    }
}
