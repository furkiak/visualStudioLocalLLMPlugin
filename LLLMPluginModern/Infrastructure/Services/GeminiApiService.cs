using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json.Linq;
namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class GeminiApiService : HttpLlmApiService
    {
        public GeminiApiService() { }
        public GeminiApiService(HttpClient client, TimeSpan? timeout = null) : base(client, timeout) { }
        protected override HttpRequestMessage CreateRequest(Uri endpoint, string input, LlmSettings settings)
        {
            var request = JsonRequest(endpoint, new { contents = new[] { new { parts = new[] { new { text = input } } } } });
            request.Headers.Add("x-goog-api-key", settings.ApiKey);
            return request;
        }
        protected override async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken token, IProgress<string> progress)
        {
            var json = await ReadJsonAsync(reader, token).ConfigureAwait(false);
            var candidate = json["candidates"]?[0];
            if (Text(candidate?["finishReason"]) != "STOP") throw new System.IO.InvalidDataException();
            var text = new System.Text.StringBuilder();
            foreach (var part in candidate?["content"]?["parts"] as JArray ?? new JArray())
                if (part.Value<bool?>("thought") != true) text.Append(Text(part["text"]));
            return text.ToString();
        }
    }
}
