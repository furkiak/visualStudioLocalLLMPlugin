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
    /// <summary>Google Gemini API (streamGenerateContent with SSE). The key travels in a header, never in the URL.</summary>
    public sealed class GeminiApiService : LlmApiServiceBase
    {
        protected override string ProviderName => "Gemini";

        private static string BaseUrl(ProviderProfile profile)
        {
            string url = (profile?.Url ?? "").Trim();
            int models = url.IndexOf("/models", StringComparison.OrdinalIgnoreCase);
            if (models >= 0)
                url = url.Substring(0, models);
            return Endpoints.NormalizeBase(url, ProviderInfo.Get(ProviderIds.Gemini).DefaultUrl);
        }

        private static string ModelName(ProviderProfile profile)
        {
            string model = RequireModel(profile, "Gemini");
            return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? model.Substring("models/".Length) : model;
        }

        private static void Authorize(HttpRequestMessage message, ProviderProfile profile)
        {
            string key = (profile?.ApiKey ?? "").Trim();
            if (key.Length == 0)
                throw new LlmApiException("Enter your Gemini API key in Settings.");
            message.Headers.Add("x-goog-api-key", key);
        }

        protected override HttpRequestMessage CreateChatRequest(LlmRequest request)
        {
            var profile = request.Profile;
            string model = ModelName(profile);

            var body = new Dictionary<string, object>
            {
                ["contents"] = request.Messages.Select(m => new
                {
                    role = m.Role == ChatRole.User ? "user" : "model",
                    parts = new[] { new { text = m.Content } }
                }).ToList()
            };
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                body["systemInstruction"] = new { parts = new[] { new { text = request.SystemPrompt } } };

            if (profile.Temperature.HasValue || profile.MaxTokens.HasValue)
            {
                var config = new Dictionary<string, object>();
                if (profile.Temperature.HasValue)
                    config["temperature"] = profile.Temperature.Value;
                if (profile.MaxTokens.HasValue)
                    config["maxOutputTokens"] = profile.MaxTokens.Value;
                body["generationConfig"] = config;
            }

            var uri = Endpoints.Combine(BaseUrl(profile), "models/" + Uri.EscapeDataString(model) + ":streamGenerateContent?alt=sse");
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

            if (!TryParseJson(data, out var token) || !(token is JObject json))
                return;

            if (json["error"] is JObject)
            {
                state.Error = "Gemini: " + ExtractErrorMessage(data);
                state.Done = true;
                return;
            }

            string blockReason = (string)json["promptFeedback"]?["blockReason"];
            if (!string.IsNullOrEmpty(blockReason))
            {
                state.Error = $"Gemini blocked the request ({blockReason}).";
                state.Done = true;
                return;
            }

            var candidate = json["candidates"]?.FirstOrDefault() as JObject;
            if (candidate?["content"]?["parts"] is JArray parts)
            {
                foreach (var part in parts.OfType<JObject>())
                {
                    string text = (string)part["text"];
                    if ((bool?)part["thought"] == true)
                        state.AppendReasoning(text);
                    else
                        state.AppendText(text);
                }
            }

            string finish = (string)candidate?["finishReason"];
            if (finish == "MAX_TOKENS")
                state.Notice = "The answer was cut off at the token limit.";
            else if (!string.IsNullOrEmpty(finish) && finish != "STOP" && finish != "FINISH_REASON_UNSPECIFIED")
            {
                if (state.HasText)
                    state.Notice = $"Gemini stopped early ({finish}).";
                else
                    state.Error = $"Gemini did not return an answer ({finish}).";
            }

            var usage = json["usageMetadata"] as JObject;
            if (usage != null)
            {
                state.InputTokens = (int?)usage["promptTokenCount"] ?? state.InputTokens;
                state.OutputTokens = (int?)usage["candidatesTokenCount"] ?? state.OutputTokens;
            }
        }

        protected override void OnStreamEnd(StreamState state)
        {
            if (!state.HasText && state.Error == null && state.Unparsed.Length > 0)
                state.Error = "Gemini sent an unexpected response: " + ExtractErrorMessage(state.Unparsed.ToString());
        }

        protected override HttpRequestMessage CreateListModelsRequest(ProviderProfile profile, string pageToken)
        {
            string relative = "models?pageSize=1000" + (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken));
            var message = new HttpRequestMessage(HttpMethod.Get, Endpoints.Combine(BaseUrl(profile), relative));
            Authorize(message, profile);
            return message;
        }

        protected override string ParseModels(JToken body, List<string> models)
        {
            if (body["models"] is JArray list)
            {
                foreach (var model in list.OfType<JObject>())
                {
                    var methods = model["supportedGenerationMethods"] as JArray;
                    if (methods != null && !methods.Any(m => (string)m == "generateContent"))
                        continue;
                    string name = (string)model["name"] ?? "";
                    models.Add(name.StartsWith("models/", StringComparison.Ordinal) ? name.Substring("models/".Length) : name);
                }
            }
            return (string)body["nextPageToken"];
        }
    }
}
