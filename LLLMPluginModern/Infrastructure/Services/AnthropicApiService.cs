using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Http;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    /// <summary>Anthropic Messages API with SSE streaming.</summary>
    public sealed class AnthropicApiService : LlmApiServiceBase
    {
        private const string ApiVersion = "2023-06-01";
        private const string FallbackBeta = "server-side-fallback-2026-07-01";
        private const int DefaultMaxTokens = 64000;
        private static readonly string[] KnownSuffixes = { "/messages", "/models" };

        // Models that accept server-side refusal fallbacks ("fallbacks": "default").
        private static readonly string[] FallbackModelPrefixes = { "claude-opus-5", "claude-fable-5", "claude-sonnet-5-5" };

        protected override string ProviderName => "Anthropic";

        private static string BaseUrl(ProviderProfile profile)
        {
            return Endpoints.NormalizeBase(profile?.Url, ProviderInfo.Get(ProviderIds.Anthropic).DefaultUrl, KnownSuffixes);
        }

        private static void Authorize(HttpRequestMessage message, ProviderProfile profile)
        {
            string key = (profile?.ApiKey ?? "").Trim();
            if (key.Length == 0)
                throw new LlmApiException("Enter your Anthropic API key in Settings.");
            message.Headers.Add("x-api-key", key);
            message.Headers.Add("anthropic-version", ApiVersion);
        }

        protected override HttpRequestMessage CreateChatRequest(LlmRequest request)
        {
            var profile = request.Profile;
            string model = RequireModel(profile, ProviderName);
            var uri = Endpoints.Combine(BaseUrl(profile), "messages");

            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = profile.MaxTokens ?? DefaultMaxTokens,
                ["messages"] = request.Messages.Select(m => new { role = m.Role == ChatRole.User ? "user" : "assistant", content = m.Content }).ToList(),
                ["stream"] = true
            };
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                body["system"] = request.SystemPrompt;
            if (profile.Temperature.HasValue)
                body["temperature"] = profile.Temperature.Value;

            bool useFallbacks = uri.Host.Equals("api.anthropic.com", StringComparison.OrdinalIgnoreCase) &&
                                FallbackModelPrefixes.Any(prefix => model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (useFallbacks)
                body["fallbacks"] = "default";

            var message = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent(body) };
            Authorize(message, profile);
            if (useFallbacks)
                message.Headers.Add("anthropic-beta", FallbackBeta);
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

            if (!TryParseJson(data, out var token) || !(token is JObject json))
                return;

            switch ((string)json["type"])
            {
                case "message_start":
                    state.InputTokens = (int?)json["message"]?["usage"]?["input_tokens"];
                    break;

                case "content_block_start":
                    if ((string)json["content_block"]?["type"] == "fallback")
                    {
                        // The server re-runs a declined request on a fallback model: drop the partial answer.
                        state.Reset();
                        state.Notice = "The request was declined by the selected model and answered by a fallback model.";
                    }
                    break;

                case "content_block_delta":
                    var delta = json["delta"] as JObject;
                    switch ((string)delta?["type"])
                    {
                        case "text_delta":
                            state.AppendText((string)delta["text"]);
                            break;
                        case "thinking_delta":
                            state.AppendReasoning((string)delta["thinking"]);
                            break;
                    }
                    break;

                case "message_delta":
                    state.OutputTokens = (int?)json["usage"]?["output_tokens"] ?? state.OutputTokens;
                    switch ((string)json["delta"]?["stop_reason"])
                    {
                        case "max_tokens":
                            state.Notice = "The answer was cut off at the max token limit (raise it in Settings).";
                            break;
                        case "refusal":
                            if (state.HasText)
                                state.Notice = "The model stopped because it declined to continue.";
                            else
                                state.Error = "The model declined this request. Rephrase it or choose another model.";
                            break;
                    }
                    break;

                case "message_stop":
                    state.Done = true;
                    break;

                case "error":
                    string type = (string)json["error"]?["type"];
                    string message = (string)json["error"]?["message"] ?? "Unknown error.";
                    state.Error = type == "overloaded_error"
                        ? "Anthropic is temporarily overloaded. Please try again in a moment."
                        : "Anthropic: " + message;
                    state.Done = true;
                    break;
            }
        }

        protected override void OnStreamEnd(StreamState state)
        {
            if (!state.HasText && state.Error == null && state.Unparsed.Length > 0)
                state.Error = "Anthropic sent an unexpected response: " + ExtractErrorMessage(state.Unparsed.ToString());
        }

        protected override HttpRequestMessage CreateListModelsRequest(ProviderProfile profile, string pageToken)
        {
            string relative = "models?limit=100" + (string.IsNullOrEmpty(pageToken) ? "" : "&after_id=" + Uri.EscapeDataString(pageToken));
            var message = new HttpRequestMessage(HttpMethod.Get, Endpoints.Combine(BaseUrl(profile), relative));
            Authorize(message, profile);
            return message;
        }

        protected override string ParseModels(JToken body, List<string> models)
        {
            if (body["data"] is JArray list)
                models.AddRange(list.Select(m => (string)m["id"]));
            return (bool?)body["has_more"] == true ? (string)body["last_id"] : null;
        }
    }
}
