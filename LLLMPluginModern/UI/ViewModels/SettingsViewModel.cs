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
            var currentSettings = _settingsService.GetSettings();

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
                    Model = "gpt-4o";
                    IsUrlVisible = true;  
                    break;
                case "Anthropic":
                    Url = "https://api.anthropic.com/v1/messages";
                    Model = "claude-3-opus-20240229";
                    IsUrlVisible = true;  
                    break;
                case "Gemini":
                    Url = "";
                    Model = "gemini-1.5-pro";
                    IsUrlVisible = false;  
                    break;
            }
        }

        private void SaveSettings()
        {
            var newSettings = new LlmSettings { AiServer = this.AiServer, Url = this.Url, Model = this.Model, ApiKey = this.ApiKey };
            _settingsService.SaveSettings(newSettings);
            CloseAction?.Invoke();
        }
    }
}