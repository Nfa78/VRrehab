using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRStrokeRehab.MenuScene
{
    [Serializable]
    public class SceneMenuSerializedItem
    {
        [SerializeField] private UnityEngine.Object sceneObject;
        [SerializeField] private string sceneTitle = string.Empty;
        [SerializeField] private Sprite previewImage;
        [SerializeField, HideInInspector] private string sceneName = string.Empty;
        [SerializeField, HideInInspector] private string scenePath = string.Empty;

        public string SceneTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(sceneTitle))
                {
                    return sceneTitle;
                }

                if (!string.IsNullOrWhiteSpace(sceneName))
                {
                    return sceneName;
                }

                return sceneObject != null ? sceneObject.name : string.Empty;
            }
        }

        public Sprite PreviewImage
        {
            get { return previewImage; }
        }

        public string SceneName
        {
            get { return sceneName; }
        }

        public string ScenePath
        {
            get { return scenePath; }
        }

        public string SceneId
        {
            get { return !string.IsNullOrWhiteSpace(scenePath) ? scenePath : sceneName; }
        }

        public string SceneLoadReference
        {
            get { return !string.IsNullOrWhiteSpace(scenePath) ? scenePath : sceneName; }
        }

        public bool HasLoadReference
        {
            get { return !string.IsNullOrWhiteSpace(SceneLoadReference); }
        }

#if UNITY_EDITOR
        public void SyncEditorMetadata()
        {
            scenePath = string.Empty;
            sceneName = string.Empty;

            if (sceneObject == null)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(sceneObject);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                sceneName = sceneObject.name;
                return;
            }

            scenePath = assetPath;
            sceneName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        }
#endif
    }
}
