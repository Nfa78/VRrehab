using System.Collections.Generic;
using SoundSystem;
using UnityEngine;

namespace TaskSystem
{
    /// <summary>
    /// Owns the raking-only leaf field. It is intentionally independent from
    /// the catch-leaf system so each task can use the interaction model it needs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RakeLeafFieldController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RakeLeavesTaskDriver taskDriver;
        [SerializeField] private Transform hoeTransform;
        [SerializeField] private Transform fallingLeafTemplatesRoot;

        [Header("Leaf Field")]
        [SerializeField, Min(1)] private int initialLeafCount = 12;
        [SerializeField] private Vector3 fieldCenter = new Vector3(-11.8f, 0.08f, 7.25f);
        [SerializeField] private Vector2 fieldSize = new Vector2(2.2f, 1.5f);
        [SerializeField, Min(0.1f)] private float leafVisualSpeed = 1.6f;
        [SerializeField, Range(0.1f, 1f)] private float rakeMovementMultiplier = 0.7f;
        [SerializeField, Min(0.1f)] private float maximumLeafSpeed = 1.1f;

        [Header("Pile")]
        [SerializeField] private Vector3 pileCenter = new Vector3(-11.8f, 0.08f, 7.85f);
        [SerializeField, Min(0.1f)] private float pileRadius = 0.38f;
        [SerializeField, Min(0f)] private float pileSpread = 0.24f;

        private readonly List<RakeableLeaf> spawnedLeaves = new List<RakeableLeaf>();
        private readonly List<GameObject> leafTemplates = new List<GameObject>();
        private readonly List<IgnoredCollision> ignoredHoeCollisions = new List<IgnoredCollision>();
        private SimManager simManager;
        private Transform runtimeRoot;
        private RakeSweepDetector sweepDetector;
        private bool fieldVisible;
        private int depositedLeafCount;

        public float LeafVisualSpeed => leafVisualSpeed;
        public bool CanAcceptRakeSweeps =>
            taskDriver != null && taskDriver.IsRakeLeavesStepActive() &&
            simManager != null && simManager.IsRunning && fieldVisible;

        public void Configure(RakeLeavesTaskDriver driver, Transform rakeTransform)
        {
            taskDriver = driver;
            hoeTransform = rakeTransform;
            EnsureSweepDetector();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureSweepDetector();
        }

        private void OnDisable()
        {
            SetFieldVisible(false);
        }

        private void Update()
        {
            ResolveReferences();
            bool rakeTaskActive = simManager != null && simManager.CurrentTask != null &&
                                  simManager.CurrentTask.TaskId == RakeLeavesTaskDriver.RakeTaskId;

            if (rakeTaskActive && !fieldVisible)
            {
                CreateOrResetField();
                IgnoreHoeObstacleCollisions();
                SetFieldVisible(true);
                return;
            }

            if (!rakeTaskActive && fieldVisible)
            {
                SetFieldVisible(false);
            }
        }

        public void ProcessSweep(Vector3 sweepCenter, Vector3 direction, float rakeSpeed, float width, float depth)
        {
            if (!CanAcceptRakeSweeps || Mathf.Abs(sweepCenter.y - fieldCenter.y) > 0.35f)
            {
                return;
            }

            float movementDistance = Mathf.Min(rakeSpeed, maximumLeafSpeed) *
                                     rakeMovementMultiplier * Time.fixedDeltaTime;
            EventSoundManager.TryPlayAt("garden.rake.scrape", sweepCenter);
            Vector3 sideways = new Vector3(-direction.z, 0f, direction.x);
            Bounds fieldBounds = GetFieldBounds();

            for (int i = 0; i < spawnedLeaves.Count; i++)
            {
                RakeableLeaf leaf = spawnedLeaves[i];
                if (leaf == null || leaf.IsInPile)
                {
                    continue;
                }

                Vector3 offset = leaf.PlannedPosition - sweepCenter;
                offset.y = 0f;
                float forwardDistance = Vector3.Dot(offset, direction);
                float sidewaysDistance = Mathf.Abs(Vector3.Dot(offset, sideways));
                if (Mathf.Abs(forwardDistance) > depth * 0.5f || sidewaysDistance > width * 0.5f)
                {
                    continue;
                }

                leaf.Rake(direction, movementDistance, fieldBounds);
                TryDepositLeaf(leaf);
            }
        }

        private void TryDepositLeaf(RakeableLeaf leaf)
        {
            Vector3 offset = leaf.PlannedPosition - pileCenter;
            offset.y = 0f;
            if (offset.sqrMagnitude > pileRadius * pileRadius)
            {
                return;
            }

            Vector2 randomOffset = Random.insideUnitCircle * pileSpread;
            Vector3 pilePosition = pileCenter + new Vector3(randomOffset.x, depositedLeafCount * 0.002f, randomOffset.y);
            leaf.MoveToPile(pilePosition);
            depositedLeafCount++;
            taskDriver.RakeLeaves(1f);
        }

        private void CreateOrResetField()
        {
            ClearRuntimeLeaves();
            CacheLeafTemplates();
            depositedLeafCount = 0;

            if (leafTemplates.Count == 0)
            {
                Debug.LogWarning("[RakeLeafFieldController] No leaf template was found; the rake field could not be created.", this);
                return;
            }

            EnsureRuntimeRoot();
            Bounds fieldBounds = GetFieldBounds();
            for (int i = 0; i < initialLeafCount; i++)
            {
                GameObject template = leafTemplates[i % leafTemplates.Count];
                GameObject leafObject = Instantiate(template, runtimeRoot);
                leafObject.name = $"RakeLeaf_{i + 1:00}";
                PrepareVisualOnlyLeaf(leafObject);

                Vector3 startPosition = new Vector3(
                    Random.Range(fieldBounds.min.x, fieldBounds.max.x),
                    fieldCenter.y,
                    Random.Range(fieldBounds.min.z, fieldBounds.max.z));
                RakeableLeaf leaf = leafObject.GetComponent<RakeableLeaf>() ?? leafObject.AddComponent<RakeableLeaf>();
                leaf.Initialize(this, startPosition);
                spawnedLeaves.Add(leaf);
            }
        }

