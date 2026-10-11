using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LLLMPluginModern.Core.Models
{
    /// <summary>Connection settings for a single provider. Every provider keeps its own profile,
    /// so switching providers never leaks one provider's API key or URL into another.</summary>
    public sealed class ProviderProfile
    {
        public string Url { get; set; }
        public string Model { get; set; }

        /// <summary>Plain-text key, only ever held in memory. Persisted encrypted (see <see cref="ApiKeyProtected"/>).</summary>
        [JsonIgnore]
        public string ApiKey { get; set; } = "";

        /// <summary>DPAPI-encrypted API key (current Windows user scope), base64 encoded.</summary>
        public string ApiKeyProtected { get; set; }

        /// <summary>Optional sampling temperature. Null means "use the model default".</summary>
        public double? Temperature { get; set; }

        /// <summary>Optional output token limit. Null means "use the provider default".</summary>
        public int? MaxTokens { get; set; }

        public static ProviderProfile CreateDefault(string providerId)
        {
            var info = ProviderInfo.Get(providerId);
            return new ProviderProfile { Url = info.DefaultUrl, Model = info.DefaultModel };
        }

        public ProviderProfile Clone()
        {
            return (ProviderProfile)MemberwiseClone();
        }
    }

    public static class ThemeModes
    {
        public const string Auto = "Auto";
        public const string Dark = "Dark";
        public const string Light = "Light";
    }

    public sealed class AppSettings
    {
        public const int CurrentVersion = 2;

        public int Version { get; set; } = CurrentVersion;

        public string ActiveProvider { get; set; } = ProviderIds.Ollama;

        public Dictionary<string, ProviderProfile> Profiles { get; set; } =
            new Dictionary<string, ProviderProfile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Custom system prompt. Null or empty means the built-in default.</summary>
        public string SystemPrompt { get; set; }

        /// <summary>Maximum idle time (no data received) before a request is abandoned.</summary>
        public int RequestTimeoutSeconds { get; set; } = 300;

        public string Theme { get; set; } = ThemeModes.Auto;

        /// <summary>Use the Windows 11 acrylic backdrop behind the glass surfaces when available.</summary>
        public bool UseTranslucency { get; set; } = true;

        public bool IncludeFileContext { get; set; }

        public bool MatchIndentation { get; set; } = true;

        public bool ShowReasoning { get; set; } = true;

        public double MainWindowWidth { get; set; }
        public double MainWindowHeight { get; set; }

        public ProviderProfile GetProfile(string providerId)
        {
            if (Profiles != null && providerId != null && Profiles.TryGetValue(providerId, out var profile) && profile != null)
                return profile;
            return ProviderProfile.CreateDefault(providerId);
        }

        public ProviderProfile ActiveProfile => GetProfile(ActiveProvider);

        public AppSettings Clone()
        {
            var copy = (AppSettings)MemberwiseClone();
            copy.Profiles = new Dictionary<string, ProviderProfile>(StringComparer.OrdinalIgnoreCase);
            if (Profiles != null)
            {
                foreach (var pair in Profiles)
                    copy.Profiles[pair.Key] = pair.Value?.Clone();
            }
            return copy;
        }
    }
}
