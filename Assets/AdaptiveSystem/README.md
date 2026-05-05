# AdaptiveSystem

Folder for the adaptive rehab pipeline inside Unity. Organized into three layers:
- Raw: extraction and short-term storage of trajectory samples
- Processing: preprocessing and aggregation of metrics
- Api: HTTP client and endpoints to send/receive data
- Models: request/response/entity schemas from the API spec

## Using This In a Scene

Typical flow in a task scene:
1. Add an `AdaptiveApiClient` component to a persistent GameObject (e.g., `AdaptiveSystem`).
2. Add a `TrajectoryRecorder` to the tracked hand/controller/tool.
3. Sign up or sign in with email/password, then store the returned auth session.
4. Create or replace the current patient profile with `PUT /patients/me`.
5. Start a rehab session with `POST /sessions/start`.
6. Start a task execution with `POST /task-executions`.
7. On task end: call `recorder.End()`, aggregate metrics, then `POST /task-executions/{task_execution_id}/metrics`.
8. Apply the returned decision to adjust difficulty in the scene and end the task execution/session.

Minimal example:

```csharp
using System;
using UnityEngine;
using AdaptiveSystem.Api;
using AdaptiveSystem.Raw;
using AdaptiveSystem.Processing;
using AdaptiveSystem.Models;

public class TaskTelemetryController : MonoBehaviour
{
    [SerializeField] private AdaptiveApiClient api;
    [SerializeField] private TrajectoryRecorder recorder;

    private string taskExecutionId;
    private float taskStartTime;

    public void StartTask(string sessionId, string taskId, int difficultyLevel)
    {
        taskStartTime = Time.time;
        recorder.Begin();
        StartCoroutine(api.StartTaskExecutionAsync(
            sessionId,
            taskId,
            difficultyLevel,
            DateTime.UtcNow.ToString("o"),
            result =>
        {
            if (result == null || !result.IsSuccess) return;
            taskExecutionId = result.data.task_execution_id;
        }));
    }

    public void EndTask(int errors, int prompts)
    {
        recorder.End();
        float completion = Time.time - taskStartTime;
        GlobalMetrics metrics = MetricsAggregator.Aggregate(
            recorder.Buffer.Samples,
            completion,
            errors,
            prompts
        );

        var payload = new TaskMetricsRequest { global_metrics = metrics };
        StartCoroutine(api.SubmitTaskMetricsAsync(taskExecutionId, payload, result =>
        {
            if (result == null || !result.IsSuccess) return;
            // result.data.decision => increase | maintain | decrease
        }));
    }
}
```

Notes
- `AdaptiveApiClient` reads `authBaseUrl`, `apiBaseUrl`, and `publishableKey` from `Assets/Adaptive Performance/server_connection.json` by default and throws a startup error if the file is missing or incomplete.
- `MetricsAggregator` is a baseline; extend `FeatureExtractor` with task-specific metrics as needed.
- `AdaptiveApiSmokeTest` provides a small signup/login -> patient -> session -> tasks flow for local verification.
