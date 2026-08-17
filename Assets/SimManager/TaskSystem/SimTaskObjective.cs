using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskSystem
{
    [Serializable]
    public class SimTaskObjective
    {
        public enum ObjectiveMode
        {
            Boolean,
            Counter
        }

        [SerializeField] private string objectiveId;
        [SerializeField] private string title;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private bool required = true;
        [SerializeField] private ObjectiveMode mode = ObjectiveMode.Boolean;
        [SerializeField] private float maxValue = 1f;
        [SerializeField] private bool completeWhenTargetReached = true;
        [SerializeField] private bool isMilestone;
        [SerializeField] private GameObject highlightTarget;
        [SerializeField] private Color currentObjectiveHighlightColor = new Color(0.2f, 0.55f, 1f, 0.18f);
        [SerializeField] private Color interactionStartedHighlightColor = new Color(0.2f, 0.8f, 1f, 0.32f);
        [SerializeField] private float highlightPulseSpeed = 2.5f;
        [SerializeField] private float highlightMinAlphaMultiplier = 0.55f;
        [SerializeField] private float highlightMaxAlphaMultiplier = 1f;
        [SerializeField] private List<string> activeRequiresCompletedObjectiveIds = new List<string>();

        [NonSerialized] private bool _isCompleted;
        [NonSerialized] private bool _isFailed;
        [NonSerialized] private bool _isActive;
        [NonSerialized] private float _startedAtSeconds;
        [NonSerialized] private float _completedAtSeconds;
        [NonSerialized] private float _currentValue;

        public string ObjectiveId => objectiveId;
        public string StepId => objectiveId;

        public string Title => title;

        public string Description => description;

        public bool Required => required;
        public bool IsMilestone => isMilestone;

        public ObjectiveMode Mode => mode;

        public bool CompleteWhenTargetReached => completeWhenTargetReached;

        public bool IsCompleted => _isCompleted;

        public bool IsFailed => _isFailed;

        public bool IsActive => _isActive;

        public float StartedAtSeconds => _startedAtSeconds;

        public float CompletedAtSeconds => _completedAtSeconds;

        public float CurrentValue => _currentValue;

        public float MaxValue => mode == ObjectiveMode.Boolean ? 1f : Mathf.Max(1f, maxValue);

        public float NormalizedProgress => Mathf.Approximately(MaxValue, 0f) ? 0f : Mathf.Clamp01(_currentValue / MaxValue);

        public GameObject HighlightTarget => highlightTarget;
        public IReadOnlyList<string> ActiveRequiresCompletedObjectiveIds => activeRequiresCompletedObjectiveIds;

        public void Begin(float currentTaskTimeSeconds)
        {
            _startedAtSeconds = currentTaskTimeSeconds;
            _completedAtSeconds = 0f;
            _isCompleted = false;
            _isFailed = false;
            _isActive = true;
            _currentValue = 0f;
            ApplyCurrentObjectiveHighlight();
        }

        public void Complete(float currentTaskTimeSeconds)
        {
            _isCompleted = true;
            _isFailed = false;
            _isActive = false;
            _completedAtSeconds = currentTaskTimeSeconds;
            _currentValue = MaxValue;
            ClearHighlight();
        }

        public void Fail()
        {
            _isCompleted = false;
            _isFailed = true;
            _isActive = false;
            _completedAtSeconds = 0f;
            ClearHighlight();
        }

        public void Reset()
        {
            _isCompleted = false;
            _isFailed = false;
            _isActive = false;
            _startedAtSeconds = 0f;
            _completedAtSeconds = 0f;
            _currentValue = 0f;
            ClearHighlight();
        }

        public bool SetBooleanState(bool value, float currentTaskTimeSeconds)
        {
            bool previousValue = _currentValue > 0.5f;
            _currentValue = value ? 1f : 0f;
            _isFailed = false;

            if (value && completeWhenTargetReached)
            {
                Complete(currentTaskTimeSeconds);
            }
            else if (!value)
            {
                _isCompleted = false;
                _completedAtSeconds = 0f;
                if (_isActive)
                {
                    ApplyCurrentObjectiveHighlight();
                }
                else
                {
                    ClearHighlight();
                }
            }
            else if (value != previousValue)
            {
                ApplyInteractionStartedHighlight();
            }

            return _isCompleted;
        }

        public bool SetProgress(float value, float currentTaskTimeSeconds)
        {
            float previousValue = _currentValue;
            _currentValue = Mathf.Clamp(value, 0f, MaxValue);
            _isFailed = false;

            if (completeWhenTargetReached && _currentValue >= MaxValue)
            {
                Complete(currentTaskTimeSeconds);
            }
            else
            {
                _isCompleted = false;
                _completedAtSeconds = 0f;

                if (!Mathf.Approximately(_currentValue, previousValue))
                {
                    ApplyInteractionStartedHighlight();
                }
            }

            return _isCompleted;
        }

        public bool AddProgress(float delta, float currentTaskTimeSeconds)
        {
            return SetProgress(_currentValue + delta, currentTaskTimeSeconds);
        }

        public void SetMaxValue(float value)
        {
            if (mode == ObjectiveMode.Boolean)
            {
                return;
            }

            maxValue = Mathf.Max(1f, value);
            _currentValue = Mathf.Clamp(_currentValue, 0f, MaxValue);
        }

        private void ApplyCurrentObjectiveHighlight()
        {
            if (highlightTarget == null)
            {
                return;
            }

            ObjectiveHighlightRuntime.ApplyPulsingHighlight(
                highlightTarget,
                currentObjectiveHighlightColor,
                highlightPulseSpeed,
                highlightMinAlphaMultiplier,
                highlightMaxAlphaMultiplier);
        }

        private void ApplyInteractionStartedHighlight()
        {
            if (highlightTarget == null)
            {
                return;
            }

            ObjectiveHighlightRuntime.ApplyPulsingHighlight(
                highlightTarget,
                interactionStartedHighlightColor,
                highlightPulseSpeed,
                Mathf.Clamp01(highlightMinAlphaMultiplier + 0.15f),
                1f);
        }

        private void ClearHighlight()
        {
            if (highlightTarget == null)
            {
                return;
            }

            ObjectiveHighlightRuntime.Clear(highlightTarget);
        }
    }
}
