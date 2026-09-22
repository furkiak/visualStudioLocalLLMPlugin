using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Infrastructure.Services
{
    public sealed class SettingsService : ISettingsService
    {
        private static readonly object Gate = new object();
        private readonly string _settingsFilePath;

        public SettingsService() : this(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "LLLMPluginModern", "settings.json")) { }

        public SettingsService(string settingsFilePath)
        {
            _settingsFilePath = Path.GetFullPath(settingsFilePath);
        }

        public LlmSettings GetSettings()
        {
            lock (Gate)
            {
                if (!File.Exists(_settingsFilePath)) return new LlmSettings();
                try
                {
                    if (new FileInfo(_settingsFilePath).Length > 65536) throw new InvalidDataException();
                    var saved = JObject.Parse(File.ReadAllText(_settingsFilePath));
                    int version = saved.Value<int?>("Version") ?? 1;
                    if (version != 1 && version != 2) throw new InvalidDataException();
                    var settings = new LlmSettings
                    {
                        AiServer = saved.Value<string>("AiServer") ?? "Ollama",
                        Url = saved.Value<string>("Url") ?? "http://localhost:11434/api/generate",
                        Model = saved.Value<string>("Model") ?? "deepseek-r1:8b"
                    };
                    if (version == 1)
                    {
                        settings.ApiKey = saved.Value<string>("ApiKey") ?? "";
                        SaveSettings(settings);
                    }
                    else
                    {
                        string protectedKey = saved.Value<string>("ProtectedApiKey") ?? "";
                        if (protectedKey.Length != 0)
                        {
                            byte[] clear = ProtectedData.Unprotect(Convert.FromBase64String(protectedKey), null, DataProtectionScope.CurrentUser);
                            try { settings.ApiKey = Encoding.UTF8.GetString(clear); }
                            finally { Array.Clear(clear, 0, clear.Length); }
                        }
                    }
                    return settings;
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException ||
                    ex is CryptographicException || ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
                {
                    throw new InvalidOperationException("Saved settings could not be read or unlocked. Open Settings, re-enter your values and choose Save to replace them. The existing file has not been reset.");
                }
            }
        }

        public void SaveSettings(LlmSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            HttpLlmApiService.Endpoint(settings);
            if (Encoding.UTF8.GetByteCount(settings.ApiKey ?? "") > 16000) throw new ArgumentException("API key exceeds the settings size limit.");
            if ((settings.Model ?? "").Length > 256 || (settings.Url ?? "").Length > 2048) throw new ArgumentException("Settings exceed the field size limit.");
            lock (Gate)
            {
                string protectedKey = "";
                byte[] clear = Encoding.UTF8.GetBytes(settings.ApiKey ?? "");
                try
                {
                    if (clear.Length != 0)
                        protectedKey = Convert.ToBase64String(ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser));
                }
                finally { Array.Clear(clear, 0, clear.Length); }
                var saved = new { Version = 2, settings.AiServer, settings.Url, settings.Model, ProtectedApiKey = protectedKey };
                string directory = Path.GetDirectoryName(_settingsFilePath);
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(saved, Formatting.Indented));
                        file.Write(bytes, 0, bytes.Length);
                        file.Flush(true);
                    }
                    if (File.Exists(_settingsFilePath)) File.Replace(temporary, _settingsFilePath, null);
                    else File.Move(temporary, _settingsFilePath);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
    }
}
