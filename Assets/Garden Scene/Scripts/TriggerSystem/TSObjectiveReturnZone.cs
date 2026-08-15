using System;
using System.Collections.Generic;
using TaskSystem;
using UnityEngine;

namespace TriggerSystem
{
    [DisallowMultipleComponent]
    public sealed class TSObjectiveReturnZone : MonoBehaviour
    {
        public const string WaterCanTag = "WaterCan";
        public const string BucketTag = "Bucket";

        [Header("Trigger System")]
        [SerializeField] private TSParasite triggerRadius;
        [SerializeField] private List<string> acceptedObjectTags = new List<string> { WaterCanTag };
        [SerializeField][Min(0f)] private float detectionRadius = 0.35f;
        [SerializeField][Min(0.05f)] private float targetRefreshInterval = 0.25f;

        [Header("Objective")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private SimObjectiveInteraction objectiveInteraction;
        [SerializeField] private SimManagerObjectiveCommandService objectiveCommands;
        [SerializeField] private string objectiveId;
        [SerializeField] private bool completeWhenObjectInside = true;
        [SerializeField][Min(0.05f)] private float insideRetryInterval = 0.25f;
        [SerializeField] private bool logDebug = true;

        [Header("Return Action")]
        [SerializeField] private bool requireObjectiveCurrent = true;
        [SerializeField] private bool snapObjectOnReturn = true;
        [SerializeField] private bool smoothReturnOnSnap = true;
        [SerializeField][Min(0.01f)] private float smoothReturnDuration = 0.35f;
        [SerializeField][Min(0f)] private float smoothReturnArcHeight = 0.08f;
        [SerializeField] private bool useCapturedInitialObjectPoseForSnap = true;
        [SerializeField] private Transform snapAnchor;
        [SerializeField] private bool snapRotation = true;
        [SerializeField] private bool useInitialZoneRotationForSnap = true;
        [SerializeField] private bool disableInteractablesOnReturn = true;
        [SerializeField] private bool makeRigidbodyKinematicOnReturn = true;
        [SerializeField] private bool clearRigidbodyVelocityOnReturn = true;
        [SerializeField] private List<string> interactableTypeNames = new List<string>
        {
            "Oculus.Interaction.GrabInteractable",
            "Oculus.Interaction.HandGrab.HandGrabInteractable",
            "Oculus.Interaction.Grabbable",
            "Oculus.Interaction.PhysicsGrabbable",
            "UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable",
            "UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable"
        };

        [Header("Hint Animation")]
        [SerializeField] private bool animateHint = true;
        [SerializeField] private bool animateOnlyWhenObjectiveCurrent = true;
        [SerializeField] private float rotationSpeedDegrees = 45f;
        [SerializeField][Min(0f)] private float scalePulseAmplitude = 0.12f;
        [SerializeField][Min(0.01f)] private float scalePulseSpeed = 0.8f;

        private float _nextInsideCheckTime;
        private bool _subscribed;
        private Vector3 _initialLocalScale;
        private Quaternion _initialLocalRotation;
        private Quaternion _initialWorldRotation;
        private readonly HashSet<int> _returnedObjectIds = new HashSet<int>();
        private readonly Dictionary<int, Pose> _capturedInitialObjectPoses = new Dictionary<int, Pose>();
        private readonly Dictionary<int, ActiveReturnAnimation> _activeReturnAnimations = new Dictionary<int, ActiveReturnAnimation>();

        public string ObjectiveId => objectiveInteraction != null ? objectiveInteraction.ObjectiveId : objectiveId;
        public IReadOnlyList<string> AcceptedObjectTags => acceptedObjectTags;
        public TSParasite TriggerRadius => triggerRadius;

        private void Awake()
        {
            CacheInitialTransform();
            ResolveReferences();
            ConfigureTriggerRadius();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ConfigureTriggerRadius();
            Subscribe();
        }

        private void OnDisable()
        {
            CompleteAndClearActiveReturnAnimations();
            Unsubscribe();
        }

        private void Update()
        {
            UpdateHintAnimation();

            if (!completeWhenObjectInside || Time.time < _nextInsideCheckTime)
            {
                return;
            }

            _nextInsideCheckTime = Time.time + insideRetryInterval;
            TryCompleteForInsideTargets();
        }

        [ContextMenu("Configure TS Radius")]
        public void ConfigureTriggerRadius()
        {
            ResolveReferences();
            EnsureAcceptedObjectTags();

            if (triggerRadius == null)
            {
                return;
            }

            triggerRadius.Configure(acceptedObjectTags, detectionRadius, targetRefreshInterval);
        }

        [ContextMenu("Accept WaterCan Only")]
        private void AcceptWaterCanOnly()
        {
            SetSingleAcceptedTag(WaterCanTag);
        }

        [ContextMenu("Accept Bucket Only")]
        private void AcceptBucketOnly()
        {
            SetSingleAcceptedTag(BucketTag);
        }

        private void ResolveReferences()
        {
            if (triggerRadius == null)
            {
                triggerRadius = GetComponent<TSParasite>();
            }

            if (triggerRadius == null)
            {
                triggerRadius = gameObject.AddComponent<TSParasite>();
            }

            if (objectiveInteraction == null)
            {
                objectiveInteraction = GetComponent<SimObjectiveInteraction>();
            }

            if (simManager == null)
            {
                simManager = FindFirstObjectByType<SimManager>();
            }

            if (objectiveCommands == null)
            {
                objectiveCommands = FindFirstObjectByType<SimManagerObjectiveCommandService>();
            }

            if (objectiveCommands == null)
            {
                if (simManager != null)
                {
                    objectiveCommands = simManager.GetComponent<SimManagerObjectiveCommandService>();
                    if (objectiveCommands == null)
                    {
                        objectiveCommands = simManager.gameObject.AddComponent<SimManagerObjectiveCommandService>();
                    }
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed || triggerRadius == null)
            {
                return;
            }

            triggerRadius.RadiusEntered += HandleRadiusEntered;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || triggerRadius == null)
            {
                return;
            }

            triggerRadius.RadiusEntered -= HandleRadiusEntered;
            _subscribed = false;
        }

        private void HandleRadiusEntered(GameObject target)
        {
            if (HasAcceptedTag(target))
            {
                TryComplete(target);
            }
        }

        private void TryCompleteForInsideTargets()
        {
            if (triggerRadius == null || !triggerRadius.HasTargetInsideRadius)
            {
                return;
            }

            IReadOnlyList<GameObject> insideTargets = triggerRadius.TargetsInsideRadius;
            for (int i = 0; i < insideTargets.Count; i++)
            {
                GameObject target = insideTargets[i];
                if (HasAcceptedTag(target) && TryComplete(target))
                {
                    return;
                }
            }
        }

        private bool TryComplete(GameObject target)
        {
            GameObject returnedObject = ResolveAcceptedTagRoot(target);
            if (returnedObject == null)
            {
                return false;
            }

            bool objectiveCurrent = !requireObjectiveCurrent || IsObjectiveCurrent();
            if (!objectiveCurrent)
            {
                CaptureInitialObjectPoseIfMissing(returnedObject);
                return false;
            }

            if (!CanRunReturnAction(returnedObject))
            {
                return false;
            }

            bool completed = false;
            if (simManager != null && simManager.CurrentTaskDriver is SimTaskDriver taskDriver)
            {
                completed = taskDriver.TryCompleteStep(ObjectiveId);
            }

            if (!completed && objectiveInteraction != null)
            {
                completed = objectiveInteraction.CompleteObjective();
            }
            else if (!completed && ResolveObjectiveCommands())
            {
                completed = objectiveCommands.CompleteCurrentObjective(objectiveId);
            }

            if (logDebug)
            {
                Debug.Log(
                    $"[TSObjectiveReturnZone] {name} detected {Describe(returnedObject)} for objective '{ObjectiveId}'. CompleteObjective {(completed ? "succeeded" : "was rejected")}.",
                    this);
            }

            if (completed)
            {
                ApplyReturnAction(returnedObject);
            }

            return completed;
        }

        private bool CanRunReturnAction(GameObject returnedObject)
        {
            if (returnedObject == null)
            {
                return false;
            }

            if (_returnedObjectIds.Contains(returnedObject.GetInstanceID()))
            {
                return false;
            }

            return true;
        }

        private bool IsObjectiveCurrent()
        {
            string id = ObjectiveId;
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            ResolveReferences();
            return simManager != null &&
                   simManager.CurrentObjective != null &&
                   string.Equals(simManager.CurrentObjective.ObjectiveId, id, StringComparison.Ordinal);
        }

        private void ApplyReturnAction(GameObject returnedObject)
        {
            if (returnedObject == null)
            {
                return;
            }

            _returnedObjectIds.Add(returnedObject.GetInstanceID());

            if (disableInteractablesOnReturn)
            {
                DisableInteractableComponents(returnedObject);
            }

            Rigidbody returnedRigidbody = returnedObject.GetComponent<Rigidbody>();
            if (returnedRigidbody == null)
            {
                returnedRigidbody = returnedObject.GetComponentInChildren<Rigidbody>(true);
            }

            if (returnedRigidbody != null)
            {
                if (clearRigidbodyVelocityOnReturn)
                {
                    returnedRigidbody.linearVelocity = Vector3.zero;
                    returnedRigidbody.angularVelocity = Vector3.zero;
                }

                if (makeRigidbodyKinematicOnReturn)
                {
                    returnedRigidbody.isKinematic = true;
                }
            }

            if (snapObjectOnReturn)
            {
                SnapReturnedObject(returnedObject);
            }
        }

        private void SnapReturnedObject(GameObject returnedObject)
        {
            Transform targetTransform = returnedObject.transform;
            Pose snapPose = ResolveSnapPose(returnedObject);

            if (!smoothReturnOnSnap || smoothReturnDuration <= 0.01f)
            {
                ApplyPose(targetTransform, snapPose);
            }
            else
            {
                StartSmoothReturn(returnedObject, snapPose);
            }
        }

        private void StartSmoothReturn(GameObject returnedObject, Pose snapPose)
        {
            if (returnedObject == null)
            {
                return;
            }

            int instanceId = returnedObject.GetInstanceID();
            CompleteReturnAnimation(instanceId);

            ActiveReturnAnimation state = new ActiveReturnAnimation
            {
                TargetTransform = returnedObject.transform,
                TargetPose = snapPose
            };

            state.Coroutine = StartCoroutine(SmoothReturnRoutine(instanceId, state));
            _activeReturnAnimations[instanceId] = state;
        }

        private System.Collections.IEnumerator SmoothReturnRoutine(int instanceId, ActiveReturnAnimation state)
        {
            Transform targetTransform = state.TargetTransform;
            if (targetTransform == null)
            {
                _activeReturnAnimations.Remove(instanceId);
                yield break;
            }

            Vector3 startPosition = targetTransform.position;
            Quaternion startRotation = targetTransform.rotation;
            float duration = Mathf.Max(0.01f, smoothReturnDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (targetTransform == null)
                {
                    _activeReturnAnimations.Remove(instanceId);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

                Vector3 position = Vector3.Lerp(startPosition, state.TargetPose.position, easedTime);
                position.y += Mathf.Sin(normalizedTime * Mathf.PI) * smoothReturnArcHeight;

                Quaternion rotation = snapRotation
                    ? Quaternion.Slerp(startRotation, state.TargetPose.rotation, easedTime)
                    : targetTransform.rotation;

                ApplyPose(targetTransform, new Pose(position, rotation));
                yield return null;
            }

            ApplyPose(targetTransform, state.TargetPose);
            _activeReturnAnimations.Remove(instanceId);
        }

        private void CompleteAndClearActiveReturnAnimations()
        {
            if (_activeReturnAnimations.Count == 0)
            {
                return;
            }

            List<int> activeIds = new List<int>(_activeReturnAnimations.Keys);
            for (int i = 0; i < activeIds.Count; i++)
            {
                CompleteReturnAnimation(activeIds[i]);
            }
        }

        private void CompleteReturnAnimation(int instanceId)
        {
            if (!_activeReturnAnimations.TryGetValue(instanceId, out ActiveReturnAnimation state))
            {
                return;
            }

            if (state.Coroutine != null)
            {
                StopCoroutine(state.Coroutine);
            }

            if (state.TargetTransform != null)
            {
                ApplyPose(state.TargetTransform, state.TargetPose);
            }

            _activeReturnAnimations.Remove(instanceId);
        }

        private void ApplyPose(Transform targetTransform, Pose targetPose)
        {
            if (targetTransform == null)
            {
                return;
            }

            if (snapRotation)
            {
                targetTransform.SetPositionAndRotation(targetPose.position, targetPose.rotation);
            }
            else
            {
                targetTransform.position = targetPose.position;
            }
        }

        private Pose ResolveSnapPose(GameObject returnedObject)
        {
            if (snapAnchor != null)
            {
                return new Pose(snapAnchor.position, snapAnchor.rotation);
            }

            if (useCapturedInitialObjectPoseForSnap &&
                returnedObject != null &&
                _capturedInitialObjectPoses.TryGetValue(returnedObject.GetInstanceID(), out Pose capturedPose))
            {
                return capturedPose;
            }

            return new Pose(transform.position, ResolveSnapRotation());
        }

        private Quaternion ResolveSnapRotation()
        {
            if (snapAnchor != null)
            {
                return snapAnchor.rotation;
            }

            return useInitialZoneRotationForSnap ? _initialWorldRotation : transform.rotation;
        }

        private void DisableInteractableComponents(GameObject returnedObject)
        {
            Behaviour[] behaviours = returnedObject.GetComponentsInChildren<Behaviour>(true);
            int disabledCount = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled || !IsConfiguredInteractable(behaviour))
                {
                    continue;
                }

                behaviour.enabled = false;
                disabledCount++;
            }

            if (logDebug)
            {
                Debug.Log(
                    $"[TSObjectiveReturnZone] {name} disabled {disabledCount} interactable component(s) on {returnedObject.name}.",
                    this);
            }
        }

