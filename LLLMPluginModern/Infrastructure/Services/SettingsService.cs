using System;
using System.IO;
using Newtonsoft.Json;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
  
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pluginFolder = Path.Combine(appData, "LLLMPluginModern");
            Directory.CreateDirectory(pluginFolder);

            _settingsFilePath = Path.Combine(pluginFolder, "settings.json");
        }

        public LlmSettings GetSettings()
        {
            if (!File.Exists(_settingsFilePath))
                return new LlmSettings(); 

            string json = File.ReadAllText(_settingsFilePath);
            return JsonConvert.DeserializeObject<LlmSettings>(json) ?? new LlmSettings();
        }

        public void SaveSettings(LlmSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}