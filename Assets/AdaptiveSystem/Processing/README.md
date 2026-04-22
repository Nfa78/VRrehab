# Processing

Purpose
- Preprocessing and aggregation of raw trajectory samples into metrics.

Files
- TrajectoryPreprocessor.cs: Downsampling and smoothing utilities.
- FeatureExtractor.cs: Computes GlobalMetrics from processed samples.
- MetricsAggregator.cs: Pipeline helper that chains preprocessing + extraction.

Structure
- Raw samples -> preprocess -> extract -> GlobalMetrics
