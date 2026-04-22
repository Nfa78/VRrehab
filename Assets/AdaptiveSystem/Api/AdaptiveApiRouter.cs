using System;

namespace AdaptiveSystem.Api
{
    public sealed class AdaptiveApiRouter
    {
        private readonly string _baseUrl;

        public AdaptiveApiRouter(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/');
        }

        public string Build(ApiRoute route)
        {
            return Combine(_baseUrl, route.ToPath());
        }

        public string Build(string relativePath)
        {
            return Combine(_baseUrl, relativePath);
        }

        public static string Combine(string baseUrl, string relativePath)
        {
            string normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/');
            string normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath) ? string.Empty : relativePath.TrimStart('/');
            if (string.IsNullOrEmpty(normalizedRelativePath))
            {
                return normalizedBaseUrl;
            }

            return normalizedBaseUrl + "/" + normalizedRelativePath;
        }
    }
}
