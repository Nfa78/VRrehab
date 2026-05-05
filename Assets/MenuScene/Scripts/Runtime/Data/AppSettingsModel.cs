using System;
using UnityEngine;

namespace VRStrokeRehab.MenuScene
{
    [Serializable]
    public class AppSettingsModel
    {
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        public AppSettingsModel Clone()
        {
            return new AppSettingsModel
            {
                masterVolume = masterVolume
            };
        }
    }
}
