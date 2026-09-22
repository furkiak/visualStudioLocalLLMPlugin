using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json.Linq;
namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class OpenAiApiService : HttpLlmApiService
    {
        public OpenAiApiService() { }
        public OpenAiApiService(HttpClient client, TimeSpan? timeout = null) : base(client, timeout) { }
        protected override HttpRequestMessage CreateRequest(Uri endpoint, string input, LlmSettings settings)
        {
            var request = JsonRequest(endpoint, new { model = settings.Model, messages = new[] { new { role = "user", content = input } } });
            if (!string.IsNullOrWhiteSpace(settings.ApiKey)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
            return request;
        }
        protected override async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken token, IProgress<string> progress)
        {
            var json = await ReadJsonAsync(reader, token).ConfigureAwait(false);
            if (Text(json["choices"]?[0]?["finish_reason"]) != "stop") throw new System.IO.InvalidDataException();
            return Text(json["choices"]?[0]?["message"]?["content"]);
        }
    }
}
