using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Http;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    /// <summary>Ollama native API: streaming /api/chat (NDJSON) and /api/tags.</summary>
    public sealed class OllamaApiService : LlmApiServiceBase
    {
        private static readonly string[] KnownSuffixes = { "/api/generate", "/api/chat", "/api/tags", "/api", "/v1/chat/completions", "/v1" };

        protected override string ProviderName => "Ollama";

        protected override string ConnectionHint => "Is Ollama running? Start it from the Start menu or run \"ollama serve\".";

        protected override bool UsesThinkTags => true;

        internal static string BaseUrl(ProviderProfile profile)
        {
            return Endpoints.NormalizeBase(profile?.Url, ProviderInfo.Get(ProviderIds.Ollama).DefaultUrl, KnownSuffixes);
        }

        protected override HttpRequestMessage CreateChatRequest(LlmRequest request)
        {
            var profile = request.Profile;
            string model = RequireModel(profile, ProviderName);

            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                messages.Add(new { role = "system", content = request.SystemPrompt });
            messages.AddRange(request.Messages.Select(m => (object)new { role = m.Role == ChatRole.User ? "user" : "assistant", content = m.Content }));

            Dictionary<string, object> options = null;
            if (profile.Temperature.HasValue || profile.MaxTokens.HasValue)
            {
                options = new Dictionary<string, object>();
                if (profile.Temperature.HasValue)
                    options["temperature"] = profile.Temperature.Value;
                if (profile.MaxTokens.HasValue)
                    options["num_predict"] = profile.MaxTokens.Value;
            }

            return new HttpRequestMessage(HttpMethod.Post, Endpoints.Combine(BaseUrl(profile), "api/chat"))
            {
                Content = JsonContent(new { model, messages, stream = true, options })
            };
        }

        protected override void HandleStreamLine(string line, StreamState state)
        {
            if (!TryParseJson(line, out var token) || !(token is JObject json))
            {
                state.Unparsed.AppendLine(line);
                return;
            }

            string error = (string)json["error"];
            if (!string.IsNullOrEmpty(error))
            {
                state.Error = "Ollama: " + error.TrimEnd('.') + ".";
                if (error.IndexOf("not found", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    state.Error += $" Run \"ollama pull {state.Request.Profile.Model}\" or pick an installed model in Settings.";
                state.Done = true;
                return;
            }

            var message = json["message"] as JObject;
            state.AppendReasoning((string)message?["thinking"]);
            state.AppendText((string)message?["content"] ?? (string)json["response"]);

            if ((bool?)json["done"] == true)
            {
                state.InputTokens = (int?)json["prompt_eval_count"];
                state.OutputTokens = (int?)json["eval_count"];
                if ((string)json["done_reason"] == "length")
                    state.Notice = "The answer was cut off at the token limit.";
                state.Done = true;
            }
        }

        protected override void OnStreamEnd(StreamState state)
        {
            if (!state.HasText && state.Error == null && state.Unparsed.Length > 0)
                state.Error = "Ollama sent an unexpected response: " + ExtractErrorMessage(state.Unparsed.ToString());
        }

        protected override HttpRequestMessage CreateListModelsRequest(ProviderProfile profile, string pageToken)
        {
            return new HttpRequestMessage(HttpMethod.Get, Endpoints.Combine(BaseUrl(profile), "api/tags"));
        }

        protected override string ParseModels(JToken body, List<string> models)
        {
            if (body["models"] is JArray list)
                models.AddRange(list.Select(m => (string)m["name"] ?? (string)m["model"]));
            return null;
        }
    }
}
