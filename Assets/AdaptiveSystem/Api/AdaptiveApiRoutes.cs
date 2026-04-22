namespace AdaptiveSystem.Api
{
    public static class AdaptiveApiRoutes
    {
        public const string PasswordGrantQuery = "?grant_type=password";

        public static ApiRoute Health()
        {
            return ApiRoute.From(AdaptiveApiSegments.Health);
        }

        public static ApiRoute SignUp()
        {
            return ApiRoute.From(AdaptiveApiSegments.Signup);
        }

        public static ApiRoute Token()
        {
            return ApiRoute.From(AdaptiveApiSegments.Token);
        }

        public static ApiRoute SessionsStart()
        {
            return ApiRoute.From(AdaptiveApiSegments.Sessions, AdaptiveApiSegments.Start);
        }

        public static ApiRoute SessionsEnd(string sessionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.Sessions, sessionId, AdaptiveApiSegments.End);
        }

        public static ApiRoute SessionById(string sessionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.Sessions, sessionId);
        }

        public static ApiRoute SessionTasks(string sessionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.Sessions, sessionId, AdaptiveApiSegments.Tasks);
        }

        public static ApiRoute PatientMe()
        {
            return ApiRoute.From(AdaptiveApiSegments.Patients, AdaptiveApiSegments.Me);
        }

        public static ApiRoute PatientSessions()
        {
            return ApiRoute.From(AdaptiveApiSegments.Patients, AdaptiveApiSegments.Me, AdaptiveApiSegments.Sessions);
        }

        public static ApiRoute Tasks()
        {
            return ApiRoute.From(AdaptiveApiSegments.Tasks);
        }

        public static ApiRoute TaskExecutions()
        {
            return ApiRoute.From(AdaptiveApiSegments.TaskExecutions);
        }

        public static ApiRoute TaskMetrics(string taskExecutionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.TaskExecutions, taskExecutionId, AdaptiveApiSegments.Metrics);
        }

        public static ApiRoute TaskExecutionEnd(string taskExecutionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.TaskExecutions, taskExecutionId, AdaptiveApiSegments.End);
        }

        public static ApiRoute TaskExecutionById(string taskExecutionId)
        {
            return ApiRoute.From(AdaptiveApiSegments.TaskExecutions, taskExecutionId);
        }

        public static ApiRoute TaskLevels(string taskId)
        {
            return ApiRoute.From(AdaptiveApiSegments.Tasks, taskId, AdaptiveApiSegments.Levels);
        }

        public static ApiRoute TaskLevel(string taskId, int level)
        {
            return ApiRoute.From(AdaptiveApiSegments.Tasks, taskId, AdaptiveApiSegments.Levels, level.ToString());
        }
    }
}
