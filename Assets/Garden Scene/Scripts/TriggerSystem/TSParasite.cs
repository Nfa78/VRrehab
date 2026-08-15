using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TriggerSystem
{
    [Serializable]
    public sealed class TSRadiusGameObjectEvent : UnityEvent<GameObject> { }

    [DisallowMultipleComponent]
    public sealed class TSParasite : MonoBehaviour
    {
        private const string DefaultDetectTag = "HandAnchor";

        [Header("Detection")]
        [SerializeField][Min(0f)] private float detectionRadius = 1.4f;
        [SerializeField][Min(0.05f)] private float targetRefreshInterval = 1.5f;
        [SerializeField] private List<string> detectTags = new List<string> { DefaultDetectTag };

        [Header("Events")]
        [SerializeField] private TSRadiusGameObjectEvent onRadiusEnter = new TSRadiusGameObjectEvent();
        [SerializeField] private TSRadiusGameObjectEvent onRadiusExit = new TSRadiusGameObjectEvent();

        [Header("Legacy Events")]
        [SerializeField] private UnityEvent<GameObject> onTargetEntered;
        [SerializeField] private UnityEvent<GameObject> onTargetExited;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRadius = true;
        [SerializeField] private bool drawDebugRadiusOnlyWhenSelected = true;
        [SerializeField] private bool drawDebugTargetLines = true;
        [SerializeField] private Color debugRadiusColor = new Color(0.05f, 0.45f, 1f, 0.45f);
        [SerializeField] private Color debugActiveRadiusColor = new Color(0f, 0.9f, 1f, 0.8f);
        [SerializeField] private Color debugTargetLineColor = new Color(0f, 0.9f, 1f, 0.8f);

        private readonly List<GameObject> targets = new List<GameObject>();
        private readonly List<bool> targetInsideRadius = new List<bool>();
        private readonly List<GameObject> targetsInsideRadius = new List<GameObject>();

        private float nextTargetRefreshTime;
        private GameObject closestTarget;
        private float closestDistance = Mathf.Infinity;

        private static readonly Dictionary<string, TaggedObjectCache> TaggedObjectCaches =
            new Dictionary<string, TaggedObjectCache>();

        public event Action<GameObject> TargetEntered;
        public event Action<GameObject> TargetExited;
        public event Action<GameObject> RadiusEntered;
        public event Action<GameObject> RadiusExited;

        public float DetectionRadius
        {
            get => detectionRadius;
            set => detectionRadius = Mathf.Max(0f, value);
        }

        public float TargetRefreshInterval
        {
            get => targetRefreshInterval;
            set => targetRefreshInterval = Mathf.Max(0.05f, value);
        }

        public bool HasTargetInsideRadius => targetsInsideRadius.Count > 0;
        public GameObject ClosestTarget => closestTarget;
        public float ClosestDistance => closestDistance;
        public IReadOnlyList<GameObject> TargetsInsideRadius => targetsInsideRadius;
        public TSRadiusGameObjectEvent OnRadiusEnter => onRadiusEnter;
        public TSRadiusGameObjectEvent OnRadiusExit => onRadiusExit;
        public TSRadiusGameObjectEvent OnRadiusEnterGameObject => onRadiusEnter;
        public TSRadiusGameObjectEvent OnRadiusExitGameObject => onRadiusExit;
        public bool DrawDebugRadius
        {
            get => drawDebugRadius;
            set => drawDebugRadius = value;
        }

        private void Awake()
        {
            EnsureDetectTags();
        }

        private void OnEnable()
        {
            RefreshTargetsByTag(true);
        }

        private void OnDisable()
        {
            ClearInsideState();
        }

        private void Update()
        {
            RefreshTargetsByTag(false);
            UpdateRadiusState();
        }

        public TSParasite Configure(
            IEnumerable<string> tags,
            float radius,
            float refreshInterval)
        {
            DetectionRadius = radius;
            TargetRefreshInterval = refreshInterval;

            detectTags.Clear();
            if (tags != null)
            {
                foreach (string tag in tags)
                {
                    RegisterTag(tag);
                }
            }

            EnsureDetectTags();
            ClearTargets();
            RefreshTargetsByTag(true);
            return this;
        }

        public TSParasite Set(string tag, GameObject target = null)
        {
            RegisterTag(tag);

            if (target != null)
            {
                AddTarget(target);
            }

            return this;
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

        private void RegisterTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || detectTags.Contains(tag))
            {
                return;
            }

            detectTags.Add(tag);
        }

        private void RefreshTargetsByTag(bool force)
        {
            if (!force && Time.time < nextTargetRefreshTime)
            {
                return;
            }

            nextTargetRefreshTime = Time.time + targetRefreshInterval;

            for (int i = 0; i < detectTags.Count; i++)
            {
                GameObject[] taggedObjects = FindGameObjectsWithTagSafe(detectTags[i], force);
                for (int targetIndex = 0; targetIndex < taggedObjects.Length; targetIndex++)
                {
                    AddTarget(taggedObjects[targetIndex]);
                }
            }
        }

        private GameObject[] FindGameObjectsWithTagSafe(string tag, bool force)
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

            if (!force && Time.time < cache.NextRefreshTime)
            {
                return cache.Objects;
            }

            try
            {
                cache.Objects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                cache.Objects = Array.Empty<GameObject>();
            }

            cache.NextRefreshTime = Time.time + targetRefreshInterval;
            return cache.Objects;
        }

        private void AddTarget(GameObject target)
        {
            if (target == null || target == gameObject || ContainsTarget(target))
            {
                return;
            }

            targets.Add(target);
            targetInsideRadius.Add(false);
        }

        private bool ContainsTarget(GameObject target)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateRadiusState()
        {
            closestTarget = null;
            closestDistance = Mathf.Infinity;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                GameObject target = targets[i];
                if (target == null)
                {
                    RemoveTargetAt(i);
                    continue;
                }

                float distance = Vector3.Distance(transform.position, target.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }

                SetTargetInsideRadius(i, distance <= detectionRadius);
            }
        }

        private void SetTargetInsideRadius(int index, bool isInside)
        {
            if (targetInsideRadius[index] == isInside)
            {
                return;
            }

            targetInsideRadius[index] = isInside;
            GameObject target = targets[index];

            if (isInside)
            {
                targetsInsideRadius.Add(target);
                RaiseRadiusEntered(target);
                return;
            }

            targetsInsideRadius.Remove(target);
            RaiseRadiusExited(target);
        }

        private void ClearTargets()
        {
            ClearInsideState();
            targets.Clear();
            targetInsideRadius.Clear();
            closestTarget = null;
            closestDistance = Mathf.Infinity;
        }

        private void ClearInsideState()
        {
            for (int i = targetsInsideRadius.Count - 1; i >= 0; i--)
            {
                GameObject target = targetsInsideRadius[i];
                if (target != null)
                {
                    RaiseRadiusExited(target);
                }
            }

            targetsInsideRadius.Clear();
            for (int i = 0; i < targetInsideRadius.Count; i++)
            {
                targetInsideRadius[i] = false;
            }
        }

        private void RemoveTargetAt(int index)
        {
            if (index < 0 || index >= targets.Count)
            {
                return;
            }

            GameObject target = targets[index];
            if (target != null && targetInsideRadius[index])
            {
                targetsInsideRadius.Remove(target);
                RaiseRadiusExited(target);
            }

            targets.RemoveAt(index);
            targetInsideRadius.RemoveAt(index);
        }

        private void RaiseRadiusEntered(GameObject target)
        {
            TargetEntered?.Invoke(target);
            RadiusEntered?.Invoke(target);
            onTargetEntered?.Invoke(target);
            onRadiusEnter?.Invoke(target);
        }

        private void RaiseRadiusExited(GameObject target)
        {
            TargetExited?.Invoke(target);
            RadiusExited?.Invoke(target);
            onTargetExited?.Invoke(target);
            onRadiusExit?.Invoke(target);
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugRadius || drawDebugRadiusOnlyWhenSelected)
            {
                return;
            }

            DrawRadiusGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugRadius || !drawDebugRadiusOnlyWhenSelected)
            {
                return;
            }

            DrawRadiusGizmos();
        }

        private void DrawRadiusGizmos()
        {
            Color previousColor = Gizmos.color;

            Color radiusColor = Application.isPlaying && HasTargetInsideRadius
                ? debugActiveRadiusColor
                : debugRadiusColor;

            Vector3 center = transform.position;

            Gizmos.color = radiusColor;
            Gizmos.DrawWireSphere(center, detectionRadius);

            if (Application.isPlaying && drawDebugTargetLines)
            {
                DrawTargetLineGizmos(center);
            }

            Gizmos.color = previousColor;
        }

        private void DrawTargetLineGizmos(Vector3 sourceCenter)
        {
            Gizmos.color = debugTargetLineColor;

            for (int i = 0; i < targetsInsideRadius.Count; i++)
            {
                GameObject target = targetsInsideRadius[i];
                if (target != null)
                {
                    Gizmos.DrawLine(sourceCenter, target.transform.position);
                }
            }
        }

        private sealed class TaggedObjectCache
        {
            public float NextRefreshTime;
            public GameObject[] Objects = Array.Empty<GameObject>();
        }
    }
}
