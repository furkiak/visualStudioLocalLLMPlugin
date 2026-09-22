using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.UI.ViewModels;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public sealed class WorkflowTests
    {
        private sealed class Settings : ISettingsService
        {
            public bool Fail;
            public LlmSettings Value = new LlmSettings();
            public LlmSettings GetSettings() { if (Fail) throw new IOException("secret-should-not-appear"); return Value; }
            public void SaveSettings(LlmSettings settings) { Value = settings; }
        }
        private sealed class Api : ILlmApiService, ILlmApiServiceFactory
        {
            public Func<CancellationToken, IProgress<string>, Task<LlmResponse>> Generate = (t, p) => Task.FromResult(new LlmResponse { IsSuccess = true, Text = "new code" });
            public ILlmApiService Create(string provider) => this;
            public Task<LlmResponse> GenerateTextAsync(string prompt, string selection, LlmSettings settings, CancellationToken token, IProgress<string> progress = null) => Generate(token, progress);
        }
        private sealed class Dialogs : IDialogService
        {
            public void ShowSettings() { }
            public void ShowMessage(string message, string title = "Information") { }
            public bool ShowInput(string title, string button, out string result) { result = ""; return false; }
            public bool ShowModifyReplicate(out string target, out string[] words) { target = ""; words = new string[0]; return false; }
            public bool ShowAdd(out string text, out bool left) { text = ""; left = false; return false; }
        }
        private static MainViewModel Vm(Api api = null, Settings settings = null) => new MainViewModel(api ?? new Api(), settings ?? new Settings(), new Dialogs())
            { SelectedText = "old code", PromptText = "modify", IsTargetCurrent = () => true };

        [Fact]
        public async Task OnlySuccessfulReviewedCurrentCandidateCanApply()
        {
            var vm = Vm();
            bool applied = false;
            vm.CloseAction = result => applied = result;
            vm.ApplyCommand.Execute(null);
            Assert.False(applied);
            await vm.SendToAiAsync();
            Assert.False(vm.ApplyCommand.CanExecute(null));
            vm.Reviewed = true;
            Assert.True(vm.ApplyCommand.CanExecute(null));
            vm.ApplyCommand.Execute(null);
            Assert.True(applied);
            vm.IsTargetCurrent = () => false;
            Assert.False(vm.ApplyCommand.CanExecute(null));
        }

        [Fact]
        public async Task CancelPropagatesAndNeverLeavesPartialTextApplicable()
        {
            var started = new TaskCompletionSource<bool>();
            CancellationToken received = default;
            var api = new Api { Generate = async (t, p) => { received = t; p.Report("partial"); started.SetResult(true); await Task.Delay(Timeout.Infinite, t); return null; } };
            var vm = Vm(api);
            var operation = vm.SendToAiAsync();
            await started.Task;
            vm.Reviewed = true;
            Assert.True(vm.IsBusy);
            Assert.False(vm.ApplyCommand.CanExecute(null));
            Assert.False(vm.RemoveDuplicatesCommand.CanExecute(null));
            vm.CancelCommand.Execute(null);
            await operation;
            Assert.True(received.IsCancellationRequested);
            Assert.False(vm.IsBusy);
            Assert.False(vm.CanApply);
            Assert.Equal("", vm.ResponseText);
        }

        [Fact]
        public async Task LateSuccessAfterWindowClosesIsDiscarded()
        {
            var result = new TaskCompletionSource<LlmResponse>();
            var vm = Vm(new Api { Generate = (t, p) => result.Task });
            var pending = vm.SendToAiAsync();
            vm.OnClosed();
            result.SetResult(new LlmResponse { IsSuccess = true, Text = "late" });
            await pending;
            Assert.Equal("", vm.ResponseText);
            Assert.False(vm.CanApply);
        }

        [Theory]
        [InlineData(false)] [InlineData(true)]
        public async Task ErrorOrExceptionClearsPreviousCandidateAndBusy(bool throws)
        {
            var api = new Api();
            var vm = Vm(api);
            await vm.SendToAiAsync();
            vm.Reviewed = true;
            api.Generate = (t, p) => throws ? throw new IOException("secret-do-not-log") : Task.FromResult(new LlmResponse { IsSuccess = false, ErrorMessage = "401" });
            await vm.SendToAiAsync();
            Assert.False(vm.IsBusy);
            Assert.False(vm.CanApply);
            Assert.Equal("", vm.ResponseText);
            Assert.DoesNotContain("secret-do-not-log", vm.StatusText);
        }

        [Fact]
        public void CorruptSettingsHaveRecoveryPathWithoutResettingDisk()
        {
            var settings = new Settings { Fail = true };
            var vm = Vm(settings: settings);
            Assert.False(vm.SendAiCommand.CanExecute(null));
            Assert.DoesNotContain("secret", vm.StatusText);
            var form = new SettingsViewModel(settings);
            Assert.Contains("Save replaces", form.ErrorText);
            settings.Fail = false;
            form.SaveCommand.Execute(null);
            vm.RefreshSettings();
            Assert.True(vm.SendAiCommand.CanExecute(null));
        }

        [Fact]
        public void ProviderSwitchClearsCredential()
        {
            var form = new SettingsViewModel(new Settings { Value = new LlmSettings { ApiKey = "only-for-ollama" } });
            form.AiServer = "Anthropic";
            Assert.Equal("", form.ApiKey);
        }

        [Fact]
        public async Task DoubleSendIsIgnoredAndEditingSelectionInvalidatesCandidate()
        {
            var result = new TaskCompletionSource<LlmResponse>(); int calls = 0;
            var vm = Vm(new Api { Generate = (t, p) => { calls++; return result.Task; } });
            var first = vm.SendToAiAsync();
            await vm.SendToAiAsync();
            Assert.Equal(1, calls);
            result.SetResult(new LlmResponse { IsSuccess = true, Text = "candidate" });
            await first;
            vm.Reviewed = true;
            vm.SelectedText = "changed";
            Assert.False(vm.CanApply);
        }

        [Fact]
        public async Task AsyncCommandContainsExceptionsAndReenablesItself()
        {
            bool caught = false;
            var command = new AsyncCommand(() => throw new IOException(), () => true, () => caught = true);
            await command.ExecuteAsync();
            Assert.True(caught);
            Assert.True(command.CanExecute(null));
        }

        [Theory]
        [InlineData("other", "contents", "selection", 1, 10)]
        [InlineData("file", "modified elsewhere", "selection", 1, 10)]
        [InlineData("file", "contents", "different", 1, 10)]
        [InlineData("file", "contents", "selection", 2, 10)]
        [InlineData("file", "contents", "selection", 1, 11)]
        public void ChangedDocumentContentSelectionOrRangeIsRejected(string doc, string contents, string selection, int start, int end)
        {
            var change = new SelectionChange("file", "contents", "selection", 1, 10);
            Assert.True(change.Matches("file", "contents", "selection", 1, 10));
            Assert.False(change.Matches(doc, contents, selection, start, end));
        }
        [Fact]
        public void PreviewShowsRemovedAndAddedLines()
        {
            string diff = SelectionChange.Preview("header\nold\nfooter", "header\nnew\nfooter");
            Assert.Contains("line 2", diff);
            Assert.Contains("- old\n+ new", diff);
            Assert.DoesNotContain("footer", diff);
        }
    }
}
