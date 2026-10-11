using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.UI.ViewModels;
using LLLMPluginModern.UI.Views;

namespace LLLMPluginModern.UI.Services
{
    public sealed class DialogService : IDialogService
    {
        private readonly ISettingsService _settings;
        private readonly ILlmApiServiceFactory _apiFactory;

        public DialogService(ISettingsService settings, ILlmApiServiceFactory apiFactory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
        }

        public bool ShowSettings()
        {
            var view = new SettingsView(new SettingsViewModel(_settings, _apiFactory));
            return ShowDialog(view) == true;
        }

        public bool ShowInput(string title, string message, string okButtonText, out string result)
        {
            var vm = new InputDialogViewModel(title, message, okButtonText);
            bool ok = ShowDialog(new InputDialogView(vm)) == true;
            result = vm.InputText;
            return ok;
        }

        public void ShowMessage(string message, string title = "Information")
        {
            ShowDialog(new MessageView(new MessageViewModel(title, message)));
        }

        public void ShowAbout()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var vm = new MessageViewModel("Local LLM Plugin", "AI assistant for Visual Studio — local models through Ollama or LM Studio, and cloud models from OpenAI, Anthropic and Google.", MessageKind.About)
            {
                Detail = $"Version {version.Major}.{version.Minor}.{version.Build} · Furkan Akça · Spaceandtab",
                LinkText = "github.com/furkiak/visualStudioLocalLLMPlugin",
                LinkUrl = "https://github.com/furkiak/visualStudioLocalLLMPlugin"
            };
            ShowDialog(new MessageView(vm));
        }

        public bool ShowModifyReplicate(out string targetWord, out string[] replacementWords)
        {
            var vm = new ModifyReplicateViewModel();
            bool ok = ShowDialog(new ModifyReplicateView(vm)) == true;
            targetWord = vm.TargetWord;
            replacementWords = vm.GetReplacements();
            return ok;
        }

        public bool ShowAdd(out string textToAdd, out bool atLineStart)
        {
            var vm = new AddViewModel();
            bool ok = ShowDialog(new AddView(vm)) == true;
            textToAdd = vm.TextToAdd;
            atLineStart = vm.AtLineStart;
            return ok;
        }

        /// <summary>Shows a dialog centered on the currently active window (the assistant itself).</summary>
        private static bool? ShowDialog(Window dialog)
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && !ReferenceEquals(w, dialog));
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            return dialog.ShowDialog();
        }
    }

    public sealed class ClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // The clipboard can be briefly locked by another process (CLIPBRD_E_CANT_OPEN); retry a few times.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException) when (attempt < 4)
                {
                    Thread.Sleep(40);
                }
            }
        }
    }
}
