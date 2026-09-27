using System;
using System.Collections.Generic;
using System.Windows.Input;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.UI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private bool _isInitializing = true;
        private string _errorText;
        public string ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }
        public string ProfileNotice => (_settingsService as ISettingsProfile)?.ProfileNotice ?? "";
        public bool IsValidationProfile => _settingsService is ISettingsProfile;
        public bool IsNormalProfile => !IsValidationProfile;
        public string[] FixtureScenarios { get; } = new[] { "fixture-stream", "fixture-401", "fixture-malformed", "fixture-timeout", "fixture-cancel" };

        public Action CloseAction { get; set; }

        public List<string> Providers { get; } = new List<string> { "Ollama", "Gemini", "OpenAI", "Anthropic" };

        private string _aiServer;
        public string AiServer
        {
            get => _aiServer;
            set
            {
                if (SetProperty(ref _aiServer, value))
                {
                    ApplyDefaultSettingsForProvider(value);
                    if (!_isInitializing) ApiKey = "";
                }
            }
        }

        private string _url;
        public string Url { get => _url; set => SetProperty(ref _url, value); }

        private string _model;
        public string Model { get => _model; set => SetProperty(ref _model, value); }

        private string _apiKey;
        public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }

   
        private bool _isUrlVisible = true;
        public bool IsUrlVisible { get => _isUrlVisible; set => SetProperty(ref _isUrlVisible, value); }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LlmSettings currentSettings;
            try { currentSettings = _settingsService.GetSettings(); }
            catch (Exception)
            {
                currentSettings = (_settingsService as ISettingsProfile)?.DefaultSettings ?? new LlmSettings();
                ErrorText = "Saved settings could not be read or unlocked. Re-enter your values; Save replaces the existing file. Cancel leaves it untouched.";
            }

            AiServer = string.IsNullOrWhiteSpace(currentSettings.AiServer) ? "Ollama" : currentSettings.AiServer;
            Url = currentSettings.Url;
            Model = currentSettings.Model;
            ApiKey = currentSettings.ApiKey;

         
            IsUrlVisible = AiServer != "Gemini";

            _isInitializing = false;

            SaveCommand = new RelayCommand(_ => SaveSettings());
            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());
        }

        private void ApplyDefaultSettingsForProvider(string provider)
        {
            if (_isInitializing) return;

            switch (provider)
            {
                case "Ollama":
                    Url = "http://localhost:11434/api/generate";
                    Model = "deepseek-r1:8b";
                    IsUrlVisible = true;  
                    break;
                case "OpenAI":
                    Url = "https://api.openai.com/v1/chat/completions";
                    Model = "";
                    IsUrlVisible = true;  
                    break;
                case "Anthropic":
                    Url = "https://api.anthropic.com/v1/messages";
                    Model = "";
                    IsUrlVisible = true;  
                    break;
                case "Gemini":
                    Url = "";
                    Model = "";
                    IsUrlVisible = false;  
                    break;
            }
        }

        private void SaveSettings()
        {
            var newSettings = new LlmSettings { AiServer = this.AiServer, Url = this.Url, Model = this.Model, ApiKey = this.ApiKey };
            try
            {
                Infrastructure.Services.HttpLlmApiService.Endpoint(newSettings);
                if (string.IsNullOrWhiteSpace(newSettings.Model)) { ErrorText = "Enter an available model identifier for this provider."; return; }
                _settingsService.SaveSettings(newSettings);
                CloseAction?.Invoke();
            }
            catch (Exception) { ErrorText = "Could not save settings. Check the model, endpoint and file permissions. Use HTTPS remotely and keep credentials out of URLs."; }
        }
    }
}
