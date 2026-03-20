using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class AddViewModel : ViewModelBase
    {
        public Action<bool> CloseAction { get; set; }

        private string _textToAdd;
        public string TextToAdd { get => _textToAdd; set => SetProperty(ref _textToAdd, value); }

        private bool _isLeft = true;
        public bool IsLeft { get => _isLeft; set => SetProperty(ref _isLeft, value); }

        private bool _isRight;
        public bool IsRight { get => _isRight; set => SetProperty(ref _isRight, value); }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public AddViewModel()
        {
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }
    }
}