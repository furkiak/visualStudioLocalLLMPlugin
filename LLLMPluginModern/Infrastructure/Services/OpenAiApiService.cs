using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Http;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    /// <summary>
    /// OpenAI Chat Completions with SSE streaming. Also serves every OpenAI-compatible
    /// server (LM Studio, llama.cpp, vLLM, Jan, LocalAI…) when <c>compatibleMode</c> is set.
    /// </summary>
    public sealed class OpenAiApiService : LlmApiServiceBase
    {
        private static readonly string[] KnownSuffixes = { "/chat/completions", "/completions", "/responses", "/models" };
        private static readonly string[] NonChatModelMarkers =
        {
            "embedding", "whisper", "tts", "dall-e", "davinci", "babbage", "moderation", "image", "audio",
            "realtime", "transcribe", "search", "computer-use", "sora"
        };

        private readonly bool _compatibleMode;

        public OpenAiApiService(bool compatibleMode = false)
        {
            _compatibleMode = compatibleMode;
        }

        protected override string ProviderName => _compatibleMode ? "The OpenAI-compatible server" : "OpenAI";

        protected override string ConnectionHint => _compatibleMode
            ? "Is the local server running and is its API server enabled (LM Studio: Developer ▸ Start Server)?"
            : "Check the endpoint URL and your network connection.";

        protected override bool UsesThinkTags => _compatibleMode;

        private string BaseUrl(ProviderProfile profile)
        {
            string defaultUrl = ProviderInfo.Get(_compatibleMode ? ProviderIds.OpenAICompatible : ProviderIds.OpenAI).DefaultUrl;
            return Endpoints.NormalizeBase(profile?.Url, defaultUrl, KnownSuffixes);
        }

        private bool IsOfficialEndpoint(Uri uri) =>
            uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);

        private void Authorize(HttpRequestMessage message, ProviderProfile profile)
        {
            string key = (profile?.ApiKey ?? "").Trim();
            if (key.Length > 0)
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            else if (!_compatibleMode)
                throw new LlmApiException("Enter your OpenAI API key in Settings.");
        }

        protected override HttpRequestMessage CreateChatRequest(LlmRequest request)
        {
            var profile = request.Profile;
            string model = RequireModel(profile, ProviderName);
            var uri = Endpoints.Combine(BaseUrl(profile), "chat/completions");
            bool official = IsOfficialEndpoint(uri);

            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                messages.Add(new { role = "system", content = request.SystemPrompt });
            messages.AddRange(request.Messages.Select(m => (object)new { role = m.Role == ChatRole.User ? "user" : "assistant", content = m.Content }));

            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = true
            };
            if (official)
                body["stream_options"] = new { include_usage = true };
            if (profile.Temperature.HasValue)
                body["temperature"] = profile.Temperature.Value;
            if (profile.MaxTokens.HasValue)
                body[official ? "max_completion_tokens" : "max_tokens"] = profile.MaxTokens.Value;

            var message = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent(body) };
            Authorize(message, profile);
            return message;
        }

        protected override void HandleStreamLine(string line, StreamState state)
        {
            string data = SseData(line);
            if (data == null)
            {
                if (!IsSseControlLine(line))
                    state.Unparsed.AppendLine(line);
                return;
            }

            if (data == "[DONE]")
            {
                state.Done = true;
                return;
            }

            if (!TryParseJson(data, out var token) || !(token is JObject json))
                return;

            if (json["error"] != null && json["error"].Type != JTokenType.Null)
            {
                state.Error = $"{ProviderName}: {ExtractErrorMessage(data)}";
                state.Done = true;
                return;
            }

            var choice = json["choices"]?.FirstOrDefault() as JObject;
            var delta = choice?["delta"] as JObject;
            if (delta != null)
            {
                state.AppendReasoning((string)delta["reasoning_content"] ?? (string)delta["reasoning"]);
                state.AppendText((string)delta["content"]);
            }

            switch ((string)choice?["finish_reason"])
            {
                case "length":
                    state.Notice = "The answer was cut off at the token limit.";
                    break;
                case "content_filter":
                    state.Notice = "Part of the answer was removed by the provider's content filter.";
                    break;
            }

            var usage = json["usage"];
            if (usage != null && usage.Type == JTokenType.Object)
            {
                state.InputTokens = (int?)usage["prompt_tokens"];
                state.OutputTokens = (int?)usage["completion_tokens"];
            }
        }

        protected override void OnStreamEnd(StreamState state)
        {
            if (state.HasText || state.Error != null || state.Unparsed.Length == 0)
                return;

            // Some proxies ignore "stream": true and return a regular completion body.
            string body = state.Unparsed.ToString();
            if (TryParseJson(body, out var token) && token is JObject json)
            {
                string content = ((json["choices"]?.FirstOrDefault() as JObject)?["message"] as JObject)?["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                {
                    state.AppendText(content);
                    return;
                }
            }
            state.Error = $"{ProviderName} sent an unexpected response: {ExtractErrorMessage(body)}";
        }

        protected override HttpRequestMessage CreateListModelsRequest(ProviderProfile profile, string pageToken)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, Endpoints.Combine(BaseUrl(profile), "models"));
            Authorize(message, profile);
            return message;
        }

        protected override string ParseModels(JToken body, List<string> models)
        {
            if (body["data"] is JArray list)
            {
                var ids = list.Select(m => (string)m["id"]).Where(id => id != null);
                if (!_compatibleMode)
                    ids = ids.Where(id => !NonChatModelMarkers.Any(marker => id.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0));
                models.AddRange(ids);
            }
            return null;
        }
    }
}
