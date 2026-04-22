using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class ErrorResponse
    {
        public string message;
        public string code;
    }

    [Serializable]
    public class HealthResponse
    {
        public string status;
        public string time;
    }
}
