using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Factories;
using LLLMPluginModern.Infrastructure.Services;
using LLLMPluginModern.Testing;
using LLLMPluginModern.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace LLLMPluginModern.Tests
{
    public sealed class NativeFixtureTests
    {
        private readonly ITestOutputHelper output;
        public NativeFixtureTests(ITestOutputHelper output) { this.output = output; }
        private sealed class Dialogs : IDialogService
        {
            public void ShowSettings() { }
            public void ShowMessage(string message, string title = "Information") { }
            public bool ShowInput(string title, string button, out string result) { result = ""; return false; }
            public bool ShowModifyReplicate(out string target, out string[] words) { target = ""; words = new string[0]; return false; }
            public bool ShowAdd(out string text, out bool left) { text = ""; left = false; return false; }
        }
        private sealed class Profile : IDisposable
        {
            public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "LLLMNativeFixtures", Guid.NewGuid().ToString("N"));
            public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
            public ValidationSettingsService Service { get; }
            public Profile(string endpoint)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(Path.Combine(DirectoryPath, "profile.json"), JsonConvert.SerializeObject(new { Version = 1, Mode = "LoopbackHttpFixture", Endpoint = endpoint }));
                Service = new ValidationSettingsService(DirectoryPath);
            }
            public void Dispose() { Directory.Delete(DirectoryPath, true); }
        }
        private static MainViewModel Vm(ValidationSettingsService settings)
        {
            var factory = new LlmApiServiceFactory(new OllamaApiService(null, TimeSpan.FromSeconds(3)),
                new GeminiApiService(), new OpenAiApiService(), new AnthropicApiService());
            return new MainViewModel(factory, settings, new Dialogs())
            { SelectedText = "// disposable selection", PromptText = "synthetic fixture request", IsTargetCurrent = () => true };
        }
        private static async Task FirstChunk(MainViewModel vm, Task operation, TaskCompletionSource<bool> seen)
        {
            Assert.Same(seen.Task, await Task.WhenAny(seen.Task, operation, Task.Delay(5000)));
            Assert.False(operation.IsCompleted);
            Assert.True(vm.IsBusy);
            Assert.False(vm.CanApply);
            Assert.Equal(LoopbackOllamaFixture.FirstText, vm.ResponseText);
        }
        private static TaskCompletionSource<bool> Observe(MainViewModel vm)
        {
            var seen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            vm.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(vm.ResponseText) && vm.ResponseText == LoopbackOllamaFixture.FirstText)
                    seen.TrySetResult(true);
            };
            return seen;
        }

        [Fact]
        public async Task RealChunkedProductionFactoryUpdatesCandidateBeforeDoneThenRequiresReview()
        {
            using (var fixture = new LoopbackOllamaFixture(holdStream: true, segmentDelay: TimeSpan.FromMilliseconds(200)))
            using (var profile = new Profile(fixture.Endpoint))
            {
                var vm = Vm(profile.Service);
                Assert.Contains("ISOLATED HTTP FIXTURE", vm.RequestSummary);
                Assert.Contains("no model", vm.RequestSummary);
                var seen = Observe(vm);
                var operation = vm.SendToAiAsync();
                await FirstChunk(vm, operation, seen);
                Assert.DoesNotContain(fixture.Events, entry => entry.Contains("done-sent"));
                fixture.ReleaseStreams();
                await operation;
                Assert.Equal(LoopbackOllamaFixture.FirstText + LoopbackOllamaFixture.LastText, vm.ResponseText);
                Assert.False(vm.IsBusy);
                Assert.False(vm.CanApply);
                vm.Reviewed = true;
                Assert.True(vm.CanApply);
                Assert.Equal(1, fixture.RequestCount);
                Assert.All(fixture.Events, entry => { Assert.DoesNotContain("disposable selection", entry); Assert.DoesNotContain("synthetic fixture request", entry); });
                output.WriteLine("Actual TCP chunked HTTP -> production factory/provider -> MainViewModel: partial candidate before done, split UTF-8/NDJSON chunk boundaries, final candidate/review gating passed. No external model.");
            }
        }

        [Theory]
        [InlineData("fixture-401", "authentication", false)]
        [InlineData("fixture-malformed", "invalid JSON", true)]
        [InlineData("fixture-timeout", "timed out", true)]
        [InlineData("fixture-cancel", "cancelled", true)]
        public async Task RealHttpFailuresAndCancelClearCandidateBusyAndAllowRetry(string scenario, string status, bool partial)
        {
            using (var fixture = new LoopbackOllamaFixture(holdStream: true, segmentDelay: TimeSpan.FromMilliseconds(200)))
            using (var profile = new Profile(fixture.Endpoint))
            {
                profile.Service.SaveSettings(new LlmSettings { Url = fixture.Endpoint, Model = scenario });
                var vm = Vm(profile.Service);
                var seen = Observe(vm);
                var operation = vm.SendToAiAsync();
                if (partial) await FirstChunk(vm, operation, seen);
                if (scenario == "fixture-cancel") vm.CancelCommand.Execute(null);
                Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(6000)));
                await operation;
                Assert.False(vm.IsBusy);
                Assert.Equal("", vm.ResponseText);
                vm.Reviewed = true;
                Assert.False(vm.CanApply);
                Assert.Contains(status, vm.StatusText);
                Assert.True(vm.SendAiCommand.CanExecute(null));
                profile.Service.SaveSettings(new LlmSettings { Url = fixture.Endpoint, Model = "fixture-stream" });
                vm.RefreshSettings();
                fixture.ReleaseStreams();
                await vm.SendToAiAsync();
                Assert.False(vm.IsBusy);
                Assert.Equal(LoopbackOllamaFixture.FirstText + LoopbackOllamaFixture.LastText, vm.ResponseText);
                vm.Reviewed = true;
                Assert.True(vm.CanApply);
                Assert.Equal(2, fixture.RequestCount);
                output.WriteLine("Actual TCP HTTP " + scenario + " -> production provider/view-model: status separated, candidate cleared, Apply off, busy cleared, retry successful. No model invoked.");
            }
        }

        [Fact]
        public void ProfileRoundtripIsSeparateAndNeverLoadsOrPersistsKeys()
        {
            using (var fixture = new LoopbackOllamaFixture())
            using (var profile = new Profile(fixture.Endpoint))
            {
                string sentinel = Path.Combine(profile.DirectoryPath, "normal-settings-sentinel.json");
                string original = "{\"Version\":2,\"ProtectedApiKey\":\"synthetic-invalid-blob\"}";
                File.WriteAllText(sentinel, original);
                Assert.Equal("fixture-stream", profile.Service.GetSettings().Model);
                Assert.Equal(TimeSpan.FromSeconds(15), profile.Service.RequestTimeout);
                Assert.False(File.Exists(profile.SettingsPath));
                profile.Service.SaveSettings(new LlmSettings { Url = fixture.Endpoint, Model = "fixture-401" });
                Assert.Equal("fixture-401", new ValidationSettingsService(profile.DirectoryPath).GetSettings().Model);
                var saved = JObject.Parse(File.ReadAllText(profile.SettingsPath));
                Assert.Null(saved["ApiKey"]);
                Assert.Null(saved["ProtectedApiKey"]);
                Assert.Equal(original, File.ReadAllText(sentinel));
                string before = File.ReadAllText(profile.SettingsPath);
                Assert.Throws<ArgumentException>(() => profile.Service.SaveSettings(new LlmSettings { Url = fixture.Endpoint, Model = "fixture-stream", ApiKey = "never-store-this" }));
                Assert.Equal(before, File.ReadAllText(profile.SettingsPath));
                Assert.Contains("ISOLATED HTTP FIXTURE", new SettingsViewModel(profile.Service).ProfileNotice);
            }
        }

        [Theory]
        [InlineData("{broken")]
        [InlineData("{\"Version\":999,\"ValidationOnly\":true}")]
        [InlineData("{\"Version\":1,\"ValidationOnly\":true,\"ProtectedApiKey\":\"not-decrypted\"}")]
        [InlineData("{\"Version\":1,\"ValidationOnly\":true,\"ApiKey\":\"not-loaded\"}")]
        public void CorruptOrCredentialBearingProfilePreservesFileAndOffersExplicitRecovery(string saved)
        {
            using (var fixture = new LoopbackOllamaFixture())
            using (var profile = new Profile(fixture.Endpoint))
            {
                File.WriteAllText(profile.SettingsPath, saved);
                var vm = Vm(profile.Service);
                Assert.False(vm.SendAiCommand.CanExecute(null));
                Assert.Contains("ISOLATED HTTP FIXTURE", vm.RequestSummary);
                Assert.Equal(saved, File.ReadAllText(profile.SettingsPath));
                Assert.DoesNotContain("not-loaded", vm.StatusText);
                Assert.DoesNotContain("not-decrypted", vm.StatusText);
                var form = new SettingsViewModel(profile.Service);
                Assert.Contains("Save replaces", form.ErrorText);
                Assert.Equal(fixture.Endpoint, form.Url);
                Assert.Equal("fixture-stream", form.Model);
                form.SaveCommand.Execute(null);
                vm.RefreshSettings();
                Assert.True(vm.SendAiCommand.CanExecute(null));
                Assert.Null(JObject.Parse(File.ReadAllText(profile.SettingsPath))["ProtectedApiKey"]);
            }
        }

        [Theory]
        [InlineData("https://example.test/api/generate", "Ollama", "fixture-stream")]
        [InlineData("http://127.0.0.1:1/api/generate", "Ollama", "fixture-stream")]
        [InlineData(null, "OpenAI", "fixture-stream")]
        [InlineData(null, "Ollama", "a-real-model")]
        public void ProfileRejectsOtherRecipientsProvidersAndRealModels(string url, string provider, string model)
        {
            using (var fixture = new LoopbackOllamaFixture())
            using (var profile = new Profile(fixture.Endpoint))
            {
                Assert.Throws<ArgumentException>(() => profile.Service.SaveSettings(new LlmSettings { AiServer = provider, Url = url ?? fixture.Endpoint, Model = model }));
                Assert.False(File.Exists(profile.SettingsPath));
                Assert.Equal(0, fixture.RequestCount);
            }
        }

        [Fact]
        public void MissingUnsupportedOrNormalProfileNeverFallsBackToPersonalSettings()
        {
            Assert.Throws<ArgumentException>(() => new ValidationSettingsService("relative-profile"));
            string normalDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLLMPluginModern");
            Assert.Throws<ArgumentException>(() => new ValidationSettingsService(normalDirectory));
            string missing = Path.Combine(Path.GetTempPath(), "LLLMNativeFixtures", Guid.NewGuid().ToString("N"));
            Assert.Throws<InvalidDataException>(() => new ValidationSettingsService(missing));
            Assert.False(Directory.Exists(missing));
            using (var fixture = new LoopbackOllamaFixture())
            using (var profile = new Profile(fixture.Endpoint))
            {
                string marker = Path.Combine(profile.DirectoryPath, "profile.json");
                string invalid = "{\"Version\":999,\"Mode\":\"LoopbackHttpFixture\"}";
                File.WriteAllText(marker, invalid);
                Assert.Throws<InvalidDataException>(() => new ValidationSettingsService(profile.DirectoryPath));
                Assert.Equal(invalid, File.ReadAllText(marker));
                Assert.False(File.Exists(profile.SettingsPath));
            }
        }
    }
}
