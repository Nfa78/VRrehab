using System;

namespace VRStrokeRehab.MenuScene
{
    public enum MenuHandUsed
    {
        Left,
        Right
    }

    [Serializable]
    public class SceneSessionConfiguration
    {
        public MenuHandUsed handUsed = MenuHandUsed.Right;

        public SceneSessionConfiguration Clone()
        {
            return new SceneSessionConfiguration
            {
                handUsed = handUsed
            };
        }
    }
}
