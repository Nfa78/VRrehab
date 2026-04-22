# Api

Purpose
- HTTP client and URL construction for the adaptive rehab API.

Files
- AdaptiveApiClient.cs: Coroutine-based UnityWebRequest client for auth, patient, session, task, and metrics endpoints.
- AdaptiveApiResult.cs: Result wrappers for successful and failed API calls.
- AdaptiveApiRouter.cs: Builds full URLs from the auth base URL or API base URL.
- ApiRoute.cs: Minimal route object that stores segments and produces a safe path.
- AdaptiveApiRoutes.cs: Route factories for each endpoint.
- AdaptiveApiSegments.cs: Shared segment names to avoid hardcoded strings.
- AdaptiveApiSmokeTest.cs: Small debug flow that exercises signup/login, patient creation, session start, and task listing.

Structure
- Client -> Router -> Route -> Segments
