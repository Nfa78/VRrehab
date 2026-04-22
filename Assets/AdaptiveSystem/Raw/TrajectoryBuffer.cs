using System.Collections.Generic;

namespace AdaptiveSystem.Raw
{
    public class TrajectoryBuffer
    {
        private readonly List<RawSample> _samples = new List<RawSample>(1024);
        public bool IsRecording { get; private set; }

        public IReadOnlyList<RawSample> Samples => _samples;

        public void Start()
        {
            IsRecording = true;
            _samples.Clear();
        }

        public void Stop()
        {
            IsRecording = false;
        }

        public void AddSample(RawSample sample)
        {
            if (!IsRecording)
            {
                return;
            }

            _samples.Add(sample);
        }

        public void Clear()
        {
            _samples.Clear();
        }
    }
}
