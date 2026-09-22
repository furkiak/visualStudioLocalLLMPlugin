using System;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Infrastructure.Services;

namespace LLLMPluginModern.Infrastructure.Factories
{
    public class LlmApiServiceFactory : ILlmApiServiceFactory
    {
        private readonly OllamaApiService _ollamaService;
        private readonly GeminiApiService _geminiService;
        private readonly OpenAiApiService _openAiService;
        private readonly AnthropicApiService _anthropicService;

        public LlmApiServiceFactory(
            OllamaApiService ollamaService,
            GeminiApiService geminiService,
            OpenAiApiService openAiService,
            AnthropicApiService anthropicService)
        {
            _ollamaService = ollamaService;
            _geminiService = geminiService;
            _openAiService = openAiService;
            _anthropicService = anthropicService;
        }

        public ILlmApiService Create(string providerName)
        {
            if (string.Equals(providerName, "Gemini", StringComparison.OrdinalIgnoreCase))
                return _geminiService;

            if (string.Equals(providerName, "OpenAI", StringComparison.OrdinalIgnoreCase))
                return _openAiService;

            if (string.Equals(providerName, "Anthropic", StringComparison.OrdinalIgnoreCase))
                return _anthropicService;

            if (string.Equals(providerName, "Ollama", StringComparison.OrdinalIgnoreCase)) return _ollamaService;
            throw new ArgumentException("Unknown provider.", nameof(providerName));
        }
    }
}
