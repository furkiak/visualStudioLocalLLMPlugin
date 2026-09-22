using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using System;
using System.Threading;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ILlmApiService
    {
        Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings,
            CancellationToken cancellationToken, IProgress<string> progress = null);
    }
}
