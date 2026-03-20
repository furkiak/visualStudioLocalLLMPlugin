using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Infrastructure.Services;
using LLLMPluginModern.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LLLMPluginModern.Core
{
    public static class Bootstrapper
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void Initialize()
        {
            var services = new ServiceCollection();

            // Sınıflarımızı sisteme tanıtıyoruz
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<IDialogService, UI.Services.DialogService>();
            services.AddSingleton<OllamaApiService>();
            services.AddSingleton<GeminiApiService>();
            services.AddSingleton<OpenAiApiService>();
            services.AddSingleton<AnthropicApiService>();
            services.AddSingleton<ILlmApiServiceFactory, Infrastructure.Factories.LlmApiServiceFactory>();
            ServiceProvider = services.BuildServiceProvider();
        }
    }
}