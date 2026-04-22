using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class SessionStartRequest
    {
        public string device;
        public string version;
        public string start_time;
    }

    [Serializable]
    public class SessionStartResponse
    {
        public string session_id;
    }

    [Serializable]
    public class SessionEndRequest
    {
        public string end_time;
    }

    [Serializable]
    public class SessionEndResponse
    {
        public string session_id;
    }

    [Serializable]
    public class Session
    {
        public string session_id;
        public string patient_code;
        public string start_time;
        public string end_time;
        public string device;
        public string version;
        public string notes;
    }

    [Serializable]
    public class SessionsListResponse
    {
        public Session[] items;
    }
}
