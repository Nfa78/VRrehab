using System;
using System.Collections.Generic;

namespace AdaptiveSystem.Api
{
    public struct ApiRoute
    {
        private string[] _segments;

        private ApiRoute(string[] segments)
        {
            _segments = segments;
        }

        public static ApiRoute From(params string[] segments)
        {
            if (segments == null)
            {
                return new ApiRoute(new string[0]);
            }

            return new ApiRoute(segments);
        }

        public string ToPath()
        {
            if (_segments == null || _segments.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(_segments.Length);
            for (int i = 0; i < _segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_segments[i]))
                {
                    continue;
                }

                parts.Add(Uri.EscapeDataString(_segments[i]));
            }

            return "/" + string.Join("/", parts);
        }
    }
}
