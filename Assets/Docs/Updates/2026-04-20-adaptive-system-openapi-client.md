# AdaptiveSystem OpenAPI Client Update

Date: 2026-04-20

## Summary

This update replaces the legacy `AdaptiveSystem` Unity API surface with a coroutine-based client that follows `Assets/AdaptiveSystem/openapi.yaml` as the source of truth.

The previous implementation targeted an older backend shape. In particular, it still used task routes under `/tasks/...`, expected task start payloads containing `scene` and `task_type`, and included `patient_code` in session start requests. The OpenAPI contract in `Assets/AdaptiveSystem/openapi.yaml` defines a different model:

- auth is split between Supabase Auth endpoints and application API endpoints
- patient profile access is under `/patients/me`
- task execution is a first-class resource under `/task-executions`
- session start resolves the patient from the authenticated JWT

The code was updated to match that contract directly, without code generation.

## Contract Source Used

Primary source:

- `Assets/AdaptiveSystem/openapi.yaml`

Implementation rule followed:

- OpenAPI contract was treated as authoritative for routes, DTO field names, request/response shapes, and expected runtime flow.

Important limitation during implementation:

- There is no `supabase/functions/api/*` source tree in this workspace, so no backend code cross-check was possible locally.

## Files Added

### API

- `Assets/AdaptiveSystem/Api/AdaptiveApiResult.cs`
- `Assets/AdaptiveSystem/Api/AdaptiveApiSmokeTest.cs`

### Models

- `Assets/AdaptiveSystem/Models/AuthModels.cs`
- `Assets/AdaptiveSystem/Models/PatientModels.cs`

### Unity Asset Metadata

- `Assets/Docs.meta`
- `Assets/Docs/Updates.meta`
- `Assets/AdaptiveSystem/Api/AdaptiveApiResult.cs.meta`
- `Assets/AdaptiveSystem/Api/AdaptiveApiSmokeTest.cs.meta`
- `Assets/AdaptiveSystem/Models/AuthModels.cs.meta`
- `Assets/AdaptiveSystem/Models/PatientModels.cs.meta`

## Files Updated

### API

- `Assets/AdaptiveSystem/Api/AdaptiveApiClient.cs`
- `Assets/AdaptiveSystem/Api/AdaptiveApiRouter.cs`
- `Assets/AdaptiveSystem/Api/AdaptiveApiRoutes.cs`
- `Assets/AdaptiveSystem/Api/AdaptiveApiSegments.cs`
- `Assets/AdaptiveSystem/Api/README.md`

### Models

- `Assets/AdaptiveSystem/Models/SessionModels.cs`
- `Assets/AdaptiveSystem/Models/TaskModels.cs`
- `Assets/AdaptiveSystem/Models/README.md`

### Documentation

- `Assets/AdaptiveSystem/README.md`

## Detailed Change Log

### 1. Rebuilt the API client around the OpenAPI contract

`Assets/AdaptiveSystem/Api/AdaptiveApiClient.cs` was completely replaced.

The previous client:

- had one `baseUrl`
- had an optional `functionPrefix`
- only supported a bearer token header
- exposed a small set of legacy methods:
  - health
  - start session
  - end session
  - start task
  - submit task metrics
  - end task

The new client now:

- uses two explicit base URLs:
  - auth base URL: `http://127.0.0.1:54421/auth/v1`
  - API base URL: `http://127.0.0.1:54421/functions/v1/api`
- centralizes publishable key storage
- centralizes auth session storage
- centralizes request construction and header selection
- centralizes non-2xx parsing into `ErrorResponse`
- returns results through a structured wrapper: `ApiResult<T>`
- stays coroutine-based and uses `UnityWebRequest`

The new client exposes the requested method set:

1. `HealthAsync()`
2. `SignUpAsync(email, password)`
3. `SignInAsync(email, password)`
4. `GetMyPatientAsync()`
5. `PutMyPatientAsync(patient_code, dominant_hand, notes)`
6. `PatchMyPatientAsync(...)`
7. `StartSessionAsync(device, version, start_time)`
8. `GetSessionAsync(session_id)`
9. `ListMySessionsAsync()`
10. `ListTasksAsync()`
11. `ListTaskLevelsAsync(task_id)`
12. `GetTaskLevelAsync(task_id, difficulty_level)`
13. `StartTaskExecutionAsync(session_id, task_id, difficulty_level, start_time)`
14. `GetTaskExecutionAsync(task_execution_id)`
15. `SubmitTaskMetricsAsync(task_execution_id, TaskMetricsRequest)`
16. `GetTaskMetricsAsync(task_execution_id)`
17. `EndTaskExecutionAsync(task_execution_id, end_time)`
18. `EndSessionAsync(session_id, end_time)`

