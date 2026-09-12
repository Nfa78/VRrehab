using TaskSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimDashboardGUI : MonoBehaviour
{
    private const string WaterPlantsTaskId = "water_plants";
    private const int ProgressBarSegments = 12;

    [SerializeField] private SimManager simManager;

    public TextMeshProUGUI TaskName_UIText;
    public TextMeshProUGUI TaskObjectiveStatus_UIText;
    public Button pause, restart, exit;
    public Sprite playSprite, pauseSprite;

    private void Awake()
    {
        if (simManager == null)
        {
            simManager = GetComponent<SimManager>();
        }
    }

    private void OnEnable()
    {
        pause?.onClick.AddListener(TogglePause);
        restart?.onClick.AddListener(RestartSimulation);
        exit?.onClick.AddListener(ExitSimulation);
    }

    private void OnDisable()
    {
        pause?.onClick.RemoveListener(TogglePause);
        restart?.onClick.RemoveListener(RestartSimulation);
        exit?.onClick.RemoveListener(ExitSimulation);
    }

    private void Update()
    {
        Refresh();
    }

    public void TogglePause()
    {
        if (simManager == null)
        {
            return;
        }

        if (simManager.IsPaused)
        {
            simManager.ResumeSimulation();
        }
        else
        {
            simManager.PauseSimulation();
        }
    }

    public void RestartSimulation()
    {
        if (simManager == null)
        {
            return;
        }

        simManager.ResetSimulation();
        simManager.StartSimulation();
    }

    public void ExitSimulation()
    {
        simManager?.StopSimulation();
    }

    private void Refresh()
    {
        SimTask task = simManager != null ? simManager.CurrentTask : null;
        SimTaskObjective objective = simManager != null ? simManager.CurrentObjective : null;

        if (TaskName_UIText != null)
        {
            TaskName_UIText.text = task == null ? "No active task" : Label(task.Title, task.TaskId);
        }

        if (TaskObjectiveStatus_UIText != null)
        {
            TaskObjectiveStatus_UIText.text = objective == null
                ? "No active objective\nProgress: -"
                : FormatObjectiveStatus(task, objective);
        }

        if (pause != null && pause.image != null)
        {
            pause.image.sprite = simManager != null && simManager.IsPaused ? playSprite : pauseSprite;
        }
    }

    private static string Label(string title, string id)
    {
        return string.IsNullOrWhiteSpace(title) ? id : title;
    }

    private static string FormatObjectiveStatus(SimTask task, SimTaskObjective objective)
    {
        if (objective == null)
        {
            return "No active objective\nProgress: -";
        }

        float normalizedProgress = objective.NormalizedProgress;
        string progressBar = BuildProgressBar(normalizedProgress);
        string percentLabel = $"{normalizedProgress * 100f:0.0}%";

        if (task != null &&
            string.Equals(task.TaskId, WaterPlantsTaskId, System.StringComparison.Ordinal) &&
            objective.Mode == SimTaskObjective.ObjectiveMode.Counter)
        {
            return $"{Label(objective.Title, objective.ObjectiveId)}\nWatered: {objective.CurrentValue:0.00}s/{objective.MaxValue:0.00}s {progressBar} {percentLabel}";
        }

        return $"{Label(objective.Title, objective.ObjectiveId)}\nProgress: {objective.CurrentValue:0.##}/{objective.MaxValue:0.##} {progressBar} {percentLabel}";
    }

    private static string BuildProgressBar(float normalizedProgress)
    {
        int filledSegments = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(normalizedProgress) * ProgressBarSegments),
            0,
            ProgressBarSegments);

        return $"[{new string('#', filledSegments)}{new string('-', ProgressBarSegments - filledSegments)}]";
    }
}
