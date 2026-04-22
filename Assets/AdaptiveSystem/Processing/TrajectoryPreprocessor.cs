using System.Collections.Generic;
using UnityEngine;
using AdaptiveSystem.Raw;

namespace AdaptiveSystem.Processing
{
    public static class TrajectoryPreprocessor
    {
        public static List<RawSample> Downsample(IReadOnlyList<RawSample> input, int step)
        {
            var output = new List<RawSample>();
            if (input == null || input.Count == 0 || step <= 1)
            {
                if (input != null)
                {
                    output.AddRange(input);
                }
                return output;
            }

            for (int i = 0; i < input.Count; i += step)
            {
                output.Add(input[i]);
            }

            return output;
        }

        public static List<RawSample> SmoothMovingAverage(IReadOnlyList<RawSample> input, int window)
        {
            var output = new List<RawSample>();
            if (input == null || input.Count == 0 || window <= 1)
            {
                if (input != null)
                {
                    output.AddRange(input);
                }
                return output;
            }

            int half = window / 2;
            for (int i = 0; i < input.Count; i++)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int j = i - half; j <= i + half; j++)
                {
                    if (j < 0 || j >= input.Count)
                    {
                        continue;
                    }
                    sum += input[j].position;
                    count++;
                }

                var smoothed = input[i];
                if (count > 0)
                {
                    smoothed.position = sum / count;
                }
                output.Add(smoothed);
            }

            return output;
        }
    }
}
