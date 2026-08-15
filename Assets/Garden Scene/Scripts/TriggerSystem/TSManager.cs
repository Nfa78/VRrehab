using System;
using System.Collections.Generic;
using UnityEngine;

namespace TriggerSystem
{
    public sealed class TSManager : MonoBehaviour
    {
        private const string DefaultDetectTag = "HandAnchor";

        [SerializeField] private List<string> detectTags = new List<string> { DefaultDetectTag };
        [SerializeField][Min(0f)] private float detectionRadius = 1.4f;
        [SerializeField][Min(0.05f)] private float targetRefreshInterval = 1.5f;
        [SerializeField] private bool plantOnStart = true;
        [SerializeField] private bool skipDetectTagObjects = true;

        private void Start()
        {
            if (plantOnStart)
            {
                PlantParasites();
            }
        }

        public void PlantParasites()
        {
            EnsureDetectTags();

            Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];
                if (targetCollider == null)
                {
                    continue;
                }

                if (skipDetectTagObjects && HasDetectTagInParents(targetCollider.transform))
                {
                    continue;
                }

                TSParasite parasite =
                    targetCollider.GetComponent<TSParasite>() ??
                    targetCollider.gameObject.AddComponent<TSParasite>();

                parasite.Configure(detectTags, detectionRadius, targetRefreshInterval);
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

        private bool HasDetectTagInParents(Transform current)
        {
            while (current != null)
            {
                if (HasDetectTag(current.gameObject))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool HasDetectTag(GameObject candidate)
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

                if (string.Equals(candidate.tag, tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
