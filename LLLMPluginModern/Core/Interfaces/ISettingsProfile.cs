using System;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Interfaces
{
    public interface ISettingsProfile
    {
        string ProfileNotice { get; }
        TimeSpan RequestTimeout { get; }
        LlmSettings DefaultSettings { get; }
    }
}
