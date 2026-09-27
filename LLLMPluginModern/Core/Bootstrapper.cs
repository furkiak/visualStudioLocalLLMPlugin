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

            var profile = ValidationSettingsService.FromEnvironment();
            if (profile == null) services.AddSingleton<ISettingsService, SettingsService>();
            else services.AddSingleton<ISettingsService>(profile);
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<IDialogService, UI.Services.DialogService>();
            services.AddSingleton(profile == null ? new OllamaApiService() : new OllamaApiService(null, profile.RequestTimeout));
            services.AddSingleton<GeminiApiService>();
            services.AddSingleton<OpenAiApiService>();
            services.AddSingleton<AnthropicApiService>();
            services.AddSingleton<ILlmApiServiceFactory, Infrastructure.Factories.LlmApiServiceFactory>();
            ServiceProvider = services.BuildServiceProvider();
        }
    }
}
