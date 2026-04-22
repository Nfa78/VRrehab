using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class TaskStartRequest
    {
        public string session_id;
        public string task_id;
        public int difficulty_level;
        public string start_time;
    }

    [Serializable]
    public class TaskStartResponse
    {
        public string task_execution_id;
        public string task_level_id;
        public int timeout_seconds;
        public int expected_time_seconds;
    }

    [Serializable]
    public class TaskEndRequest
    {
        public string end_time;
    }

    [Serializable]
    public class TaskEndResponse
    {
        public string task_execution_id;
    }

    [Serializable]
    public class TaskMetricsRequest
    {
        public GlobalMetrics global_metrics;
        public SceneMetric[] scene_metrics;
    }

    [Serializable]
    public class TaskMetricsResponse
    {
        public string decision;
        public string method;
        public float confidence;
    }

    [Serializable]
    public class TaskMetricsGetResponse
    {
        public GlobalMetrics global_metrics;
        public SceneMetric[] scene_metrics;
    }

    [Serializable]
    public class TaskDefinition
    {
        public string task_id;
        public string scene;
        public string task_type;
    }

    [Serializable]
    public class TaskLevel
    {
        public string task_level_id;
        public string task_id;
        public int difficulty_level;
        public int expected_time_seconds;
        public int timeout_seconds;
        public float target_size;
        public float reach_distance;
        public int task_complexity;
    }

    [Serializable]
    public class TaskExecution
    {
        public string task_execution_id;
        public string task_id;
        public string session_id;
        public string task_level_id;
        public string scene;
        public string task_type;
        public int difficulty_level;
        public float target_size;
        public float reach_distance;
        public int task_complexity;
        public string start_time;
        public string end_time;
    }

    [Serializable]
    public class TaskListResponse
    {
        public TaskDefinition[] items;
    }

    [Serializable]
    public class TaskLevelsResponse
    {
        public TaskLevel[] items;
    }

    [Serializable]
    public class SessionTasksResponse
    {
        public TaskExecution[] items;
    }
}