        private bool IsConfiguredInteractable(Behaviour behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }

            if (interactableTypeNames == null || interactableTypeNames.Count == 0)
            {
                return false;
            }

            Type type = behaviour.GetType();
            string fullName = type.FullName;
            string name = type.Name;

            for (int i = 0; i < interactableTypeNames.Count; i++)
            {
                string configuredName = interactableTypeNames[i];
                if (string.IsNullOrWhiteSpace(configuredName))
                {
                    continue;
                }

                if (string.Equals(fullName, configuredName, StringComparison.Ordinal) ||
                    string.Equals(name, configuredName, StringComparison.Ordinal) ||
                    fullName != null && fullName.EndsWith("." + configuredName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateHintAnimation()
        {
            if (!animateHint)
            {
                ResetHintTransform();
                return;
            }

            if (animateOnlyWhenObjectiveCurrent && !IsObjectiveCurrent())
            {
                ResetHintTransform();
                return;
            }

            float rotationY = Time.time * rotationSpeedDegrees;
            float pulse = 1f + Mathf.Sin(Time.time * scalePulseSpeed * Mathf.PI * 2f) * scalePulseAmplitude;
            transform.localRotation = _initialLocalRotation * Quaternion.Euler(0f, rotationY, 0f);
            transform.localScale = _initialLocalScale * Mathf.Max(0.01f, pulse);
        }

        private void ResetHintTransform()
        {
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        private void CacheInitialTransform()
        {
            _initialLocalScale = transform.localScale;
            _initialLocalRotation = transform.localRotation;
            _initialWorldRotation = transform.rotation;
        }

        private void CaptureInitialObjectPoseIfMissing(GameObject returnedObject)
        {
            if (!useCapturedInitialObjectPoseForSnap || returnedObject == null)
            {
                return;
            }

            int instanceId = returnedObject.GetInstanceID();
            if (_capturedInitialObjectPoses.ContainsKey(instanceId))
            {
                return;
            }

            Transform targetTransform = returnedObject.transform;
            _capturedInitialObjectPoses.Add(
                instanceId,
                new Pose(targetTransform.position, targetTransform.rotation));
        }

        private bool ResolveObjectiveCommands()
        {
            if (objectiveCommands != null)
            {
                return true;
            }

            ResolveReferences();
            return objectiveCommands != null;
        }

        private bool HasAcceptedTag(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            EnsureAcceptedObjectTags();
            for (Transform cursor = target.transform; cursor != null; cursor = cursor.parent)
            {
                for (int i = 0; i < acceptedObjectTags.Count; i++)
                {
                    string acceptedTag = acceptedObjectTags[i];
                    if (!string.IsNullOrWhiteSpace(acceptedTag) &&
                        string.Equals(cursor.gameObject.tag, acceptedTag, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private GameObject ResolveAcceptedTagRoot(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            EnsureAcceptedObjectTags();
            for (Transform cursor = target.transform; cursor != null; cursor = cursor.parent)
            {
                for (int i = 0; i < acceptedObjectTags.Count; i++)
                {
                    string acceptedTag = acceptedObjectTags[i];
                    if (!string.IsNullOrWhiteSpace(acceptedTag) &&
                        string.Equals(cursor.gameObject.tag, acceptedTag, StringComparison.Ordinal))
                    {
                        return cursor.gameObject;
                    }
                }
            }

            return null;
        }

        private void EnsureAcceptedObjectTags()
        {
            if (acceptedObjectTags == null)
            {
                acceptedObjectTags = new List<string>();
            }

            for (int i = acceptedObjectTags.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(acceptedObjectTags[i]))
                {
                    acceptedObjectTags.RemoveAt(i);
                }
            }

            if (acceptedObjectTags.Count == 0)
            {
                acceptedObjectTags.Add(WaterCanTag);
            }
        }

        private void SetSingleAcceptedTag(string tag)
        {
            if (acceptedObjectTags == null)
            {
                acceptedObjectTags = new List<string>();
            }

            acceptedObjectTags.Clear();
            acceptedObjectTags.Add(tag);
            ConfigureTriggerRadius();
        }

        private static string Describe(GameObject target)
        {
            if (target == null)
            {
                return "null";
            }

            return $"{target.name} tag={target.tag}";
        }

        private sealed class ActiveReturnAnimation
        {
            public Transform TargetTransform;
            public Pose TargetPose;
            public Coroutine Coroutine;
        }
    }
}
