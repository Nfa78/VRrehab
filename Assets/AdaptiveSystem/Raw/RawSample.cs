using System;
using UnityEngine;

namespace AdaptiveSystem.Raw
{
    [Serializable]
    public struct RawSample
    {
        public Vector3 position;
        public float timestamp;

        public RawSample(Vector3 position, float timestamp)
        {
            this.position = position;
            this.timestamp = timestamp;
        }
    }
}
