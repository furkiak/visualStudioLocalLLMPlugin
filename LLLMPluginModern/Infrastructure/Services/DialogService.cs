using System;
using System.Windows;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.UI.ViewModels;
using LLLMPluginModern.UI.Views;

namespace LLLMPluginModern.UI.Services
{
    public class DialogService : IDialogService
    {
        private static void ApplyAppearance(Window view)
        {
            var service = Core.Bootstrapper.ServiceProvider.GetService(typeof(ISettingsService)) as ISettingsService;
            int scale = 100;
            try { scale = service?.GetSettings().TextScalePercent ?? 100; }
            catch (Exception) { /* Main/Settings already reports unreadable settings; local tools stay available. */ }
            AppearanceController.Apply(view, scale);
        }
        public void ShowSettings()
        {
            var vm = Core.Bootstrapper.ServiceProvider.GetService(typeof(SettingsViewModel)) as SettingsViewModel;
            var view = new SettingsView(vm);
            view.ShowDialog();
        }

        public bool ShowInput(string title, string okButtonText, out string result)
        {
            var vm = new InputDialogViewModel(title, okButtonText);
            var view = new InputDialogView(vm);
            ApplyAppearance(view);
            bool? success = view.ShowDialog();

            result = vm.InputText;
            return success == true;
        }

        public void ShowMessage(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowModifyReplicate(out string targetWord, out string[] replacementWords)
        {
            var vm = new ModifyReplicateViewModel();
            var view = new ModifyReplicateView(vm);
            ApplyAppearance(view);
            bool? success = view.ShowDialog();

            targetWord = vm.TargetWord;
            replacementWords = string.IsNullOrWhiteSpace(vm.ReplacementWords)
                ? new string[0]
                : vm.ReplacementWords.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            return success == true;
        }

        public bool ShowAdd(out string textToAdd, out bool isLeft)
        {
            var vm = new AddViewModel();
            var view = new AddView(vm);
            ApplyAppearance(view);
            bool? success = view.ShowDialog();

            textToAdd = vm.TextToAdd;
            isLeft = vm.IsLeft; 

            return success == true;
        }
    }
}
