using System.Collections.Generic;
using UnityEngine;
using AdaptiveSystem.Models;
using AdaptiveSystem.Raw;

namespace AdaptiveSystem.Processing
{
    public static class FeatureExtractor
    {
        public static GlobalMetrics ComputeGlobalMetrics(
            IReadOnlyList<RawSample> samples,
            float completionTimeSeconds,
            int errorCount,
            int promptCount,
            float hesitationSpeedThreshold = 0.01f,
            float minHesitationSeconds = 0.25f)
        {
            var metrics = new GlobalMetrics
            {
                completion_time = completionTimeSeconds,
                error_count = errorCount,
                prompt_count = promptCount
            };

            if (samples == null || samples.Count < 2)
            {
                return metrics;
            }

            float totalTime = completionTimeSeconds;
            if (totalTime <= 0f)
            {
                totalTime = samples[samples.Count - 1].timestamp - samples[0].timestamp;
            }

            float pathLength = 0f;
            float peakSpeed = 0f;
            float speedSum = 0f;
            int speedCount = 0;
            float hesitationTotal = 0f;
            int hesitationCount = 0;
            float currentPause = 0f;

            for (int i = 1; i < samples.Count; i++)
            {
                var a = samples[i - 1];
                var b = samples[i];
                float dt = Mathf.Max(0.0001f, b.timestamp - a.timestamp);
                float dist = Vector3.Distance(a.position, b.position);
                float speed = dist / dt;

                pathLength += dist;
                peakSpeed = Mathf.Max(peakSpeed, speed);
                speedSum += speed;
                speedCount++;

                if (speed < hesitationSpeedThreshold)
                {
                    currentPause += dt;
                }
                else if (currentPause > 0f)
                {
                    if (currentPause >= minHesitationSeconds)
                    {
                        hesitationCount++;
                        hesitationTotal += currentPause;
                    }
                    currentPause = 0f;
                }
            }

            if (currentPause >= minHesitationSeconds)
            {
                hesitationCount++;
                hesitationTotal += currentPause;
            }

            float straightLine = Vector3.Distance(samples[0].position, samples[samples.Count - 1].position);
            metrics.path_efficiency = pathLength > 0f ? straightLine / pathLength : 0f;
            metrics.avg_speed = speedCount > 0 ? speedSum / speedCount : 0f;
            metrics.peak_speed = peakSpeed;
            metrics.hesitation_count = hesitationCount;
            metrics.hesitation_total_time = hesitationTotal;

            metrics.smoothness = EstimateSmoothness(samples);
            metrics.spatial_accuracy = 0f;

            return metrics;
        }

        private static float EstimateSmoothness(IReadOnlyList<RawSample> samples)
        {
            if (samples == null || samples.Count < 3)
            {
                return 0f;
            }

            float jerkSum = 0f;
            int jerkCount = 0;

            for (int i = 2; i < samples.Count; i++)
            {
                var p0 = samples[i - 2].position;
                var p1 = samples[i - 1].position;
                var p2 = samples[i].position;

                float v1 = (p1 - p0).magnitude;
                float v2 = (p2 - p1).magnitude;
                float jerk = Mathf.Abs(v2 - v1);

                jerkSum += jerk;
                jerkCount++;
            }

            return jerkCount > 0 ? jerkSum / jerkCount : 0f;
        }
    }
}
