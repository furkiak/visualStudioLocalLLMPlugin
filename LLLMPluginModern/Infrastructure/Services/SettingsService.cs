using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    /// <summary>
    /// Persists settings to %AppData%\LLLMPluginModern\settings.json. API keys are encrypted with
    /// DPAPI for the current Windows user, writes are atomic, and a corrupt file never crashes the IDE.
    /// Settings written by version 3.x are migrated automatically.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LLLMPluginModern.ApiKey.v1");

        // Defaults shipped by earlier versions whose models have since been retired.
        private static readonly Dictionary<string, string> RetiredDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini-1.5-pro"] = ProviderIds.Gemini,
            ["claude-3-opus-20240229"] = ProviderIds.Anthropic,
        };

        private readonly object _gate = new object();
        private readonly string _settingsFilePath;
        private AppSettings _cache;

        public SettingsService(string folder = null)
        {
            folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLLMPluginModern");
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            lock (_gate)
            {
                if (_cache == null)
                    _cache = ReadFromDisk();
                return _cache.Clone();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            lock (_gate)
            {
                var toWrite = settings.Clone();
                toWrite.Version = AppSettings.CurrentVersion;
                foreach (var profile in toWrite.Profiles.Values)
                {
                    if (profile != null)
                        profile.ApiKeyProtected = Protect(profile.ApiKey);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
                string json = JsonConvert.SerializeObject(toWrite, Formatting.Indented);
                string tempPath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (File.Exists(_settingsFilePath))
                    File.Replace(tempPath, _settingsFilePath, null);
                else
                    File.Move(tempPath, _settingsFilePath);

                _cache = toWrite.Clone();
            }
        }

        private AppSettings ReadFromDisk()
        {
            if (!File.Exists(_settingsFilePath))
                return Normalize(new AppSettings());

            try
            {
                var json = JObject.Parse(File.ReadAllText(_settingsFilePath));
                var settings = json["Profiles"] == null && json["AiServer"] != null
                    ? MigrateLegacy(json)
                    : json.ToObject<AppSettings>() ?? new AppSettings();
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                TryBackupCorruptFile();
                return Normalize(new AppSettings());
            }
        }

        /// <summary>Converts the single-profile format of version 3.x ({AiServer, Url, Model, ApiKey}).</summary>
        private static AppSettings MigrateLegacy(JObject json)
        {
            string provider = ProviderInfo.Get((string)json["AiServer"]).Id;
            var profile = ProviderProfile.CreateDefault(provider);

            string url = (string)json["Url"];
            if (!string.IsNullOrWhiteSpace(url))
            {
                switch (provider)
                {
                    case ProviderIds.Ollama:
                        profile.Url = Endpoints.NormalizeBase(url, profile.Url, "/api/generate", "/api/chat");
                        break;
                    case ProviderIds.OpenAI:
                        profile.Url = Endpoints.NormalizeBase(url, profile.Url, "/chat/completions");
                        break;
                    case ProviderIds.Anthropic:
                        profile.Url = Endpoints.NormalizeBase(url, profile.Url, "/messages");
                        break;
                }
            }

            string model = (string)json["Model"];
            if (!string.IsNullOrWhiteSpace(model) && !RetiredDefaults.ContainsKey(model.Trim()))
                profile.Model = model.Trim();

            string apiKey = (string)json["ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
                profile.ApiKeyProtected = Protect(apiKey);

            var settings = new AppSettings { ActiveProvider = provider };
            settings.Profiles[provider] = profile;
            return settings;
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            settings.ActiveProvider = ProviderInfo.Get(settings.ActiveProvider).Id;

            // Json.NET creates a case-sensitive dictionary; rebuild it with the intended comparer.
            var profiles = new Dictionary<string, ProviderProfile>(StringComparer.OrdinalIgnoreCase);
            if (settings.Profiles != null)
            {
                foreach (var pair in settings.Profiles)
                {
                    if (pair.Value == null)
                        continue;
                    pair.Value.ApiKey = Unprotect(pair.Value.ApiKeyProtected);
                    profiles[pair.Key] = pair.Value;
                }
            }
            settings.Profiles = profiles;

            if (settings.RequestTimeoutSeconds < 10)
                settings.RequestTimeoutSeconds = 300;
            if (settings.Theme != ThemeModes.Dark && settings.Theme != ThemeModes.Light)
                settings.Theme = ThemeModes.Auto;
            return settings;
        }

        private void TryBackupCorruptFile()
        {
            try
            {
                File.Copy(_settingsFilePath, _settingsFilePath + ".corrupt", overwrite: true);
            }
            catch (Exception)
            {
                // Best effort only; defaults are used either way.
            }
        }

        private static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        private static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
                return "";
            try
            {
                byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                // Encrypted by another Windows user or machine: the key has to be entered again.
                return "";
            }
        }
    }
}
