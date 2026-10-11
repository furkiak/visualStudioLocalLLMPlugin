using System;
using System.Collections.Generic;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;

namespace LLLMPluginModern.Infrastructure.Factories
{
    public sealed class LlmApiServiceFactory : ILlmApiServiceFactory
    {
        private readonly Dictionary<string, ILlmApiService> _services;

        public LlmApiServiceFactory()
            : this(new Dictionary<string, ILlmApiService>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderIds.Ollama] = new OllamaApiService(),
                [ProviderIds.OpenAI] = new OpenAiApiService(),
                [ProviderIds.OpenAICompatible] = new OpenAiApiService(compatibleMode: true),
                [ProviderIds.Anthropic] = new AnthropicApiService(),
                [ProviderIds.Gemini] = new GeminiApiService(),
            })
        {
        }

        public LlmApiServiceFactory(Dictionary<string, ILlmApiService> services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public ILlmApiService Create(string providerId)
        {
            if (providerId != null && _services.TryGetValue(providerId, out var service))
                return service;
            return _services[ProviderIds.Ollama];
        }
    }
}
