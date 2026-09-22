using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class OllamaApiService : HttpLlmApiService
    {
        public OllamaApiService() { }
        public OllamaApiService(HttpClient client, TimeSpan? timeout = null) : base(client, timeout) { }
        protected override HttpRequestMessage CreateRequest(Uri endpoint, string input, LlmSettings settings)
        {
            var request = JsonRequest(endpoint, new { model = settings.Model, prompt = input, stream = true });
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
            return request;
        }
        protected override async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken token, IProgress<string> progress)
        {
            var result = new StringBuilder();
            int total = 0;
            var reportClock = Stopwatch.StartNew();
            bool reported = false;
            string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                token.ThrowIfCancellationRequested();
                total += line.Length + 1;
                if (total > MaxResponseCharacters || line.Length > 65536) throw new InvalidDataException();
                bool done = Consume(line, result);
                if (!reported || done || reportClock.ElapsedMilliseconds >= 100)
                {
                    progress?.Report(WithoutReasoning(result.ToString(), false));
                    reported = true;
                    reportClock.Restart();
                }
                if (done) return WithoutReasoning(result.ToString(), true);
            }
            throw new InvalidDataException();
        }
        private static bool Consume(string line, StringBuilder result)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var json = JObject.Parse(line);
            if (json["error"] != null) throw new InvalidDataException();
            if (json["response"] != null && json["response"].Type != JTokenType.String) throw new InvalidDataException();
            if (json["done"]?.Type != JTokenType.Boolean) throw new InvalidDataException();
            result.Append(Text(json["response"]));
            bool done = json.Value<bool>("done");
            if (done && Text(json["done_reason"]) == "length") throw new InvalidDataException();
            return done;
        }
        private static string WithoutReasoning(string text, bool final)
        {
            text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            int open = text.IndexOf("<think>", StringComparison.Ordinal);
            if (open >= 0)
            {
                if (final) throw new InvalidDataException();
                text = text.Substring(0, open);
            }
            if (!final)
            {
                int lastTag = text.LastIndexOf('<');
                if (lastTag >= 0 && "<think>".StartsWith(text.Substring(lastTag), StringComparison.Ordinal))
                    text = text.Substring(0, lastTag);
            }
            return text;
        }
    }
}
