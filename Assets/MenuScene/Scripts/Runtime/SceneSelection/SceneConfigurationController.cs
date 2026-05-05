using System;
using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    public class SceneConfigurationController : MonoBehaviour
    {
        [SerializeField] private SceneConfigurationPanelView view;
        [SerializeField] private MenuHandUsed defaultHandUsed = MenuHandUsed.Right;

        private SceneSessionConfiguration configuration = new SceneSessionConfiguration();

        public event Action<SceneSessionConfiguration> ConfigurationChanged;

        public SceneConfigurationPanelView View
        {
            get { return view; }
        }

        public SceneSessionConfiguration CurrentConfiguration
        {
            get { return configuration; }
        }

        private void Awake()
        {
            if (view != null)
            {
                view.HandSelected += HandleHandSelected;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (view != null)
            {
                view.HandSelected -= HandleHandSelected;
            }
        }

        public void Initialize()
        {
            configuration.handUsed = defaultHandUsed;
            ApplyToView();
        }

        public SceneSessionConfiguration CreateSnapshot()
        {
            return configuration != null ? configuration.Clone() : new SceneSessionConfiguration();
        }

        public bool HasRequiredReferences(out string message)
        {
            if (view == null)
            {
                message = "Scene configuration view is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void HandleHandSelected(MenuHandUsed handUsed)
        {
            configuration.handUsed = handUsed;
            ApplyToView();

            if (ConfigurationChanged != null)
            {
                ConfigurationChanged.Invoke(CreateSnapshot());
            }
        }

        private void ApplyToView()
        {
            if (view != null)
            {
                view.SetSelectedHand(configuration.handUsed);
            }
        }
    }
}
