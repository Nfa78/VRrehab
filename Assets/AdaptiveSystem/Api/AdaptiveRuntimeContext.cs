using System;
using System.Collections.Generic;
using AdaptiveSystem.Models;

namespace AdaptiveSystem.Api
{
    public static class AdaptiveRuntimeContext
    {
        private static readonly Dictionary<string, int> TaskDifficultyLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        private static AuthSessionResponse _authSession = new AuthSessionResponse();

        public static string ActiveSessionId { get; private set; } = string.Empty;

        public static string PatientCode { get; private set; } = string.Empty;

        public static string DominantHand { get; private set; } = string.Empty;

        public static string PatientNotes { get; private set; } = string.Empty;

        public static bool HasAuthSession
        {
            get { return _authSession != null && !string.IsNullOrWhiteSpace(_authSession.access_token); }
        }

        public static void SetAuthSession(AuthSessionResponse session)
        {
            _authSession = CloneAuthSession(session) ?? new AuthSessionResponse();
        }

        public static AuthSessionResponse GetAuthSessionCopy()
        {
            return CloneAuthSession(_authSession) ?? new AuthSessionResponse();
        }

        public static void ClearAuthSession()
        {
            _authSession = new AuthSessionResponse();
            ActiveSessionId = string.Empty;
            PatientCode = string.Empty;
            DominantHand = string.Empty;
            PatientNotes = string.Empty;
            TaskDifficultyLevels.Clear();
        }

        public static void SetActiveSessionId(string sessionId)
        {
            ActiveSessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId.Trim();
        }

        public static void ClearActiveSession()
        {
            ActiveSessionId = string.Empty;
        }

        public static void SetPatientProfile(string patientCode, string dominantHand, string patientNotes)
        {
            PatientCode = string.IsNullOrWhiteSpace(patientCode) ? string.Empty : patientCode.Trim();
            DominantHand = string.IsNullOrWhiteSpace(dominantHand) ? string.Empty : dominantHand.Trim();
            PatientNotes = patientNotes ?? string.Empty;
        }

        public static void SetTaskDifficulty(string taskId, int difficultyLevel)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            TaskDifficultyLevels[taskId] = Math.Max(1, difficultyLevel);
        }

        public static bool TryGetTaskDifficulty(string taskId, out int difficultyLevel)
        {
            if (!string.IsNullOrWhiteSpace(taskId) && TaskDifficultyLevels.TryGetValue(taskId, out int storedDifficultyLevel))
            {
                difficultyLevel = Math.Max(1, storedDifficultyLevel);
                return true;
            }

            difficultyLevel = 1;
            return false;
        }

        public static string ResolveAuthenticatedEmail()
        {
            return _authSession != null &&
                   _authSession.user != null &&
                   !string.IsNullOrWhiteSpace(_authSession.user.email)
                ? _authSession.user.email
                : string.Empty;
        }

        private static AuthSessionResponse CloneAuthSession(AuthSessionResponse session)
        {
            if (session == null)
            {
                return null;
            }

            return new AuthSessionResponse
            {
                access_token = session.access_token,
                token_type = session.token_type,
                expires_in = session.expires_in,
                expires_at = session.expires_at,
                refresh_token = session.refresh_token,
                user = CloneAuthUser(session.user)
            };
        }

        private static AuthUser CloneAuthUser(AuthUser user)
        {
            if (user == null)
            {
                return null;
            }

            return new AuthUser
            {
                id = user.id,
                email = user.email,
                user_metadata = user.user_metadata != null ? new AuthUserMetadata() : null
            };
        }
    }
}
