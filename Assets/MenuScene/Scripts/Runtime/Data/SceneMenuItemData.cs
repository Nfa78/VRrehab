using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    [CreateAssetMenu(fileName = "SceneMenuItem", menuName = "VR Stroke Rehab/Menu Scene/Scene Menu Item")]
    public class SceneMenuItemData : ScriptableObject
    {
        [SerializeField] private string sceneId = string.Empty;
        [SerializeField] private string sceneTitle = string.Empty;
        [SerializeField] private string sceneName = string.Empty;
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField] private string shortDescription = string.Empty;
        [SerializeField] private Sprite previewImage;

        public string SceneId
        {
            get { return sceneId; }
        }

        public string SceneTitle
        {
            get { return string.IsNullOrWhiteSpace(sceneTitle) ? name : sceneTitle; }
        }

        public string SceneName
        {
            get { return sceneName; }
        }

        public string ScenePath
        {
            get { return scenePath; }
        }

        public string ShortDescription
        {
            get { return shortDescription; }
        }

        public Sprite PreviewImage
        {
            get { return previewImage; }
        }

        public string SceneLoadReference
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(scenePath))
                {
                    return scenePath;
                }

                return sceneName;
            }
        }

        public bool HasLoadReference
        {
            get { return !string.IsNullOrWhiteSpace(SceneLoadReference); }
        }

        public static SceneMenuItemData CreateRuntime(string sceneId, string sceneTitle, string sceneName, string scenePath, Sprite previewImage)
        {
            var item = CreateInstance<SceneMenuItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.sceneId = !string.IsNullOrWhiteSpace(sceneId)
                ? sceneId
                : (!string.IsNullOrWhiteSpace(scenePath) ? scenePath : sceneName);
            item.sceneTitle = sceneTitle;
            item.sceneName = sceneName;
            item.scenePath = scenePath;
            item.previewImage = previewImage;
            item.shortDescription = string.Empty;
            item.name = !string.IsNullOrWhiteSpace(item.SceneTitle) ? item.SceneTitle : "SceneMenuItem";
            return item;
        }
    }
}
