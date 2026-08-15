using System;
using UnityEngine;

namespace TaskSystem
{
    [Serializable]
    public class TrackedTaskObject
    {
        [SerializeField] private string objectId;
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody rigidbody;
        [SerializeField] private bool captureStartPose = true;
        [SerializeField] private bool resetVelocityOnReset = true;
        [SerializeField] private bool restoreParentOnReset = true;

        [NonSerialized] private bool _hasCapturedStartPose;
        [NonSerialized] private Vector3 _startPosition;
        [NonSerialized] private Quaternion _startRotation;
        [NonSerialized] private Vector3 _startLocalScale;
        [NonSerialized] private Transform _startParent;

        public string ObjectId => objectId;

        public Transform Target => target;

        public Rigidbody Rigidbody => rigidbody;

        public bool HasCapturedStartPose => _hasCapturedStartPose;

        public Vector3 StartPosition => _startPosition;

        public void CaptureStartState()
        {
            CaptureStartState(false);
        }

        public void CaptureStartState(bool forceRecapture)
        {
            if (!captureStartPose || target == null)
            {
                return;
            }

            if (_hasCapturedStartPose && !forceRecapture)
            {
                return;
            }

            if (rigidbody == null)
            {
                rigidbody = target.GetComponent<Rigidbody>();
            }

            _startPosition = target.position;
            _startRotation = target.rotation;
            _startLocalScale = target.localScale;
            _startParent = target.parent;

            _hasCapturedStartPose = true;
        }

        public void ResetToStartState()
        {
            if (target == null || !_hasCapturedStartPose)
            {
                return;
            }

            if (restoreParentOnReset)
            {
                target.SetParent(_startParent, true);
            }

            target.position = _startPosition;
            target.rotation = _startRotation;
            target.localScale = _startLocalScale;

            if (rigidbody == null)
            {
                rigidbody = target.GetComponent<Rigidbody>();
            }

            if (rigidbody == null || !resetVelocityOnReset)
            {
                return;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        public float DistanceFromStart()
        {
            if (target == null || !_hasCapturedStartPose)
            {
                return 0f;
            }

            return Vector3.Distance(target.position, _startPosition);
        }
    }
}
