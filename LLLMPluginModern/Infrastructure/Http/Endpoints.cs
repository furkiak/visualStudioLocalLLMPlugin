using System;
using LLLMPluginModern.Core.Interfaces;

namespace LLLMPluginModern.Infrastructure.Http
{
    internal static class Endpoints
    {
        /// <summary>
        /// Turns whatever the user typed (a base URL or a full endpoint such as ".../api/generate")
        /// into a base URL without a trailing slash.
        /// </summary>
        public static string NormalizeBase(string url, string defaultUrl, params string[] knownSuffixes)
        {
            string value = (url ?? "").Trim();
            if (value.Length == 0)
                value = defaultUrl;

            int query = value.IndexOf('?');
            if (query >= 0)
                value = value.Substring(0, query);

            value = value.TrimEnd('/');
            bool stripped;
            do
            {
                stripped = false;
                foreach (var suffix in knownSuffixes)
                {
                    if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(0, value.Length - suffix.Length).TrimEnd('/');
                        stripped = true;
                    }
                }
            }
            while (stripped);

            return value;
        }

        public static Uri Combine(string baseUrl, string relative)
        {
            string url = baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new LlmApiException($"The endpoint URL \"{baseUrl}\" is not a valid http(s) address. Fix it in Settings.");
            return uri;
        }
    }
}
