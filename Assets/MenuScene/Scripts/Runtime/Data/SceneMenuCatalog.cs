using System.Collections.Generic;
using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    [CreateAssetMenu(fileName = "SceneMenuCatalog", menuName = "VR Stroke Rehab/Menu Scene/Scene Menu Catalog")]
    public class SceneMenuCatalog : ScriptableObject
    {
        [SerializeField] private List<SceneMenuItemData> items = new List<SceneMenuItemData>();

        public IReadOnlyList<SceneMenuItemData> Items
        {
            get { return items; }
        }

        public int Count
        {
            get { return items != null ? items.Count : 0; }
        }
    }
}