### 2. Added explicit request scopes and header rules

The new client uses internal request scopes:

- `PublicApi`
- `Auth`
- `PrivateApi`

These scopes control base URL and headers:

#### Public API requests

Used for:

- `GET /health`

Headers:

- no auth required
- no `apikey` required according to the contract

#### Auth requests

Used for:

- `POST /signup`
- `POST /token?grant_type=password`

Headers:

- `apikey: <publishable_key>`
- `Content-Type: application/json`

#### Private API requests

Used for all authenticated application endpoints.

Headers:

- `Authorization: Bearer <access_token>`
- `apikey: <publishable_key>`
- `Content-Type: application/json` when a body is present

### 3. Centralized auth session storage

`AdaptiveApiClient` now stores:

- `access_token`
- `refresh_token`
- `token_type`
- `expires_in`
- `expires_at`
- `user`

Exposed properties:

- `AccessToken`
- `RefreshToken`
- `TokenType`
- `ExpiresIn`
- `ExpiresAt`
- `HasAccessToken`

There is also a `ClearAuthSession()` method.

Behavior:

- `SignInAsync()` stores the returned session automatically
- `SignUpAsync()` stores the returned session automatically if signup returns an authenticated session payload in local development

### 4. Added structured API result wrappers

`Assets/AdaptiveSystem/Api/AdaptiveApiResult.cs` introduces:

- `ApiResult<T>`
- `AuthSignUpResult`

`ApiResult<T>` contains:

- `is_success`
- `status_code`
- `data`
- `error`
- `transport_error`
- `raw_text`

This was added so callers can distinguish:

- successful typed responses
- parsed API error bodies
- low-level transport failures
- raw response bodies when needed for debugging

### 5. Implemented contract-auth models

`Assets/AdaptiveSystem/Models/AuthModels.cs` was added for:

- `AuthSignUpRequest`
- `AuthSignInRequest`
- `AuthSessionResponse`
- `AuthUser`

Notes:

- field names were preserved exactly as wire-format JSON names
- `AuthUser.user_metadata` is represented as `AuthUserMetadata`, an empty placeholder type

Reason for the placeholder:

- Unity `JsonUtility` does not support arbitrary object/dictionary payloads well
- the OpenAPI contract describes `user_metadata` as an open object with `additionalProperties: true`
- the client currently preserves the field shape at the DTO level, but not arbitrary nested metadata contents

### 6. Implemented patient models

`Assets/AdaptiveSystem/Models/PatientModels.cs` was added for:

- `PatientUpsertRequest`
- `PatientPatchRequest`
- `PatientProfile`

This aligns Unity DTOs with the `/patients/me` contract instead of the old patient-code-in-path approach.

### 7. Corrected session DTOs

`Assets/AdaptiveSystem/Models/SessionModels.cs` was updated.

Key change:

- removed `patient_code` from `SessionStartRequest`

Reason:

- the OpenAPI contract says the backend resolves the patient from the authenticated JWT-linked profile
- `POST /sessions/start` only requires `start_time`
- `device` and `version` remain optional fields in the request

Other session DTOs were already broadly aligned and were retained:

- `SessionStartResponse`
- `SessionEndRequest`
- `SessionEndResponse`
- `Session`
- `SessionsListResponse`

### 8. Corrected task DTOs

`Assets/AdaptiveSystem/Models/TaskModels.cs` was updated substantially.

#### Task start request

Old shape:

- `session_id`
- `scene`
- `task_type`
- `difficulty_level`
- `target_size`
- `reach_distance`
- `task_complexity`
- `start_time`

New contract shape:

- `session_id`
- `task_id`
- `difficulty_level`
- `start_time`

#### Task start response

Old shape:

- `task_id`
- `timeout_seconds`
- `expected_time_seconds`

New contract shape:

- `task_execution_id`
- `task_level_id`
- `timeout_seconds`
- `expected_time_seconds`

#### Task end response

Old shape:

- `task_id`

New contract shape:

- `task_execution_id`

#### Task level

Added missing fields required by the contract:

