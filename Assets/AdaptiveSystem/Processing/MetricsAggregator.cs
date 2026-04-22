using System.Collections.Generic;
using AdaptiveSystem.Models;
using AdaptiveSystem.Raw;

namespace AdaptiveSystem.Processing
{
    public static class MetricsAggregator
    {
        public static GlobalMetrics Aggregate(
            IReadOnlyList<RawSample> rawSamples,
            float completionTimeSeconds,
            int errorCount,
            int promptCount,
            int downsampleStep = 2,
            int smoothWindow = 5)
        {
            var downsampled = TrajectoryPreprocessor.Downsample(rawSamples, downsampleStep);
            var smoothed = TrajectoryPreprocessor.SmoothMovingAverage(downsampled, smoothWindow);

            return FeatureExtractor.ComputeGlobalMetrics(
                smoothed,
                completionTimeSeconds,
                errorCount,
                promptCount);
        }
    }
}
