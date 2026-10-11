using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ISettingsService
    {
        /// <summary>Returns a copy of the current settings; mutate it freely and pass it to <see cref="Save"/>.</summary>
        AppSettings Load();

        void Save(AppSettings settings);
    }
}
