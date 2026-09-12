using UnityEngine;

namespace AdaptiveSystem.Raw
{
    public class TrajectoryRecorder : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float sampleIntervalSeconds = 0.02f;

        private float _nextSampleTime;
        private readonly TrajectoryBuffer _buffer = new TrajectoryBuffer();

        public TrajectoryBuffer Buffer => _buffer;

        public Transform Target
        {
            get { return target; }
            set { target = value; }
        }

        public float SampleIntervalSeconds
        {
            get { return sampleIntervalSeconds; }
            set { sampleIntervalSeconds = Mathf.Max(0.001f, value); }
        }

        public void Begin()
        {
            _buffer.Start();
            _nextSampleTime = Time.time;
        }

        public void End()
        {
            _buffer.Stop();
        }

        private void Update()
        {
            if (!_buffer.IsRecording || target == null)
            {
                return;
            }

            if (Time.time < _nextSampleTime)
            {
                return;
            }

            _buffer.AddSample(new RawSample(target.position, Time.time));
            _nextSampleTime = Time.time + sampleIntervalSeconds;
        }
    }
}
