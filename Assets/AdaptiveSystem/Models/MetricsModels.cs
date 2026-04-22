using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class GlobalMetrics
    {
        public float completion_time;
        public int error_count;
        public int prompt_count;
        public float path_efficiency;
        public float smoothness;
        public int hesitation_count;
        public float hesitation_total_time;
        public float spatial_accuracy;

        public int steps_completed;
        public int step_errors;
        public float avg_speed;
        public float peak_speed;
    }

    [Serializable]
    public class SceneMetric
    {
        public string feature_name;
        public float feature_value;
        public string feature_unit;
    }
}
