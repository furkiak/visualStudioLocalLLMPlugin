using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ILlmApiService
    {
        Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings);
    }
}