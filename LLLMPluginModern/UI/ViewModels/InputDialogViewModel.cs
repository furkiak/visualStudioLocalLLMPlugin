using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class InputDialogViewModel : ViewModelBase
    {
        public Action<bool> CloseAction { get; set; }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _inputText;
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        private string _okButtonText;
        public string OkButtonText
        {
            get => _okButtonText;
            set => SetProperty(ref _okButtonText, value);
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public InputDialogViewModel(string title, string okButtonText = "OK")
        {
            Title = title;
            OkButtonText = okButtonText;

            OkCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }
    }
}