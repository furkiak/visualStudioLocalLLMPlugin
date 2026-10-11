using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class InputDialogViewModel : ViewModelBase
    {
        private string _inputText = "";

        public InputDialogViewModel(string title, string message, string okButtonText = "OK")
        {
            Title = title ?? "";
            Message = message ?? "";
            OkButtonText = okButtonText ?? "OK";

            OkCommand = new RelayCommand(_ => CloseAction?.Invoke(true), _ => !string.IsNullOrEmpty(InputText));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public Action<bool> CloseAction { get; set; }

        public string Title { get; }
        public string Message { get; }
        public string OkButtonText { get; }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value ?? "");
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
    }
}
