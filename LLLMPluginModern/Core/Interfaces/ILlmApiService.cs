using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ILlmApiService
    {
        /// <summary>Streams a chat completion. Never throws for network/API errors; inspect the returned response instead.</summary>
        Task<LlmResponse> GenerateAsync(LlmRequest request, IProgress<StreamChunk> progress, CancellationToken cancellationToken);

        /// <summary>Lists the models the endpoint offers. Throws <see cref="LlmApiException"/> with a user friendly message on failure.</summary>
        Task<IReadOnlyList<string>> ListModelsAsync(ProviderProfile profile, CancellationToken cancellationToken);
    }

    /// <summary>An error whose message is safe and helpful to show to the user.</summary>
    public sealed class LlmApiException : Exception
    {
        public LlmApiException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
