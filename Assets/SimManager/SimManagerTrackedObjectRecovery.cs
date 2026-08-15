using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace TaskSystem
{
    internal sealed class SimManagerTrackedObjectRecovery
    {
        private readonly Dictionary<string, float> _lastObjectFallResetById = new Dictionary<string, float>();

        public void Clear()
        {
            _lastObjectFallResetById.Clear();
        }

        public void HandleFalls(
            SimTask task,
            float currentClock,
            float fallDistanceBelowStart,
            float objectFallResetCooldown,
            bool failAndRetryActiveObjectiveOnObjectFall,
            Action<string> logDebug,
            Func<bool> retryTaskFromMilestone)
        {
            if (task == null)
            {
                return;
            }

            IReadOnlyList<TrackedTaskObject> trackedObjects = task.TrackedObjects;
            for (int index = 0; index < trackedObjects.Count; index++)
            {
                TrackedTaskObject tracked = trackedObjects[index];
                if (tracked == null || tracked.Target == null)
                {
                    continue;
                }

                if (!tracked.HasCapturedStartPose)
                {
                    tracked.CaptureStartState();
                }

                if (!tracked.HasCapturedStartPose)
                {
                    continue;
                }

                if (IsHeldOrPhysicsLocked(tracked))
                {
                    continue;
                }

                float fallThresholdY = tracked.StartPosition.y - Mathf.Abs(fallDistanceBelowStart);
                if (tracked.Target.position.y > fallThresholdY)
                {
                    continue;
                }

                string trackedId = string.IsNullOrWhiteSpace(tracked.ObjectId) ? tracked.Target.name : tracked.ObjectId;
                if (_lastObjectFallResetById.TryGetValue(trackedId, out float lastResetClock) &&
                    currentClock - lastResetClock < objectFallResetCooldown)
                {
                    continue;
                }

                _lastObjectFallResetById[trackedId] = currentClock;
                tracked.ResetToStartState();
                logDebug?.Invoke($"Tracked object fell and was reset: {trackedId}. ThresholdY={fallThresholdY:0.###}");

                if (!failAndRetryActiveObjectiveOnObjectFall || task.CurrentObjective == null)
                {
                    continue;
                }

                bool restarted = retryTaskFromMilestone != null && retryTaskFromMilestone();
                if (restarted)
                {
                    logDebug?.Invoke(
                        $"Task restarted from milestone due to object falling. RestartStep={GetObjectiveLabel(task.CurrentObjective)} Reason=ObjectFell.");
                }
            }
        }

        private static string GetObjectiveLabel(SimTaskObjective objective)
        {
            if (objective == null)
            {
                return "-";
            }

            return string.IsNullOrWhiteSpace(objective.Title)
                ? objective.ObjectiveId
                : $"{objective.Title} [{objective.ObjectiveId}]";
        }

        private static bool IsHeldOrPhysicsLocked(TrackedTaskObject tracked)
        {
            if (tracked == null || tracked.Target == null)
            {
                return false;
            }

            Grabbable grabbable =
                tracked.Target.GetComponent<Grabbable>() ??
                tracked.Target.GetComponentInParent<Grabbable>() ??
                tracked.Target.GetComponentInChildren<Grabbable>(true);

            if (grabbable != null && grabbable.SelectingPointsCount > 0)
            {
                return true;
            }

            Rigidbody trackedRigidbody = tracked.Rigidbody;
            if (trackedRigidbody == null)
            {
                trackedRigidbody = tracked.Target.GetComponent<Rigidbody>();
            }

            return trackedRigidbody != null && trackedRigidbody.isKinematic;
        }
    }
}
