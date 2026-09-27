using System;
using System.IO;
using System.Text;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class ValidationSettingsService : ISettingsService, ISettingsProfile
    {
        public const string EnvironmentVariable = "LLLM_VALIDATION_PROFILE";
        private readonly string settingsPath;
        private readonly string endpoint;
        private readonly object gate = new object();
        public string ProfileNotice => "ISOLATED HTTP FIXTURE · synthetic response, no model. Normal settings and API keys are not loaded.";
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(15);
        public LlmSettings DefaultSettings => new LlmSettings { Url = endpoint, Model = "fixture-stream" };

        public ValidationSettingsService(string profileDirectory)
        {
            if (string.IsNullOrWhiteSpace(profileDirectory) || !Path.IsPathRooted(profileDirectory))
                throw new ArgumentException("Validation profile requires an absolute directory.");
            string directory = Path.GetFullPath(profileDirectory);
            settingsPath = Path.Combine(directory, "settings.json");
            string normalPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLLMPluginModern", "settings.json"));
            if (string.Equals(settingsPath, normalPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Validation cannot use the normal settings location.");
            string marker = Path.Combine(directory, "profile.json");
            if (!File.Exists(marker) || new FileInfo(marker).Length > 4096)
                throw new InvalidDataException("Validation profile marker is missing or too large.");
            var profile = JObject.Parse(File.ReadAllText(marker));
            if (profile.Value<int?>("Version") != 1 || profile.Value<string>("Mode") != "LoopbackHttpFixture")
                throw new InvalidDataException("Validation profile marker is unsupported.");
            endpoint = profile.Value<string>("Endpoint");
            Validate(new LlmSettings { Url = endpoint, Model = "fixture-stream" });
        }

        public static ValidationSettingsService FromEnvironment()
        {
            string directory = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return string.IsNullOrEmpty(directory) ? null : new ValidationSettingsService(directory);
        }

        public LlmSettings GetSettings()
        {
            lock (gate)
            {
                if (!File.Exists(settingsPath)) return DefaultSettings;
                if (new FileInfo(settingsPath).Length > 65536) throw new InvalidDataException("Validation settings are too large.");
                var saved = JObject.Parse(File.ReadAllText(settingsPath));
                if (saved.Value<int?>("Version") != 1 || saved.Value<bool?>("ValidationOnly") != true ||
                    saved["ApiKey"] != null || saved["ProtectedApiKey"] != null)
                    throw new InvalidDataException("Validation settings cannot contain credentials or normal settings.");
                var settings = new LlmSettings { AiServer = saved.Value<string>("AiServer"), Url = saved.Value<string>("Url"), Model = saved.Value<string>("Model") };
                Validate(settings);
                return settings;
            }
        }

        public void SaveSettings(LlmSettings settings)
        {
            Validate(settings);
            lock (gate)
            {
                string temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var saved = new { Version = 1, ValidationOnly = true, settings.AiServer, settings.Url, settings.Model };
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(saved, Formatting.Indented));
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        file.Write(bytes, 0, bytes.Length);
                        file.Flush(true);
                    }
                    if (File.Exists(settingsPath)) File.Replace(temporary, settingsPath, null);
                    else File.Move(temporary, settingsPath);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private void Validate(LlmSettings settings)
        {
            if (settings == null || settings.AiServer != "Ollama" || !string.IsNullOrEmpty(settings.ApiKey) ||
                (settings.Model != "fixture-stream" && settings.Model != "fixture-401" && settings.Model != "fixture-malformed" &&
                 settings.Model != "fixture-timeout" && settings.Model != "fixture-cancel"))
                throw new ArgumentException("Validation uses Ollama fixture scenarios and no credentials.");
            var uri = HttpLlmApiService.Endpoint(settings);
            if (uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.Port <= 0 || uri.AbsolutePath != "/api/generate" ||
                (endpoint != null && !string.Equals(settings.Url, endpoint, StringComparison.Ordinal)))
                throw new ArgumentException("Validation endpoint must match the explicit IPv4 loopback fixture.");
        }
    }
}
