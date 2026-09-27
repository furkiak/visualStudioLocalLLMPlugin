using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;
using LLLMPluginModern.UI.ViewModels;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public sealed class AppearanceSettingsTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "LLLMAppearanceTests", Guid.NewGuid().ToString("N"));
        private string MainPath => Path.Combine(root, "normal", "settings.json");
        private string ValidationDirectory => Path.Combine(root, "validation");
        private string ValidationPath => Path.Combine(ValidationDirectory, "settings.json");
        private const string Endpoint = "http://127.0.0.1:64789/api/generate";
        private SettingsService Normal => new SettingsService(MainPath);
        public AppearanceSettingsTests() { Directory.CreateDirectory(Path.GetDirectoryName(MainPath)); Directory.CreateDirectory(ValidationDirectory); }
        public void Dispose() { Directory.Delete(root, true); }
        private ValidationSettingsService Validation()
        {
            File.WriteAllText(Path.Combine(ValidationDirectory, "profile.json"), new JObject
            { ["Version"] = 1, ["Mode"] = "LoopbackHttpFixture", ["Endpoint"] = Endpoint }.ToString());
            return new ValidationSettingsService(ValidationDirectory);
        }

        [Fact]
        public void Legacy_v2_missing_font_size_defaults_without_rewrite_then_roundtrips_secret_and_preferences()
        {
            var settings = new LlmSettings { ApiKey = "synthetic-dpapi-key", Model = "chosen-model" };
            Normal.SaveSettings(settings);
            var legacy = JObject.Parse(File.ReadAllText(MainPath));
            legacy.Remove("TextScalePercent");
            File.WriteAllText(MainPath, legacy.ToString());
            var before = File.ReadAllBytes(MainPath);
            Assert.Equal(100, Normal.GetSettings().TextScalePercent);
            Assert.Equal(before, File.ReadAllBytes(MainPath));
            settings = Normal.GetSettings();
            settings.TextScalePercent = 200;
            Normal.SaveSettings(settings);
            var restarted = Normal.GetSettings();
            Assert.Equal(200, restarted.TextScalePercent);
            Assert.Equal("synthetic-dpapi-key", restarted.ApiKey);
            Assert.Equal("chosen-model", restarted.Model);
            Assert.Equal(settings.Url, restarted.Url);
            Assert.Equal(settings.AiServer, restarted.AiServer);
            Assert.DoesNotContain("synthetic-dpapi-key", File.ReadAllText(MainPath));
            settings.TextScalePercent = 150;
            Normal.SaveSettings(settings);
            Assert.Equal(150, Normal.GetSettings().TextScalePercent);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(MainPath)));
        }

        [Fact]
        public void Legacy_v1_migration_retains_font_size_without_plaintext_backup()
        {
            File.WriteAllText(MainPath, "{\"AiServer\":\"Ollama\",\"Model\":\"chosen\",\"ApiKey\":\"migration-fixture-key\",\"TextScalePercent\":150}");
            var loaded = Normal.GetSettings();
            Assert.Equal(150, loaded.TextScalePercent);
            Assert.Equal("migration-fixture-key", loaded.ApiKey);
            Assert.DoesNotContain("migration-fixture-key", File.ReadAllText(MainPath));
            Assert.Equal(2, JObject.Parse(File.ReadAllText(MainPath)).Value<int>("Version"));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(MainPath)));
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"200\"")]
        [InlineData("125")]
        [InlineData("100.0")]
        [InlineData("999999999999999")]
        [InlineData("100,\"TextScalePercent\":200")]
        public void Invalid_saved_font_preserves_legacy_main_and_existing_backup_without_migration(string value)
        {
            var content = "{\"Version\":1,\"ApiKey\":\"never-echo-fixture-key\",\"TextScalePercent\":" + value + "}";
            File.WriteAllText(MainPath, content);
            File.WriteAllText(MainPath + ".bak", "owned-existing-backup");
            var error = Assert.Throws<InvalidOperationException>(() => Normal.GetSettings());
            Assert.DoesNotContain("never-echo-fixture-key", error.ToString());
            Assert.Equal(content, File.ReadAllText(MainPath));
            Assert.Equal("owned-existing-backup", File.ReadAllText(MainPath + ".bak"));
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(MainPath)).Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(125)]
        [InlineData(300)]
        public void Invalid_font_save_preserves_normal_encrypted_main_backup_and_validation_main(int scale)
        {
            Normal.SaveSettings(new LlmSettings { ApiKey = "synthetic-encrypted-key" });
            var before = File.ReadAllBytes(MainPath);
            File.WriteAllText(MainPath + ".bak", "owned-backup");
            Assert.Throws<ArgumentException>(() => Normal.SaveSettings(new LlmSettings { TextScalePercent = scale }));
            Assert.Equal(before, File.ReadAllBytes(MainPath));
            Assert.Equal("owned-backup", File.ReadAllText(MainPath + ".bak"));
            var service = Validation();
            var settings = service.DefaultSettings;
            service.SaveSettings(settings);
            before = File.ReadAllBytes(ValidationPath);
            settings.TextScalePercent = scale;
            Assert.Throws<ArgumentException>(() => service.SaveSettings(settings));
            Assert.Equal(before, File.ReadAllBytes(ValidationPath));
            Assert.Empty(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories));
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"150\"")]
        [InlineData("125")]
        [InlineData("100,\"TextScalePercent\":200")]
        public void Invalid_validation_font_is_preserved_and_does_not_consult_normal_credentials(string value)
        {
            File.WriteAllText(MainPath, "{normal-settings-are-intentionally-unreadable");
            var service = Validation();
            var original = "{\"Version\":1,\"ValidationOnly\":true,\"AiServer\":\"Ollama\",\"Url\":\"" + Endpoint + "\",\"Model\":\"fixture-stream\",\"TextScalePercent\":" + value + "}";
            File.WriteAllText(ValidationPath, original);
            Assert.ThrowsAny<Exception>(() => service.GetSettings());
            Assert.Equal(original, File.ReadAllText(ValidationPath));
            Assert.Equal("{normal-settings-are-intentionally-unreadable", File.ReadAllText(MainPath));
        }

        [Fact]
        public void Failed_normal_font_replace_preserves_encrypted_bytes_and_removes_temporary_file()
        {
            Normal.SaveSettings(new LlmSettings { ApiKey = "owned-dpapi-key", TextScalePercent = 150 });
            var before = File.ReadAllBytes(MainPath);
            var changed = Normal.GetSettings();
            changed.TextScalePercent = 200;
            using (var locked = new FileStream(MainPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsAny<IOException>(() => Normal.SaveSettings(changed));
            Assert.Equal(before, File.ReadAllBytes(MainPath));
            Assert.Equal(150, Normal.GetSettings().TextScalePercent);
            Assert.Equal("owned-dpapi-key", Normal.GetSettings().ApiKey);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(MainPath)));
        }

        [Fact]
        public void Validation_legacy_font_default_and_restart_roundtrip_stay_credential_free()
        {
            var service = Validation();
            var settings = service.DefaultSettings;
            service.SaveSettings(settings);
            var legacy = JObject.Parse(File.ReadAllText(ValidationPath));
            legacy.Remove("TextScalePercent");
            File.WriteAllText(ValidationPath, legacy.ToString());
            var before = File.ReadAllBytes(ValidationPath);
            settings = service.GetSettings();
            Assert.Equal(100, settings.TextScalePercent);
            Assert.Equal(before, File.ReadAllBytes(ValidationPath));
            settings.TextScalePercent = 200;
            service.SaveSettings(settings);
            var restarted = Validation().GetSettings();
            Assert.Equal(200, restarted.TextScalePercent);
            Assert.Equal(Endpoint, restarted.Url);
            Assert.Equal("fixture-stream", restarted.Model);
            var saved = JObject.Parse(File.ReadAllText(ValidationPath));
            Assert.Null(saved["ApiKey"]);
            Assert.Null(saved["ProtectedApiKey"]);
        }

        [Fact]
        public void Failed_validation_font_replace_preserves_previous_preferences_and_no_temporary_file()
        {
            var service = Validation();
            var settings = service.DefaultSettings;
            settings.TextScalePercent = 150;
            service.SaveSettings(settings);
            var before = File.ReadAllBytes(ValidationPath);
            settings.TextScalePercent = 200;
            using (var locked = new FileStream(ValidationPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsAny<IOException>(() => service.SaveSettings(settings));
            Assert.Equal(before, File.ReadAllBytes(ValidationPath));
            Assert.Equal(150, service.GetSettings().TextScalePercent);
            Assert.Empty(Directory.GetFiles(ValidationDirectory, "*.tmp"));
        }

        [Fact]
        public void Font_reload_after_Settings_preserves_review_candidate_selection_and_disclosure()
        {
            var service = Validation();
            var vm = new MainViewModel(new CapturingApi(), service, new Dialogs())
            { SelectedText = "original\noriginal", PromptText = "unchanged prompt", IsTargetCurrent = () => true };
            vm.RemoveDuplicatesCommand.Execute(null);
            vm.Reviewed = true;
            var candidate = vm.ResponseText;
            var diff = vm.DiffText;
            var disclosure = vm.RequestSummary;
            var form = new SettingsViewModel(service) { TextScalePercent = 200 };
            form.SaveCommand.Execute(null);
            vm.RefreshSettings();
            Assert.Equal(200, vm.TextScalePercent);
            Assert.Equal("original\noriginal", vm.SelectedText);
            Assert.Equal("unchanged prompt", vm.PromptText);
            Assert.Equal(candidate, vm.ResponseText);
            Assert.Equal(diff, vm.DiffText);
            Assert.Equal(disclosure, vm.RequestSummary);
            Assert.True(vm.Reviewed);
            Assert.True(vm.CanApply);
        }

        [Fact]
        public async Task Changed_adapter_and_font_reload_use_saved_values_on_the_next_request()
        {
            Normal.SaveSettings(new LlmSettings { ApiKey = "only-for-old-provider", TextScalePercent = 150 });
            var api = new CapturingApi();
            var vm = new MainViewModel(api, Normal, new Dialogs())
            { SelectedText = "owned test selection", PromptText = "test prompt", IsTargetCurrent = () => true };
            Assert.Equal(150, vm.TextScalePercent);
            var form = new SettingsViewModel(Normal) { AiServer = "OpenAI", Model = "explicit-model", TextScalePercent = 200 };
            form.Url = "https://example.test/v1/chat/completions";
            Assert.Equal("", form.ApiKey);
            form.SaveCommand.Execute(null);
            vm.RefreshSettings();
            Assert.Equal(200, vm.TextScalePercent);
            Assert.Contains("OpenAI", vm.RequestSummary);
            await vm.SendToAiAsync();
            Assert.Equal("OpenAI", api.Provider);
            Assert.Equal(200, api.LastSettings.TextScalePercent);
            Assert.Equal("explicit-model", api.LastSettings.Model);
            Assert.Equal(form.Url, api.LastSettings.Url);
            Assert.Equal("", api.LastSettings.ApiKey);
            Assert.False(vm.Reviewed);
            Assert.False(vm.CanApply);
        }

        [Fact]
        public void Invalid_form_font_keeps_saved_file_open_for_correction()
        {
            Normal.SaveSettings(new LlmSettings { TextScalePercent = 150 });
            var before = File.ReadAllBytes(MainPath);
            var form = new SettingsViewModel(Normal) { TextScalePercent = 125 };
            var closed = false;
            form.CloseAction = () => closed = true;
            form.SaveCommand.Execute(null);
            Assert.False(closed);
            Assert.NotEmpty(form.ErrorText);
            Assert.Equal(before, File.ReadAllBytes(MainPath));
        }

        private sealed class CapturingApi : ILlmApiService, ILlmApiServiceFactory
        {
            public string Provider;
            public LlmSettings LastSettings;
            public ILlmApiService Create(string provider) { Provider = provider; return this; }
            public Task<LlmResponse> GenerateTextAsync(string prompt, string selection, LlmSettings settings,
                CancellationToken token, IProgress<string> progress = null)
            { LastSettings = settings; return Task.FromResult(new LlmResponse { IsSuccess = true, Text = "adapter-binding stub" }); }
        }
        private sealed class Dialogs : IDialogService
        {
            public void ShowSettings() { }
            public void ShowMessage(string message, string title = "Information") { }
            public bool ShowInput(string title, string button, out string result) { result = ""; return false; }
            public bool ShowModifyReplicate(out string target, out string[] words) { target = ""; words = new string[0]; return false; }
            public bool ShowAdd(out string text, out bool left) { text = ""; left = false; return false; }
        }
    }
}
