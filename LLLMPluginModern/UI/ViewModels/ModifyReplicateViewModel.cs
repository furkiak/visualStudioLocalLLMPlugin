using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class ModifyReplicateViewModel : ViewModelBase
    {
        public Action<bool> CloseAction { get; set; }

        private string _targetWord;
        public string TargetWord { get => _targetWord; set => SetProperty(ref _targetWord, value); }

        private string _replacementWords;
        public string ReplacementWords { get => _replacementWords; set => SetProperty(ref _replacementWords, value); }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public ModifyReplicateViewModel()
        {
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }
    }
}