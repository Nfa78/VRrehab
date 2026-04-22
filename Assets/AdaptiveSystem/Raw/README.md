# Raw

Purpose
- Low-level data capture and temporary storage of motion samples.

Files
- RawSample.cs: Single position + timestamp sample.
- TrajectoryBuffer.cs: In-memory buffer that records samples during a task.
- TrajectoryRecorder.cs: MonoBehaviour that samples a target Transform on an interval.

Usage
- Attach TrajectoryRecorder to the tracked object and call Begin/End around a task.