- `target_size`
- `reach_distance`
- `task_complexity`

#### Task execution

Added missing contract field:

- `task_execution_id`

This is important because the backend distinguishes:

- task definition identity: `task_id`
- execution instance identity: `task_execution_id`

### 9. Reworked route definitions to match the OpenAPI endpoints

`Assets/AdaptiveSystem/Api/AdaptiveApiRoutes.cs` and `AdaptiveApiSegments.cs` were updated.

Added segment constants:

- `signup`
- `token`
- `me`
- `task-executions`

Added route factories:

- `SignUp()`
- `Token()`
- `PatientMe()`
- `TaskExecutions()`
- `TaskExecutionById(taskExecutionId)`
- `TaskExecutionEnd(taskExecutionId)`

Changed route behavior:

- patient sessions now route to `/patients/me/sessions`
- task metrics now route to `/task-executions/{task_execution_id}/metrics`
- task end now routes to `/task-executions/{task_execution_id}/end`
- task creation now routes to `/task-executions`

Added:

- `PasswordGrantQuery = "?grant_type=password"`

Used for:

- `POST /token?grant_type=password`

### 10. Simplified URL building

`Assets/AdaptiveSystem/Api/AdaptiveApiRouter.cs` was simplified.

Old behavior:

- combined one base URL with an optional function prefix

New behavior:

- combines a selected base URL with a relative path
- exposes `Combine(baseUrl, relativePath)` for centralized request URL resolution

Reason:

- the contract now needs two explicit upstreams
- auth endpoints and API endpoints are no longer cleanly represented by a single base URL plus function prefix

### 11. Added signup `oneOf` handling

The OpenAPI contract defines signup response as:

- either `AuthUser`
- or `AuthSessionResponse`

`AdaptiveApiClient.SignUpAsync()` handles this by:

1. sending the request to `/signup`
2. checking for non-2xx responses and parsing `ErrorResponse` where possible
3. attempting to deserialize to `AuthSessionResponse`
4. if that does not produce an authenticated payload, attempting to deserialize to `AuthUser`
5. wrapping the outcome in `AuthSignUpResult`

Why this was needed:

- the local development environment may return an authenticated session immediately
- a different environment may return a user-only payload if email confirmation is enabled later

### 12. Added error parsing and transport failure handling

The old client only surfaced `request.error` for failures.

The new client:

- checks HTTP success by status code
- keeps raw response text
- attempts to parse error bodies into `ErrorResponse`
- separately keeps low-level transport failures in `transport_error`

This is especially useful for contract-defined non-200 cases such as:

- `400`
- `404`
- `409`

Examples from the contract:

- `GET /patients/me` may return `404`
- `GET /task-executions/{task_execution_id}/metrics` may return `404`
- `POST /task-executions/{task_execution_id}/metrics` may return `409` when scene metrics already exist

### 13. Added a smoke-test MonoBehaviour

`Assets/AdaptiveSystem/Api/AdaptiveApiSmokeTest.cs` was added as a small integration/debug helper.

Flow implemented:

1. optional signup
2. fallback sign-in if needed
3. `PUT /patients/me`
4. `POST /sessions/start`
5. `GET /tasks`

Purpose:

- quickly validate local Supabase configuration from the Unity side
- separate backend/data availability problems from client integration problems

This matches the requested minimal integration flow.

### 14. Updated module documentation

`Assets/AdaptiveSystem/README.md` was updated to describe the correct modern runtime flow:

1. sign up or sign in
2. create/replace patient profile
3. start session
4. start task execution
5. submit task metrics
6. end task execution and session

The example code in the README was also changed to use:

- `StartTaskExecutionAsync(...)`
- `SubmitTaskMetricsAsync(...)`
- `task_execution_id`

`Assets/AdaptiveSystem/Api/README.md` and `Assets/AdaptiveSystem/Models/README.md` were updated so the package documentation matches the new file layout and responsibilities.

## Contract Alignment Details

### Endpoints now represented in the client

Public:

- `GET /health`

Supabase Auth:

- `POST /signup`
- `POST /token?grant_type=password`

Authenticated application API:

- `GET /patients/me`
- `PUT /patients/me`
- `PATCH /patients/me`
- `POST /sessions/start`
- `GET /sessions/{session_id}`
- `GET /patients/me/sessions`
- `GET /tasks`
- `GET /tasks/{task_id}/levels`
- `GET /tasks/{task_id}/levels/{difficulty_level}`
- `POST /task-executions`
- `GET /task-executions/{task_execution_id}`
- `POST /task-executions/{task_execution_id}/metrics`
- `GET /task-executions/{task_execution_id}/metrics`
- `POST /task-executions/{task_execution_id}/end`
- `POST /sessions/{session_id}/end`

