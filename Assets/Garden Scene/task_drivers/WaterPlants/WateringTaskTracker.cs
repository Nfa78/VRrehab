using System;
using System.IO;
using AdaptiveSystem.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TaskSystem
{
    [DisallowMultipleComponent]
    public sealed class WateringTaskTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimManager simManager;
        [SerializeField] private WaterPlantsTaskDriver taskDriver;
        [SerializeField] private WaterSpill waterSpill;
        [SerializeField] private WaterSpillSetup waterSpillSetup;
        [SerializeField] private Transform waterCanTransform;
        [SerializeField] private FlowerPot firstFlowerPot;
        [SerializeField] private FlowerPot secondFlowerPot;
        [SerializeField] private bool autoResolveReferences = true;

        [Header("JSON Export")]
        [SerializeField] private bool exportJsonOnTaskEnd = true;
        [SerializeField] private string exportFolderName = "TaskTracking";
        [SerializeField] private bool createDateSubfolder = true;
        [SerializeField] private bool logExports = true;

        [Header("Runtime")]
        [SerializeField] private bool taskRunActive;
        [SerializeField] private string activeObjectiveId = string.Empty;
        [SerializeField] private int currentAttemptIndex;
        [SerializeField] private string lastExportFilePath = string.Empty;
        [SerializeField] [TextArea(4, 12)] private string lastExportJson = string.Empty;
        [SerializeField] private TaskRunReport currentTaskReport = new TaskRunReport();
        [SerializeField] private TaskRunReport lastExportedTaskReport = new TaskRunReport();
        [SerializeField] private TaskRunReport lastCompletedTaskReport = new TaskRunReport();

        private bool subscribedToSimManager;
        private FlowerPot subscribedFirstFlowerPot;
        private FlowerPot subscribedSecondFlowerPot;
        private ObjectiveRuntime activeObjectiveRuntime;
        private float currentAttemptStartTaskSeconds;

        public event Action<TaskRunReport> TaskRunStarted;
        public event Action<TaskRunReport> TaskRunCompleted;

        public string LastExportFilePath => lastExportFilePath;

        public string LastExportJson => lastExportJson;

        private void Awake()
        {
            EnsureReferences();
            ResetTaskRunReport(currentTaskReport);
            ResetTaskRunReport(lastExportedTaskReport);
            ResetTaskRunReport(lastCompletedTaskReport);
        }

        private void OnEnable()
        {
            EnsureReferences();
            SubscribeToSimManager();
            RefreshFlowerPotSubscriptions();
            SyncWithCurrentTaskState();
        }

        private void OnDisable()
        {
            UnsubscribeFromSimManager();
            UnsubscribeFromFlowerPots();
        }

        public bool TryGetLastCompletedReport(out TaskRunReport report)
        {
            report = HasMeaningfulReport(lastCompletedTaskReport)
                ? CloneTaskRunReport(lastCompletedTaskReport)
                : null;
            return report != null;
        }

        private void Update()
        {
            if (autoResolveReferences && NeedsReferenceResolution())
            {
                EnsureReferences();
                RefreshFlowerPotSubscriptions();
            }

            if (!ShouldTrackCurrentFrame())
            {
                return;
            }

            UpdateObjectiveRuntime(simManager.CurrentClock);
        }

        private bool ShouldTrackCurrentFrame()
        {
            if (!taskRunActive || simManager == null || taskDriver == null)
            {
                return false;
            }

            if (!IsOwnedWateringTask(simManager.CurrentTask))
            {
                return false;
            }

            if (!simManager.IsRunning)
            {
                ResetRuntimeAfterPause();
                return false;
            }

            return activeObjectiveRuntime != null;
        }

        private void HandleTaskStarted(SimTask task)
        {
            if (!IsOwnedWateringTask(task))
            {
                return;
            }

            StartTaskRunIfNeeded(task, task.CurrentObjective);
        }

        private void HandleTaskEnded(SimTask task)
        {
            if (!IsOwnedWateringTask(task) || !taskRunActive)
            {
                return;
            }

            EndTaskRun(task, "completed", string.Empty, false);
        }

        private void HandleTaskFailed(SimTask task, string failureReason)
        {
            if (!IsOwnedWateringTask(task) || !taskRunActive)
            {
                return;
            }

            EndTaskRun(task, "failed", failureReason, true);
        }

        private void HandleTaskStepChanged(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedWateringTask(task) || objective == null)
            {
                return;
            }

            StartTaskRunIfNeeded(task, objective);
            BeginObjectiveTracking(objective, CurrentClockOrZero());
        }

        private void HandleTaskObjectiveCompleted(SimTask task, SimTaskObjective objective)
        {
            if (!IsOwnedWateringTask(task) || objective == null)
            {
                return;
            }

            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report == null)
            {
                return;
            }

            float currentClock = CurrentClockOrZero();
            FlushObjectiveRuntime(currentClock);
            FinalizeObjectiveReport(task, objective, report, completed: true, failed: false, failureReason: string.Empty, currentClock);
            if (string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal))
            {
                ClearActiveObjective();
            }
        }

        private void HandleFlowerPotWaterParticlesCollided(FlowerPot flowerPot, int collisionEventCount)
        {
            if (flowerPot == null || collisionEventCount <= 0 || activeObjectiveRuntime == null)
            {
                return;
            }

            ObjectiveReport report = activeObjectiveRuntime.Report;
            if (report == null || !activeObjectiveRuntime.TrackWaterMetrics)
            {
                return;
            }

            FlushObjectiveRuntime(CurrentClockOrZero());

            if (string.Equals(flowerPot.ObjectiveId, report.objectiveId, StringComparison.Ordinal))
            {
                report.scoredCollisionEvents += collisionEventCount;
                if (!report.firstScoredHitCaptured)
                {
                    report.firstScoredHitCaptured = true;
                    float currentTaskSeconds = simManager != null && simManager.CurrentTask != null
                        ? simManager.CurrentTask.GetElapsedSeconds(simManager.CurrentClock)
                        : currentAttemptStartTaskSeconds;
                    report.firstScoredHitAtAttemptSeconds = ToAttemptSeconds(currentTaskSeconds);
                    report.firstScoredHitSecondsFromStart = Mathf.Max(
                        0f,
                        report.firstScoredHitAtAttemptSeconds - report.startedAtAttemptSeconds);
                }

                return;
            }

            if (IsDetailedWateringObjective(activeObjectiveId))
            {
                report.wrongTargetCollisionEvents += collisionEventCount;
            }
        }

        private void StartTaskRunIfNeeded(SimTask task, SimTaskObjective seedObjective)
        {
            if (taskRunActive || task == null)
            {
                return;
            }

            currentAttemptIndex++;
            taskRunActive = true;
            currentAttemptStartTaskSeconds = ResolveAttemptStartTaskSeconds(task, seedObjective, CurrentClockOrZero());
            ClearActiveObjective();

            ResetTaskRunReport(currentTaskReport);

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            currentTaskReport.reportFormatVersion = 2;
            currentTaskReport.sceneName = SceneManager.GetActiveScene().name;
            currentTaskReport.taskDateLocal = startedAt.ToLocalTime().ToString("yyyy-MM-dd");
            currentTaskReport.taskId = task.TaskId;
            currentTaskReport.taskTitle = task.Title;
            currentTaskReport.taskDescription = task.Description;
            currentTaskReport.attemptIndex = currentAttemptIndex;
            int effectiveDifficultyLevel = ResolveEffectiveDifficultyLevel(task);
            currentTaskReport.difficultyProfileLevel = effectiveDifficultyLevel;
            currentTaskReport.difficultyLabel = ResolveDifficultyLabel(effectiveDifficultyLevel);
            currentTaskReport.attemptStartedAtUtc = startedAt.ToString("o");
            currentTaskReport.attemptStartedAtLocal = startedAt.ToLocalTime().ToString("o");

            InitializeObjectiveReports(task);
            TaskRunStarted?.Invoke(CloneTaskRunReport(currentTaskReport));
        }

        private void EndTaskRun(SimTask task, string outcome, string failureReason, bool objectiveFailed)
        {
            float currentClock = CurrentClockOrZero();
            FinalizeActiveObjective(task, completed: false, failed: objectiveFailed, failureReason, currentClock);
            FinalizeTaskRun(task, outcome, failureReason, currentClock);
            lastCompletedTaskReport = CloneTaskRunReport(currentTaskReport) ?? new TaskRunReport();
            TaskRunCompleted?.Invoke(CloneTaskRunReport(lastCompletedTaskReport));
            ExportCurrentTaskReport();
            ClearRunState();
        }

        private void BeginObjectiveTracking(SimTaskObjective objective, float currentClock)
        {
            ObjectiveReport report = ResolveObjectiveReport(objective.ObjectiveId);
            if (report == null)
            {
                return;
            }

            float startedAtAttemptSeconds = ToAttemptSeconds(objective.StartedAtSeconds);
            bool sameActiveObjective = string.Equals(activeObjectiveId, objective.ObjectiveId, StringComparison.Ordinal);
            bool objectiveRestarted = report.started &&
                                      (!Mathf.Approximately(report.startedAtAttemptSeconds, startedAtAttemptSeconds) ||
                                       report.completed ||
                                       report.failed);

            if (sameActiveObjective && activeObjectiveRuntime != null && !objectiveRestarted)
            {
                activeObjectiveRuntime.LastClock = currentClock;
                return;
            }

            report.ResetForRun(objective.ObjectiveId, ResolveObjectiveTitle(objective, objective.ObjectiveId));
            report.started = true;
            report.startedAtAttemptSeconds = startedAtAttemptSeconds;

            activeObjectiveId = objective.ObjectiveId;
            activeObjectiveRuntime = new ObjectiveRuntime
            {
                Report = report,
                TrackWaterMetrics = IsDetailedWateringObjective(objective.ObjectiveId),
                LastClock = currentClock,
                HasLastCanPosition = waterCanTransform != null,
                LastCanPosition = waterCanTransform != null ? waterCanTransform.position : Vector3.zero,
                WasSpillingLastFrame = waterSpill != null && waterSpill.IsSpilling
            };
        }

        private void FinalizeActiveObjective(
            SimTask task,
            bool completed,
            bool failed,
            string failureReason,
            float currentClock)
        {
            if (task == null || activeObjectiveRuntime == null)
            {
                return;
            }

            ObjectiveReport report = activeObjectiveRuntime.Report;
            if (report == null || string.IsNullOrWhiteSpace(report.objectiveId))
            {
                ClearActiveObjective();
                return;
            }

            SimTaskObjective objective = task.GetObjective(report.objectiveId);
            if (objective == null)
            {
                ClearActiveObjective();
                return;
            }

            FlushObjectiveRuntime(currentClock);
            FinalizeObjectiveReport(task, objective, report, completed, failed, failureReason, currentClock);
            ClearActiveObjective();
        }

        private void FinalizeObjectiveReport(
            SimTask task,
            SimTaskObjective objective,
            ObjectiveReport report,
            bool completed,
            bool failed,
            string failureReason,
            float currentClock)
        {
            if (task == null || objective == null || report == null)
            {
                return;
            }

            report.completed = completed || objective.IsCompleted;
            report.failed = failed || objective.IsFailed;
            report.failureReason = report.failed ? failureReason : string.Empty;

            float completedAtTaskSeconds = report.completed && objective.CompletedAtSeconds > 0f
                ? objective.CompletedAtSeconds
                : task.GetElapsedSeconds(currentClock);
            report.completedAtAttemptSeconds = ToAttemptSeconds(completedAtTaskSeconds);
            report.elapsedSeconds = Mathf.Max(0f, report.completedAtAttemptSeconds - report.startedAtAttemptSeconds);

            if (IsDetailedWateringObjective(report.objectiveId))
            {
                FinalizeWaterMetrics(report);
            }
        }

        private void FinalizeWaterMetrics(ObjectiveReport report)
        {
            int emittedRounded = Mathf.Max(
                Mathf.RoundToInt(report.estimatedEmittedParticles),
                report.scoredCollisionEvents + report.wrongTargetCollisionEvents);
            int totalCollisionEvents = report.scoredCollisionEvents + report.wrongTargetCollisionEvents;
            report.estimatedEmittedParticles = emittedRounded;
            report.totalCollisionEvents = totalCollisionEvents;
            report.estimatedUntrackedParticles = Mathf.Max(0, emittedRounded - totalCollisionEvents);
            report.estimatedTargetCollisionRate = CalculateRate(
                report.scoredCollisionEvents,
                emittedRounded);
            report.estimatedTrackedCollisionRate = CalculateRate(
                totalCollisionEvents,
                emittedRounded);
            report.estimatedOffTargetCollisionRate = CalculateRate(
                report.wrongTargetCollisionEvents,
                emittedRounded);
        }

        private void FinalizeTaskRun(SimTask task, string outcome, string failureReason, float currentClock)
        {
            if (task == null)
            {
                return;
            }

            DateTimeOffset endedAt = DateTimeOffset.UtcNow;
            currentTaskReport.outcome = outcome;
            currentTaskReport.failureReason = failureReason;
            currentTaskReport.attemptEndedAtUtc = endedAt.ToString("o");
            currentTaskReport.attemptEndedAtLocal = endedAt.ToLocalTime().ToString("o");
            currentTaskReport.attemptElapsedSeconds = ToAttemptSeconds(task.GetElapsedSeconds(currentClock));
            currentTaskReport.exportedAtUtc = endedAt.ToString("o");
            currentTaskReport.exportedAtLocal = endedAt.ToLocalTime().ToString("o");

            AggregateTaskTotals();
        }

        private void AggregateTaskTotals()
        {
            ObjectiveReport firstPlant = currentTaskReport.firstPlantObjective;
            ObjectiveReport secondPlant = currentTaskReport.secondPlantObjective;

            currentTaskReport.totalWaterCanDisplacementMeters =
                firstPlant.totalWaterCanDisplacementMeters +
                secondPlant.totalWaterCanDisplacementMeters;
            currentTaskReport.totalSpillActiveSeconds =
                firstPlant.spillActiveSeconds +
                secondPlant.spillActiveSeconds;
            currentTaskReport.totalSpillActivationCount =
                firstPlant.spillActivationCount +
                secondPlant.spillActivationCount;
            currentTaskReport.totalEstimatedEmittedParticles =
                firstPlant.estimatedEmittedParticles +
                secondPlant.estimatedEmittedParticles;
            currentTaskReport.totalScoredCollisionEvents =
                firstPlant.scoredCollisionEvents +
                secondPlant.scoredCollisionEvents;
            currentTaskReport.totalWrongTargetCollisionEvents =
                firstPlant.wrongTargetCollisionEvents +
                secondPlant.wrongTargetCollisionEvents;
            currentTaskReport.totalCollisionEvents =
                firstPlant.totalCollisionEvents +
                secondPlant.totalCollisionEvents;
            currentTaskReport.totalEstimatedUntrackedParticles =
                firstPlant.estimatedUntrackedParticles +
                secondPlant.estimatedUntrackedParticles;
            currentTaskReport.overallEstimatedTargetCollisionRate = CalculateRate(
                currentTaskReport.totalScoredCollisionEvents,
                currentTaskReport.totalEstimatedEmittedParticles);
            currentTaskReport.overallEstimatedTrackedCollisionRate = CalculateRate(
                currentTaskReport.totalCollisionEvents,
                currentTaskReport.totalEstimatedEmittedParticles);
            currentTaskReport.overallEstimatedOffTargetCollisionRate = CalculateRate(
                currentTaskReport.totalWrongTargetCollisionEvents,
                currentTaskReport.totalEstimatedEmittedParticles);
        }

        private void UpdateObjectiveRuntime(float currentClock)
        {
            if (activeObjectiveRuntime == null)
            {
                return;
            }

            float deltaClock = Mathf.Max(0f, currentClock - activeObjectiveRuntime.LastClock);
            activeObjectiveRuntime.LastClock = currentClock;

            if (!activeObjectiveRuntime.TrackWaterMetrics)
            {
                return;
            }

            UpdateCanDisplacement(activeObjectiveRuntime.Report);
            UpdateSpillMetrics(activeObjectiveRuntime.Report, deltaClock);
        }

        private void UpdateCanDisplacement(ObjectiveReport report)
        {
            if (report == null || waterCanTransform == null || activeObjectiveRuntime == null)
            {
                return;
            }

            Vector3 currentPosition = waterCanTransform.position;
            if (!activeObjectiveRuntime.HasLastCanPosition)
            {
                activeObjectiveRuntime.HasLastCanPosition = true;
                activeObjectiveRuntime.LastCanPosition = currentPosition;
                return;
            }

            float displacementDelta = Vector3.Distance(currentPosition, activeObjectiveRuntime.LastCanPosition);
            activeObjectiveRuntime.LastCanPosition = currentPosition;
            report.totalWaterCanDisplacementMeters += displacementDelta;
            if (!report.firstScoredHitCaptured)
            {
                report.waterCanDisplacementUntilFirstScoredHitMeters += displacementDelta;
            }
        }

        private void UpdateSpillMetrics(ObjectiveReport report, float deltaClock)
        {
            if (report == null || waterSpill == null || activeObjectiveRuntime == null)
            {
                return;
            }

            bool isSpilling = waterSpill.IsSpilling;
            if (isSpilling && !activeObjectiveRuntime.WasSpillingLastFrame)
            {
                report.spillActivationCount++;
            }

            if (isSpilling && deltaClock > 0f)
            {
                report.spillActiveSeconds += deltaClock;
                report.estimatedEmittedParticles += ResolveEmissionRatePerSecond() * deltaClock;
            }

            activeObjectiveRuntime.WasSpillingLastFrame = isSpilling;
        }

        private float ResolveEmissionRatePerSecond()
        {
            ParticleSystem particleSystem = ResolveParticleSystem();
            if (particleSystem == null)
            {
                return 0f;
            }

            ParticleSystem.MinMaxCurve rateCurve = particleSystem.emission.rateOverTime;
            if (rateCurve.mode == ParticleSystemCurveMode.Constant)
            {
                return Mathf.Max(0f, rateCurve.constant);
            }

            if (rateCurve.mode == ParticleSystemCurveMode.TwoConstants)
            {
                return Mathf.Max(0f, rateCurve.constantMax);
            }

            return Mathf.Max(0f, rateCurve.constantMax);
        }

        private ParticleSystem ResolveParticleSystem()
        {
            if (waterSpillSetup == null && waterSpill != null)
            {
                waterSpillSetup = waterSpill.GetComponent<WaterSpillSetup>();
            }

            return waterSpillSetup != null ? waterSpillSetup.WaterParticles : null;
        }

        private void FlushObjectiveRuntime(float currentClock)
        {
            if (activeObjectiveRuntime == null)
            {
                return;
            }

            UpdateObjectiveRuntime(currentClock);
        }

        private void ResetRuntimeAfterPause()
        {
            if (activeObjectiveRuntime == null || simManager == null)
            {
                return;
            }

            activeObjectiveRuntime.LastClock = simManager.CurrentClock;
            activeObjectiveRuntime.HasLastCanPosition = waterCanTransform != null;
            activeObjectiveRuntime.LastCanPosition = waterCanTransform != null ? waterCanTransform.position : Vector3.zero;
            activeObjectiveRuntime.WasSpillingLastFrame = waterSpill != null && waterSpill.IsSpilling;
        }

        private void ExportCurrentTaskReport()
        {
            if (!exportJsonOnTaskEnd)
            {
                return;
            }

            try
            {
                string exportDirectory = BuildExportDirectory();
                Directory.CreateDirectory(exportDirectory);

                string filePath = Path.Combine(exportDirectory, BuildExportFileName());
                string json = JsonUtility.ToJson(currentTaskReport, true);
                File.WriteAllText(filePath, json);

                lastExportFilePath = filePath;
                lastExportJson = json;
                lastExportedTaskReport = JsonUtility.FromJson<TaskRunReport>(json);

                if (logExports)
                {
                    Debug.Log($"[WateringTaskTracker] Exported watering task report to {filePath}", this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WateringTaskTracker] Failed to export watering task report: {ex.Message}", this);
            }
        }

        private string BuildExportDirectory()
        {
            string baseDirectory = Path.Combine(Application.persistentDataPath, exportFolderName, "WateringTask");
            return createDateSubfolder
                ? Path.Combine(baseDirectory, DateTime.Now.ToString("yyyy-MM-dd"))
                : baseDirectory;
        }

        private string BuildExportFileName()
        {
            string outcome = string.IsNullOrWhiteSpace(currentTaskReport.outcome) ? "unknown" : currentTaskReport.outcome;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return $"WateringTask_{stamp}_attempt{currentTaskReport.attemptIndex}_{outcome}.json";
        }

        private void InitializeObjectiveReports(SimTask task)
        {
            InitializeObjectiveReport(currentTaskReport.pickObjective, task, taskDriver != null ? taskDriver.PickStepId : "pick");
            InitializeObjectiveReport(currentTaskReport.firstPlantObjective, task, taskDriver != null ? taskDriver.FirstPlantStepId : "wp1");
            InitializeObjectiveReport(currentTaskReport.secondPlantObjective, task, taskDriver != null ? taskDriver.SecondPlantStepId : "wp2");
            InitializeObjectiveReport(currentTaskReport.returnObjective, task, taskDriver != null ? taskDriver.ReturnStepId : "return_can");
        }

        private static void InitializeObjectiveReport(ObjectiveReport report, SimTask task, string objectiveId)
        {
            if (report == null)
            {
                return;
            }

            SimTaskObjective objective = task != null ? task.GetObjective(objectiveId) : null;
            report.ResetForRun(objectiveId, ResolveObjectiveTitle(objective, objectiveId));
        }

        private ObjectiveReport ResolveObjectiveReport(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId) || taskDriver == null)
            {
                return null;
            }

            if (string.Equals(objectiveId, taskDriver.PickStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.pickObjective;
            }

            if (string.Equals(objectiveId, taskDriver.FirstPlantStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.firstPlantObjective;
            }

            if (string.Equals(objectiveId, taskDriver.SecondPlantStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.secondPlantObjective;
            }

            if (string.Equals(objectiveId, taskDriver.ReturnStepId, StringComparison.Ordinal))
            {
                return currentTaskReport.returnObjective;
            }

            return null;
        }

        private bool NeedsReferenceResolution()
        {
            return simManager == null ||
                   taskDriver == null ||
                   waterSpill == null ||
                   waterSpillSetup == null ||
                   waterCanTransform == null ||
                   firstFlowerPot == null ||
                   secondFlowerPot == null;
        }

        private void EnsureReferences()
        {
            if (taskDriver == null)
            {
                taskDriver = GetComponent<WaterPlantsTaskDriver>();
            }

            if (taskDriver == null)
            {
                taskDriver = FindSceneComponent<WaterPlantsTaskDriver>();
            }

            if (simManager == null)
            {
                simManager = FindSceneComponent<SimManager>();
            }

            if (waterSpill == null && taskDriver != null)
            {
                waterSpill = taskDriver.GetComponentInChildren<WaterSpill>(true);
            }

            if (waterSpill == null)
            {
                waterSpill = FindSceneComponent<WaterSpill>();
            }

            if (waterSpillSetup == null && waterSpill != null)
            {
                waterSpillSetup = waterSpill.GetComponent<WaterSpillSetup>();
            }

            if (waterCanTransform == null && waterSpill != null)
            {
                waterCanTransform = waterSpill.transform;
            }

            ResolveFlowerPots();
        }

        private void ResolveFlowerPots()
        {
            if (taskDriver == null || (firstFlowerPot != null && secondFlowerPot != null))
            {
                return;
            }

            Scene targetScene = taskDriver.gameObject.scene;
            FlowerPot[] flowerPots = FindObjectsByType<FlowerPot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < flowerPots.Length; i++)
            {
                FlowerPot flowerPot = flowerPots[i];
                if (flowerPot == null || flowerPot.gameObject.scene != targetScene)
                {
                    continue;
                }

                if (firstFlowerPot == null &&
                    string.Equals(flowerPot.ObjectiveId, taskDriver.FirstPlantStepId, StringComparison.Ordinal))
                {
                    firstFlowerPot = flowerPot;
                }

                if (secondFlowerPot == null &&
                    string.Equals(flowerPot.ObjectiveId, taskDriver.SecondPlantStepId, StringComparison.Ordinal))
                {
                    secondFlowerPot = flowerPot;
                }
            }
        }

        private void SubscribeToSimManager()
        {
            if (subscribedToSimManager || simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted += HandleTaskStarted;
            simManager.LogicalTaskEnded += HandleTaskEnded;
            simManager.LogicalTaskFailed += HandleTaskFailed;
            simManager.LogicalTaskStepChanged += HandleTaskStepChanged;
            simManager.LogicalTaskObjectiveCompleted += HandleTaskObjectiveCompleted;
            subscribedToSimManager = true;
        }

        private void UnsubscribeFromSimManager()
        {
            if (!subscribedToSimManager || simManager == null)
            {
                return;
            }

            simManager.LogicalTaskStarted -= HandleTaskStarted;
            simManager.LogicalTaskEnded -= HandleTaskEnded;
            simManager.LogicalTaskFailed -= HandleTaskFailed;
            simManager.LogicalTaskStepChanged -= HandleTaskStepChanged;
            simManager.LogicalTaskObjectiveCompleted -= HandleTaskObjectiveCompleted;
            subscribedToSimManager = false;
        }

        private void RefreshFlowerPotSubscriptions()
        {
            if (subscribedFirstFlowerPot != firstFlowerPot)
            {
                if (subscribedFirstFlowerPot != null)
                {
                    subscribedFirstFlowerPot.WaterParticlesCollided -= HandleFlowerPotWaterParticlesCollided;
                }

                subscribedFirstFlowerPot = firstFlowerPot;
                if (subscribedFirstFlowerPot != null)
                {
                    subscribedFirstFlowerPot.WaterParticlesCollided += HandleFlowerPotWaterParticlesCollided;
                }
            }

            if (subscribedSecondFlowerPot != secondFlowerPot)
            {
                if (subscribedSecondFlowerPot != null)
                {
                    subscribedSecondFlowerPot.WaterParticlesCollided -= HandleFlowerPotWaterParticlesCollided;
                }

                subscribedSecondFlowerPot = secondFlowerPot;
                if (subscribedSecondFlowerPot != null)
                {
                    subscribedSecondFlowerPot.WaterParticlesCollided += HandleFlowerPotWaterParticlesCollided;
                }
            }
        }

        private void UnsubscribeFromFlowerPots()
        {
            if (subscribedFirstFlowerPot != null)
            {
                subscribedFirstFlowerPot.WaterParticlesCollided -= HandleFlowerPotWaterParticlesCollided;
                subscribedFirstFlowerPot = null;
            }

            if (subscribedSecondFlowerPot != null)
            {
                subscribedSecondFlowerPot.WaterParticlesCollided -= HandleFlowerPotWaterParticlesCollided;
                subscribedSecondFlowerPot = null;
            }
        }

        private void SyncWithCurrentTaskState()
        {
            if (simManager == null || taskDriver == null || !IsOwnedWateringTask(simManager.CurrentTask))
            {
                return;
            }

            StartTaskRunIfNeeded(simManager.CurrentTask, simManager.CurrentObjective);
            if (simManager.CurrentObjective != null)
            {
                BeginObjectiveTracking(simManager.CurrentObjective, simManager.CurrentClock);
            }
        }

        private bool IsOwnedWateringTask(SimTask task)
        {
            return task != null &&
                   taskDriver != null &&
                   string.Equals(task.TaskId, taskDriver.TaskId, StringComparison.Ordinal) &&
                   (simManager == null || simManager.CurrentTaskDriver == null || ReferenceEquals(simManager.CurrentTaskDriver, taskDriver));
        }

        private float ResolveAttemptStartTaskSeconds(SimTask task, SimTaskObjective seedObjective, float currentClock)
        {
            if (seedObjective != null)
            {
                return seedObjective.StartedAtSeconds;
            }

            if (task != null && task.CurrentObjective != null)
            {
                return task.CurrentObjective.StartedAtSeconds;
            }

            return task != null ? task.GetElapsedSeconds(currentClock) : 0f;
        }

        private float ToAttemptSeconds(float taskElapsedSeconds)
        {
            return Mathf.Max(0f, taskElapsedSeconds - currentAttemptStartTaskSeconds);
        }

        private float CurrentClockOrZero()
        {
            return simManager != null ? simManager.CurrentClock : 0f;
        }

        private int ResolveEffectiveDifficultyLevel(SimTask task)
        {
            if (task != null &&
                !string.IsNullOrWhiteSpace(task.TaskId) &&
                AdaptiveRuntimeContext.TryGetTaskDifficulty(task.TaskId, out int storedDifficultyLevel))
            {
                return taskDriver != null
                    ? taskDriver.ClampDifficultyLevel(storedDifficultyLevel)
                    : Mathf.Max(1, storedDifficultyLevel);
            }

            if (taskDriver != null)
            {
                return Mathf.Max(1, taskDriver.DifficultyLevel);
            }

            return task != null ? Mathf.Max(1, task.DifficultyProfileLevel) : 1;
        }

        private static string ResolveDifficultyLabel(int difficultyLevel)
        {
            return $"Level{Mathf.Max(0, difficultyLevel - 1)}";
        }

        private void ClearRunState()
        {
            taskRunActive = false;
            ClearActiveObjective();
        }

        private void ClearActiveObjective()
        {
            activeObjectiveId = string.Empty;
            activeObjectiveRuntime = null;
        }

        private T FindSceneComponent<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Scene targetScene = gameObject.scene;
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == targetScene)
                {
                    return component;
                }
            }

            return components.Length > 0 ? components[0] : null;
        }

        private bool IsDetailedWateringObjective(string objectiveId)
        {
            if (taskDriver == null || string.IsNullOrWhiteSpace(objectiveId))
            {
                return false;
            }

            return string.Equals(objectiveId, taskDriver.FirstPlantStepId, StringComparison.Ordinal) ||
                   string.Equals(objectiveId, taskDriver.SecondPlantStepId, StringComparison.Ordinal);
        }

        private static string ResolveObjectiveTitle(SimTaskObjective objective, string fallbackObjectiveId)
        {
            if (objective != null && !string.IsNullOrWhiteSpace(objective.Title))
            {
                return objective.Title;
            }

            return fallbackObjectiveId;
        }

        private static float CalculateRate(int numerator, float denominator)
        {
            return denominator > 0f ? Mathf.Max(0, numerator) / denominator : 0f;
        }

        private static bool HasMeaningfulReport(TaskRunReport report)
        {
            return report != null &&
                   !string.IsNullOrWhiteSpace(report.taskId) &&
                   report.attemptIndex > 0;
        }

        private static TaskRunReport CloneTaskRunReport(TaskRunReport source)
        {
            if (source == null)
            {
                return null;
            }

            string json = JsonUtility.ToJson(source);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<TaskRunReport>(json);
        }

        private static void ResetTaskRunReport(TaskRunReport report)
        {
            if (report == null)
            {
                return;
            }

            report.reportFormatVersion = 2;
            report.sceneName = string.Empty;
            report.taskDateLocal = string.Empty;
            report.exportedAtUtc = string.Empty;
            report.exportedAtLocal = string.Empty;
            report.taskId = string.Empty;
            report.taskTitle = string.Empty;
            report.taskDescription = string.Empty;
            report.attemptStartedAtUtc = string.Empty;
            report.attemptStartedAtLocal = string.Empty;
            report.attemptEndedAtUtc = string.Empty;
            report.attemptEndedAtLocal = string.Empty;
            report.outcome = string.Empty;
            report.failureReason = string.Empty;
            report.attemptIndex = 0;
            report.difficultyProfileLevel = 0;
            report.difficultyLabel = string.Empty;
            report.attemptElapsedSeconds = 0f;
            report.totalWaterCanDisplacementMeters = 0f;
            report.totalSpillActiveSeconds = 0f;
            report.totalSpillActivationCount = 0;
            report.totalEstimatedEmittedParticles = 0f;
            report.totalScoredCollisionEvents = 0;
            report.totalWrongTargetCollisionEvents = 0;
            report.totalCollisionEvents = 0;
            report.totalEstimatedUntrackedParticles = 0;
            report.overallEstimatedTargetCollisionRate = 0f;
            report.overallEstimatedTrackedCollisionRate = 0f;
            report.overallEstimatedOffTargetCollisionRate = 0f;
            report.pickObjective.ResetForRun(string.Empty, string.Empty);
            report.firstPlantObjective.ResetForRun(string.Empty, string.Empty);
            report.secondPlantObjective.ResetForRun(string.Empty, string.Empty);
            report.returnObjective.ResetForRun(string.Empty, string.Empty);
        }

        [Serializable]
        public sealed class TaskRunReport
        {
            public int reportFormatVersion = 2;
            public string sceneName = string.Empty;
            public string taskDateLocal = string.Empty;
            public string exportedAtUtc = string.Empty;
            public string exportedAtLocal = string.Empty;
            public string taskId = string.Empty;
            public string taskTitle = string.Empty;
            public string taskDescription = string.Empty;
            public string attemptStartedAtUtc = string.Empty;
            public string attemptStartedAtLocal = string.Empty;
            public string attemptEndedAtUtc = string.Empty;
            public string attemptEndedAtLocal = string.Empty;
            public string outcome = string.Empty;
            public string failureReason = string.Empty;
            public int attemptIndex;
            public int difficultyProfileLevel;
            public string difficultyLabel = string.Empty;
            public float attemptElapsedSeconds;
            public float totalWaterCanDisplacementMeters;
            public float totalSpillActiveSeconds;
            public int totalSpillActivationCount;
            public float totalEstimatedEmittedParticles;
            public int totalScoredCollisionEvents;
            public int totalWrongTargetCollisionEvents;
            public int totalCollisionEvents;
            public int totalEstimatedUntrackedParticles;
            public float overallEstimatedTargetCollisionRate;
            public float overallEstimatedTrackedCollisionRate;
            public float overallEstimatedOffTargetCollisionRate;
            public ObjectiveReport pickObjective = new ObjectiveReport();
            public ObjectiveReport firstPlantObjective = new ObjectiveReport();
            public ObjectiveReport secondPlantObjective = new ObjectiveReport();
            public ObjectiveReport returnObjective = new ObjectiveReport();
        }

        [Serializable]
        public sealed class ObjectiveReport
        {
            public string objectiveId = string.Empty;
            public string objectiveTitle = string.Empty;
            public bool started;
            public bool completed;
            public bool failed;
            public string failureReason = string.Empty;
            public float startedAtAttemptSeconds;
            public float completedAtAttemptSeconds;
            public float elapsedSeconds;
            public float totalWaterCanDisplacementMeters;
            public float waterCanDisplacementUntilFirstScoredHitMeters;
            public bool firstScoredHitCaptured;
            public float firstScoredHitAtAttemptSeconds = -1f;
            public float firstScoredHitSecondsFromStart = -1f;
            public float spillActiveSeconds;
            public int spillActivationCount;
            public float estimatedEmittedParticles;
            public int scoredCollisionEvents;
            public int wrongTargetCollisionEvents;
            public int totalCollisionEvents;
            public int estimatedUntrackedParticles;
            public float estimatedTargetCollisionRate;
            public float estimatedTrackedCollisionRate;
            public float estimatedOffTargetCollisionRate;

            public void ResetForRun(string newObjectiveId, string newObjectiveTitle)
            {
                objectiveId = newObjectiveId;
                objectiveTitle = newObjectiveTitle;
                started = false;
                completed = false;
                failed = false;
                failureReason = string.Empty;
                startedAtAttemptSeconds = 0f;
                completedAtAttemptSeconds = 0f;
                elapsedSeconds = 0f;
                totalWaterCanDisplacementMeters = 0f;
                waterCanDisplacementUntilFirstScoredHitMeters = 0f;
                firstScoredHitCaptured = false;
                firstScoredHitAtAttemptSeconds = -1f;
                firstScoredHitSecondsFromStart = -1f;
                spillActiveSeconds = 0f;
                spillActivationCount = 0;
                estimatedEmittedParticles = 0f;
                scoredCollisionEvents = 0;
                wrongTargetCollisionEvents = 0;
                totalCollisionEvents = 0;
                estimatedUntrackedParticles = 0;
                estimatedTargetCollisionRate = 0f;
                estimatedTrackedCollisionRate = 0f;
                estimatedOffTargetCollisionRate = 0f;
            }
        }

        private sealed class ObjectiveRuntime
        {
            public ObjectiveReport Report;
            public bool TrackWaterMetrics;
            public float LastClock;
            public Vector3 LastCanPosition;
            public bool HasLastCanPosition;
            public bool WasSpillingLastFrame;
        }
    }
}
