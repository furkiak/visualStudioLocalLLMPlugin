using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json.Linq;
namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class AnthropicApiService : HttpLlmApiService
    {
        public AnthropicApiService() { }
        public AnthropicApiService(HttpClient client, TimeSpan? timeout = null) : base(client, timeout) { }
        protected override HttpRequestMessage CreateRequest(Uri endpoint, string input, LlmSettings settings)
        {
            var request = JsonRequest(endpoint, new { model = settings.Model, max_tokens = 4096, messages = new[] { new { role = "user", content = input } } });
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            return request;
        }
        protected override async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken token, IProgress<string> progress)
        {
            var json = await ReadJsonAsync(reader, token).ConfigureAwait(false);
            if (Text(json["stop_reason"]) != "end_turn") throw new System.IO.InvalidDataException();
            var text = new System.Text.StringBuilder();
            foreach (var part in json["content"] as JArray ?? new JArray())
                if (Text(part["type"]) == "text") text.Append(Text(part["text"]));
            return text.ToString();
        }
    }
}