        private void PrepareVisualOnlyLeaf(GameObject leafObject)
        {
            LeafsFallingEffect fallingEffect = leafObject.GetComponent<LeafsFallingEffect>();
            if (fallingEffect != null)
            {
                fallingEffect.enabled = false;
            }

            RigidBodyAndColliderCleanup(leafObject);

            Renderer[] renderers = leafObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                }
            }
        }

        private static void RigidBodyAndColliderCleanup(GameObject leafObject)
        {
            Rigidbody[] bodies = leafObject.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    bodies[i].isKinematic = true;
                    bodies[i].detectCollisions = false;
                }
            }

            Collider[] colliders = leafObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void CacheLeafTemplates()
        {
            if (leafTemplates.Count > 0)
            {
                return;
            }

            if (fallingLeafTemplatesRoot == null)
            {
                GameObject rootObject = GameObject.Find("Leafs");
                fallingLeafTemplatesRoot = rootObject != null ? rootObject.transform : null;
            }

            if (fallingLeafTemplatesRoot == null)
            {
                return;
            }

            LeafsFallingEffect[] effects = fallingLeafTemplatesRoot.GetComponentsInChildren<LeafsFallingEffect>(true);
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                {
                    leafTemplates.Add(effects[i].gameObject);
                }
            }
        }

        private void ResolveReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<RakeLeavesTaskDriver>();
            }

            if (simManager == null)
            {
                simManager = FindFirstObjectByType<SimManager>();
            }

            if (hoeTransform == null && taskDriver != null)
            {
                hoeTransform = taskDriver.HoeTransform;
            }
        }

        private void EnsureSweepDetector()
        {
            if (hoeTransform == null)
            {
                return;
            }

            sweepDetector = hoeTransform.GetComponent<RakeSweepDetector>();
            if (sweepDetector == null)
            {
                sweepDetector = hoeTransform.gameObject.AddComponent<RakeSweepDetector>();
            }

            sweepDetector.Configure(hoeTransform, this);
        }

        private Bounds GetFieldBounds()
        {
            return new Bounds(fieldCenter, new Vector3(fieldSize.x, 0.1f, fieldSize.y));
        }

        private void EnsureRuntimeRoot()
        {
            if (runtimeRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("Runtime Rake Leaves");
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;
        }

        private void SetFieldVisible(bool visible)
        {
            fieldVisible = visible;
            if (!visible)
            {
                RestoreHoeObstacleCollisions();
            }

            if (runtimeRoot != null)
            {
                runtimeRoot.gameObject.SetActive(visible);
            }
        }

        private void ClearRuntimeLeaves()
        {
            spawnedLeaves.Clear();
            if (runtimeRoot == null)
            {
                return;
            }

            for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(runtimeRoot.GetChild(i).gameObject);
            }
        }

        private void IgnoreHoeObstacleCollisions()
        {
            RestoreHoeObstacleCollisions();
            if (hoeTransform == null)
            {
                return;
            }

            Collider[] hoeColliders = hoeTransform.GetComponentsInChildren<Collider>(true);
            Vector3 halfExtents = new Vector3(fieldSize.x * 0.5f + 0.75f, 1.5f, fieldSize.y * 0.5f + 0.75f);
            Collider[] nearbyColliders = Physics.OverlapBox(fieldCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

            for (int hoeIndex = 0; hoeIndex < hoeColliders.Length; hoeIndex++)
            {
                Collider hoeCollider = hoeColliders[hoeIndex];
                if (hoeCollider == null)
                {
                    continue;
                }

                for (int obstacleIndex = 0; obstacleIndex < nearbyColliders.Length; obstacleIndex++)
                {
                    Collider obstacle = nearbyColliders[obstacleIndex];
                    if (obstacle == null || obstacle.attachedRigidbody != null ||
                        obstacle.transform.IsChildOf(hoeTransform))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(hoeCollider, obstacle, true);
                    ignoredHoeCollisions.Add(new IgnoredCollision(hoeCollider, obstacle));
                }
            }
        }

        private void RestoreHoeObstacleCollisions()
        {
            for (int i = 0; i < ignoredHoeCollisions.Count; i++)
            {
                IgnoredCollision pair = ignoredHoeCollisions[i];
                if (pair.HoeCollider != null && pair.ObstacleCollider != null)
                {
                    Physics.IgnoreCollision(pair.HoeCollider, pair.ObstacleCollider, false);
                }
            }

            ignoredHoeCollisions.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 0.25f, 0.25f);
            Gizmos.DrawCube(fieldCenter, new Vector3(fieldSize.x, 0.02f, fieldSize.y));
            Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(pileCenter, pileRadius);
        }

        private readonly struct IgnoredCollision
        {
            public IgnoredCollision(Collider hoeCollider, Collider obstacleCollider)
            {
                HoeCollider = hoeCollider;
                ObstacleCollider = obstacleCollider;
            }

            public Collider HoeCollider { get; }
            public Collider ObstacleCollider { get; }
        }
    }
}
