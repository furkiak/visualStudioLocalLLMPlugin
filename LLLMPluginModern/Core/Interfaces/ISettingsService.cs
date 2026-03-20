using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ISettingsService
    {
        LlmSettings GetSettings();
        void SaveSettings(LlmSettings settings);
    }
}