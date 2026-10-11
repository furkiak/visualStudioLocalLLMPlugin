using System;
using System.Collections.Generic;
using System.Linq;

namespace LLLMPluginModern.Core.Models
{
    /// <summary>Stable provider identifiers. These strings are persisted in settings.json.</summary>
    public static class ProviderIds
    {
        public const string Ollama = "Ollama";
        public const string OpenAI = "OpenAI";
        public const string OpenAICompatible = "OpenAICompatible";
        public const string Anthropic = "Anthropic";
        public const string Gemini = "Gemini";
    }

    /// <summary>Static description of an AI provider: display name and sensible defaults.</summary>
    public sealed class ProviderInfo
    {
        private ProviderInfo(string id, string displayName, string defaultUrl, string defaultModel, bool requiresApiKey, string hint)
        {
            Id = id;
            DisplayName = displayName;
            DefaultUrl = defaultUrl;
            DefaultModel = defaultModel;
            RequiresApiKey = requiresApiKey;
            Hint = hint;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string DefaultUrl { get; }
        public string DefaultModel { get; }
        public bool RequiresApiKey { get; }
        public string Hint { get; }

        public override string ToString() => DisplayName;

        public static IReadOnlyList<ProviderInfo> All { get; } = new[]
        {
            new ProviderInfo(ProviderIds.Ollama, "Ollama (local)", "http://localhost:11434", "qwen2.5-coder:7b", false,
                "Runs models on your machine. Start Ollama, then press Refresh to list installed models."),
            new ProviderInfo(ProviderIds.OpenAICompatible, "LM Studio / OpenAI-compatible (local)", "http://localhost:1234/v1", "", false,
                "Any server that speaks the OpenAI Chat Completions API: LM Studio, llama.cpp, vLLM, Jan, LocalAI…"),
            new ProviderInfo(ProviderIds.OpenAI, "OpenAI", "https://api.openai.com/v1", "gpt-5.5", true,
                "Requires an API key from platform.openai.com."),
            new ProviderInfo(ProviderIds.Anthropic, "Anthropic", "https://api.anthropic.com/v1", "claude-opus-5-5", true,
                "Requires an API key from console.anthropic.com."),
            new ProviderInfo(ProviderIds.Gemini, "Google Gemini", "https://generativelanguage.googleapis.com/v1beta", "gemini-flash-latest", true,
                "Requires an API key from aistudio.google.com."),
        };

        public static ProviderInfo Get(string id)
        {
            return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
        }
    }
}
