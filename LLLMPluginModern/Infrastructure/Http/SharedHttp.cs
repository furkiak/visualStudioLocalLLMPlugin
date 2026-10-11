using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace LLLMPluginModern.Infrastructure.Http
{
    /// <summary>
    /// Long-lived HttpClient instances (one per process) so sockets are reused across requests.
    /// Loopback endpoints (Ollama, LM Studio…) bypass the system proxy, which corporate proxies
    /// would otherwise intercept.
    /// </summary>
    internal static class SharedHttp
    {
        private static readonly Lazy<HttpClient> Remote = new Lazy<HttpClient>(() => Create(useProxy: true), LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<HttpClient> Local = new Lazy<HttpClient>(() => Create(useProxy: false), LazyThreadSafetyMode.ExecutionAndPublication);

        public static HttpClient For(Uri uri)
        {
            return uri != null && uri.IsLoopback ? Local.Value : Remote.Value;
        }

        private static HttpClient Create(bool useProxy)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseProxy = useProxy
            };

            // Timeouts are enforced per request (idle timeout) so long generations are never cut off.
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LLLMPluginModern/4.0");
            return client;
        }
    }
}
