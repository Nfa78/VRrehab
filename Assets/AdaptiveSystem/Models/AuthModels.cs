using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class AuthSignUpRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class AuthSignInRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class AuthSessionResponse
    {
        public string access_token;
        public string token_type;
        public int expires_in;
        public int expires_at;
        public string refresh_token;
        public AuthUser user;
    }

    [Serializable]
    public class AuthUser
    {
        public string id;
        public string email;
        public AuthUserMetadata user_metadata;
    }

    [Serializable]
    public class AuthUserMetadata
    {
    }
}
