using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class AddViewModel : ViewModelBase
    {
        private string _textToAdd = "";
        private bool _atLineStart = true;

        public AddViewModel()
        {
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true), _ => !string.IsNullOrEmpty(TextToAdd));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public Action<bool> CloseAction { get; set; }

        public string TextToAdd
        {
            get => _textToAdd;
            set => SetProperty(ref _textToAdd, value ?? "");
        }

        public bool AtLineStart
        {
            get => _atLineStart;
            set
            {
                if (SetProperty(ref _atLineStart, value))
                    OnPropertyChanged(nameof(AtLineEnd));
            }
        }

        public bool AtLineEnd
        {
            get => !_atLineStart;
            set => AtLineStart = !value;
        }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
    }
}
