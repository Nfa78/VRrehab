# Models

Purpose
- Request/response DTOs and entity models that mirror the OpenAPI spec.

Files
- AuthModels.cs: Supabase email/password auth requests and responses.
- MetricsModels.cs: GlobalMetrics and SceneMetric.
- PatientModels.cs: Patient profile requests and responses.
- SessionModels.cs: Session start/end requests and session entities.
- TaskModels.cs: Task start/end requests, task metrics, and task entities.
- CommonModels.cs: ErrorResponse and HealthResponse.
- ModelConstants.cs: Shared enums as string constants (decision/method values).

Structure
- Each class is [Serializable] for JsonUtility.
