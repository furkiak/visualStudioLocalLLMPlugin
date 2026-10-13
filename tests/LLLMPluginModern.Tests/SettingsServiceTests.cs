using System;
using System.IO;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public sealed class SettingsServiceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "lllm-tests-" + Guid.NewGuid().ToString("N"));

        public SettingsServiceTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
        }

        private string SettingsFile => Path.Combine(_folder, "settings.json");

        private AppSettings LoadFresh() => new SettingsService(_folder).Load();

        [Fact]
        public void Without_a_file_the_defaults_are_used()
        {
            var settings = LoadFresh();
            Assert.Equal(ProviderIds.Ollama, settings.ActiveProvider);
            Assert.Equal(300, settings.RequestTimeoutSeconds);
            Assert.Equal(ThemeModes.Auto, settings.Theme);
            Assert.Equal("http://localhost:11434", settings.ActiveProfile.Url);
        }

        [Fact]
        public void Api_keys_are_saved_encrypted_and_read_back()
        {
            var service = new SettingsService(_folder);
            var settings = service.Load();
            settings.ActiveProvider = ProviderIds.OpenAI;
            settings.Profiles[ProviderIds.OpenAI] = new ProviderProfile { Url = "https://api.openai.com/v1", Model = "gpt-5.5", ApiKey = "test-key-not-real-123" };
            service.Save(settings);
            service.Save(settings);   // the second save replaces an existing file

            string json = File.ReadAllText(SettingsFile);
            Assert.DoesNotContain("test-key-not-real-123", json);
            Assert.Contains("ApiKeyProtected", json);
            Assert.Contains("\"Version\": 2", json);

            var reloaded = LoadFresh();
            Assert.Equal(ProviderIds.OpenAI, reloaded.ActiveProvider);
            Assert.Equal("test-key-not-real-123", reloaded.GetProfile("openai").ApiKey);   // profile names ignore case
        }

        [Fact]
        public void Load_returns_a_private_copy()
        {
            var service = new SettingsService(_folder);
            var first = service.Load();
            first.Theme = ThemeModes.Dark;
            first.Profiles[ProviderIds.Gemini] = ProviderProfile.CreateDefault(ProviderIds.Gemini);

            var second = service.Load();
            Assert.Equal(ThemeModes.Auto, second.Theme);
            Assert.False(second.Profiles.ContainsKey(ProviderIds.Gemini));
        }

        [Fact]
        public void Settings_from_version_3_are_migrated_and_the_endpoint_is_cleaned()
        {
            File.WriteAllText(SettingsFile, "{\"AiServer\":\"Ollama\",\"Url\":\"http://localhost:11434/api/generate\",\"Model\":\"llama3:8b\",\"ApiKey\":\"\"}");

            var settings = LoadFresh();

            Assert.Equal(ProviderIds.Ollama, settings.ActiveProvider);
            Assert.Equal("http://localhost:11434", settings.ActiveProfile.Url);
            Assert.Equal("llama3:8b", settings.ActiveProfile.Model);
        }

        [Fact]
        public void A_retired_default_model_is_replaced_and_the_old_key_is_kept()
        {
            File.WriteAllText(SettingsFile, "{\"AiServer\":\"Gemini\",\"Url\":\"\",\"Model\":\"gemini-1.5-pro\",\"ApiKey\":\"legacy-key\"}");

            var profile = LoadFresh().ActiveProfile;

            Assert.Equal(ProviderInfo.Get(ProviderIds.Gemini).DefaultModel, profile.Model);
            Assert.Equal(ProviderInfo.Get(ProviderIds.Gemini).DefaultUrl, profile.Url);
            Assert.Equal("legacy-key", profile.ApiKey);
        }

        [Fact]
        public void An_unknown_legacy_provider_falls_back_to_Ollama()
        {
            File.WriteAllText(SettingsFile, "{\"AiServer\":\"SomethingElse\",\"Url\":\"\",\"Model\":\"\",\"ApiKey\":\"\"}");
            Assert.Equal(ProviderIds.Ollama, LoadFresh().ActiveProvider);
        }

        [Fact]
        public void Out_of_range_values_are_repaired()
        {
            File.WriteAllText(SettingsFile, "{\"Version\":2,\"ActiveProvider\":\"anthropic\",\"Profiles\":{},\"RequestTimeoutSeconds\":2,\"Theme\":\"Neon\"}");

            var settings = LoadFresh();

            Assert.Equal(ProviderIds.Anthropic, settings.ActiveProvider);
            Assert.Equal(300, settings.RequestTimeoutSeconds);
            Assert.Equal(ThemeModes.Auto, settings.Theme);
        }

        [Fact]
        public void A_key_encrypted_elsewhere_is_dropped_instead_of_crashing()
        {
            File.WriteAllText(SettingsFile, "{\"Version\":2,\"ActiveProvider\":\"OpenAI\",\"Profiles\":{\"OpenAI\":{\"Url\":\"https://api.openai.com/v1\",\"Model\":\"m\",\"ApiKeyProtected\":\"bm90LWRwYXBp\"}}}");
            Assert.Equal("", LoadFresh().ActiveProfile.ApiKey);
        }

        [Fact]
        public void A_corrupt_file_is_kept_aside_and_defaults_are_used()
        {
            File.WriteAllText(SettingsFile, "{ this is not json");

            var settings = LoadFresh();

            Assert.Equal(ProviderIds.Ollama, settings.ActiveProvider);
            Assert.True(File.Exists(SettingsFile + ".corrupt"));
        }
    }
}
