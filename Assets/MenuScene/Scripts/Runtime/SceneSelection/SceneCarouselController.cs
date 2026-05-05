using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRStrokeRehab.MenuScene
{
    public class SceneCarouselController : MonoBehaviour
    {
        [SerializeField] private SceneMenuCatalog catalog;
        [SerializeField] private List<SceneMenuSerializedItem> serializedScenes = new List<SceneMenuSerializedItem>();
        [SerializeField] private Transform cardContainer;
        [SerializeField] private SceneCardView cardPrefab;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private SceneDetailsPanelView detailsPanel;

        private readonly List<SceneCardView> spawnedCards = new List<SceneCardView>();
        private readonly List<SceneMenuItemData> activeItems = new List<SceneMenuItemData>();
        private readonly List<SceneMenuItemData> generatedItems = new List<SceneMenuItemData>();
        private int selectedIndex = -1;

        public event Action<SceneMenuItemData> SelectionChanged;

        public SceneMenuCatalog Catalog
        {
            get { return catalog; }
        }

        public Transform CardContainer
        {
            get { return cardContainer; }
        }

        public IReadOnlyList<SceneMenuSerializedItem> SerializedScenes
        {
            get { return serializedScenes; }
        }

        public SceneCardView CardPrefab
        {
            get { return cardPrefab; }
        }

        public int ConfiguredSceneCount
        {
            get
            {
                if (HasSerializedSceneSource())
                {
                    return CountConfiguredSerializedScenes();
                }

                return catalog != null ? catalog.Count : 0;
            }
        }

        public SceneMenuItemData SelectedScene
        {
            get
            {
                if (selectedIndex < 0 || selectedIndex >= activeItems.Count)
                {
                    return null;
                }

                return activeItems[selectedIndex];
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSerializedSceneMetadata();
        }
#endif

        private void Awake()
        {
            if (previousButton != null)
            {
                previousButton.onClick.AddListener(SelectPrevious);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(SelectNext);
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(SelectPrevious);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(SelectNext);
            }

            UnsubscribeCards();
            ClearGeneratedItems();
        }

        public void Initialize()
        {
            RebuildCards();
        }

        public bool HasRequiredReferences(out string message)
        {
            if (ConfiguredSceneCount == 0)
            {
                message = "No scene entries are configured. Assign either a scene catalog or serialized scenes.";
                return false;
            }

            if (cardContainer == null)
            {
                message = "Card container is not assigned.";
                return false;
            }

            if (cardPrefab == null)
            {
                message = "Card prefab is not assigned.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public void SelectPrevious()
        {
            if (activeItems.Count == 0)
            {
                return;
            }

            var nextIndex = selectedIndex <= 0 ? activeItems.Count - 1 : selectedIndex - 1;
            SelectIndex(nextIndex);
        }

        public void SelectNext()
        {
            if (activeItems.Count == 0)
            {
                return;
            }

            var nextIndex = selectedIndex >= activeItems.Count - 1 ? 0 : selectedIndex + 1;
            SelectIndex(nextIndex);
        }

        public void SelectScene(SceneMenuItemData sceneItem)
        {
            if (sceneItem == null)
            {
                return;
            }

            for (var i = 0; i < activeItems.Count; i++)
            {
                if (activeItems[i] == sceneItem)
                {
                    SelectIndex(i);
                    return;
                }
            }
        }

        private void RebuildCards()
        {
            UnsubscribeCards();
            ClearSpawnedCards();
            RebuildActiveItems();

            if (cardContainer == null || cardPrefab == null || activeItems.Count == 0)
            {
                selectedIndex = -1;
                if (detailsPanel != null)
                {
                    detailsPanel.ShowScene(null);
                }

                UpdateNavigationButtons();
                NotifySelectionChanged();
                return;
            }

            for (var i = 0; i < activeItems.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.Bind(activeItems[i], false);
                card.Selected += HandleCardSelected;
                spawnedCards.Add(card);
            }

            SelectIndex(0);
        }

        private void SelectIndex(int index)
        {
            if (activeItems.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, activeItems.Count - 1);

            for (var i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null)
                {
                    spawnedCards[i].SetSelected(i == selectedIndex);
                }
            }

            if (detailsPanel != null)
            {
                detailsPanel.ShowScene(SelectedScene);
            }

            UpdateNavigationButtons();
            NotifySelectionChanged();
        }

        private void UpdateNavigationButtons()
        {
            var interactable = activeItems.Count > 1;
            if (previousButton != null)
            {
                previousButton.interactable = interactable;
            }

            if (nextButton != null)
            {
                nextButton.interactable = interactable;
            }
        }

        private void RebuildActiveItems()
        {
            activeItems.Clear();
            ClearGeneratedItems();

            if (HasSerializedSceneSource())
            {
                for (var i = 0; i < serializedScenes.Count; i++)
                {
                    var sceneDefinition = serializedScenes[i];
                    if (sceneDefinition == null || !sceneDefinition.HasLoadReference)
                    {
                        continue;
                    }

                    var runtimeItem = SceneMenuItemData.CreateRuntime(
                        sceneDefinition.SceneId,
                        sceneDefinition.SceneTitle,
                        sceneDefinition.SceneName,
                        sceneDefinition.ScenePath,
                        sceneDefinition.PreviewImage);

                    generatedItems.Add(runtimeItem);
                    activeItems.Add(runtimeItem);
                }

                return;
            }

            if (catalog == null || catalog.Items == null)
            {
                return;
            }

            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog.Items[i] != null)
                {
                    activeItems.Add(catalog.Items[i]);
                }
            }
        }

        private bool HasSerializedSceneSource()
        {
            return CountConfiguredSerializedScenes() > 0;
        }

        private int CountConfiguredSerializedScenes()
        {
            if (serializedScenes == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < serializedScenes.Count; i++)
            {
                if (serializedScenes[i] != null && serializedScenes[i].HasLoadReference)
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearGeneratedItems()
        {
            for (var i = 0; i < generatedItems.Count; i++)
            {
                if (generatedItems[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(generatedItems[i]);
                }
                else
                {
                    DestroyImmediate(generatedItems[i]);
                }
            }

            generatedItems.Clear();
        }

#if UNITY_EDITOR
        private void SyncSerializedSceneMetadata()
        {
            if (serializedScenes == null)
            {
                return;
            }

            for (var i = 0; i < serializedScenes.Count; i++)
            {
                if (serializedScenes[i] != null)
                {
                    serializedScenes[i].SyncEditorMetadata();
                }
            }
        }
#endif

        private void HandleCardSelected(SceneMenuItemData sceneItem)
        {
            SelectScene(sceneItem);
        }

        private void NotifySelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged.Invoke(SelectedScene);
            }
        }

        private void UnsubscribeCards()
        {
            for (var i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null)
                {
                    spawnedCards[i].Selected -= HandleCardSelected;
                }
            }
        }

        private void ClearSpawnedCards()
        {
            for (var i = 0; i < spawnedCards.Count; i++)
            {
                var card = spawnedCards[i];
                if (card == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(card.gameObject);
                }
                else
                {
                    DestroyImmediate(card.gameObject);
                }
            }

            spawnedCards.Clear();
        }
    }
}