### DTOs represented in the client

Implemented:

- `AuthSignUpRequest`
- `AuthSignInRequest`
- `AuthSessionResponse`
- `AuthUser`
- `PatientUpsertRequest`
- `PatientPatchRequest`
- `PatientProfile`
- `SessionStartRequest`
- `SessionStartResponse`
- `SessionEndRequest`
- `SessionEndResponse`
- `TaskStartRequest`
- `TaskStartResponse`
- `TaskExecution`
- `TaskDefinition`
- `TaskLevel`
- `TaskMetricsRequest`
- `GlobalMetrics`
- `SceneMetric`
- `TaskMetricsResponse`
- `TaskMetricsGetResponse`
- `Session`
- `TaskListResponse`
- `TaskLevelsResponse`
- `SessionTasksResponse`
- `SessionsListResponse`
- `ErrorResponse`
- `HealthResponse`

## Assumptions and Known Limitations

### 1. OpenAPI was treated as the backend source of truth

This was intentional and matches the implementation request.

Consequence:

- if backend runtime behavior diverges from `Assets/AdaptiveSystem/openapi.yaml`, the Unity client now follows the contract rather than the older Unity-side assumptions

### 2. No local backend source verification was possible

No `supabase/functions/api/*` path exists in this workspace.

Consequence:

- ambiguous backend implementation details could not be cross-checked locally

### 3. `JsonUtility` remains the serializer

This was preserved to stay aligned with the existing Unity project style.

Benefits:

- no new serialization dependency
- consistent with the existing codebase

Tradeoffs:

- arbitrary JSON objects are not modeled well
- `AuthUser.user_metadata` cannot be faithfully represented as a dynamic object with the current serializer choice

### 4. Signup result is modeled with a wrapper

The contract defines a `oneOf` response for signup.

Because Unity `JsonUtility` does not natively support discriminated or polymorphic `oneOf` parsing, signup is surfaced as:

- `ApiResult<AuthSignUpResult>`

where `AuthSignUpResult` can contain:

- `session`
- `user`

This is accurate behaviorally, but it is not a direct generated mirror of `AuthSignUpResponse` because generation was explicitly not used.

### 5. Refresh-token flow was not implemented

The request required:

- central token storage
- saving `access_token`, `refresh_token`, `expires_in`, `expires_at`

That is implemented.

Not implemented:

- automatic refresh token exchange
- token expiry checks
- silent session renewal

### 6. Full compile verification was not run from this shell session

Source-level verification was performed by:

- checking route and DTO coverage
- removing stale legacy route usage inside `Assets/AdaptiveSystem`
- confirming the requested coroutine methods exist

Not performed here:

- Unity editor compile confirmation after reimport
- runtime backend integration against a live local Supabase stack

Reason:

- Unity-generated project files in the shell session do not automatically include newly created scripts until Unity regenerates them

## Recommended Manual Validation Steps

After Unity reimports assets:

1. open the project and allow script recompilation
2. assign `publishableKey` on `AdaptiveApiClient`
3. keep default base URLs if using the local Supabase stack
4. attach `AdaptiveApiSmokeTest` to a test GameObject
5. assign the `AdaptiveApiClient` reference
6. run the smoke test via the inspector context menu

Expected flow:

- signup may succeed with either a user payload or a session payload
- if signup does not authenticate, sign-in should authenticate
- patient creation should succeed with `PUT /patients/me`
- session start should return `session_id`
- task listing may return zero tasks if seed data is missing

Important interpretation:

- an empty `GET /tasks` response is a backend seed-data problem, not necessarily a Unity client bug

## Final Outcome

The `AdaptiveSystem` networking layer now reflects the OpenAPI contract under `Assets/AdaptiveSystem/openapi.yaml` much more closely than the previous implementation.

The main architectural differences from the old client are:

- split auth/API upstreams
- explicit patient profile endpoints under `/patients/me`
- task execution as its own resource
- centralized token and header handling
- structured success/error result objects
- contract-aligned DTOs and route factories

This update should be considered a manual OpenAPI contract migration of the Unity client, not a minor incremental patch.
