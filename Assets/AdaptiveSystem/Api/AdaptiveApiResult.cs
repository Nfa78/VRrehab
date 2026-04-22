using System;
using AdaptiveSystem.Models;

namespace AdaptiveSystem.Api
{
    [Serializable]
    public class ApiResult<T>
    {
        public bool is_success;
        public long status_code;
        public T data;
        public ErrorResponse error;
        public string transport_error;
        public string raw_text;

        public bool IsSuccess
        {
            get { return is_success; }
        }
    }

    [Serializable]
    public class AuthSignUpResult
    {
        public AuthSessionResponse session;
        public AuthUser user;

        public bool HasSession
        {
            get { return session != null && !string.IsNullOrEmpty(session.access_token); }
        }
    }
}
