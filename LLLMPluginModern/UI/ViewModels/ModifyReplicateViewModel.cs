using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class ModifyReplicateViewModel : ViewModelBase
    {
        private string _targetWord = "";
        private string _replacementWords = "";

        public ModifyReplicateViewModel()
        {
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true),
                _ => !string.IsNullOrEmpty(TargetWord) && !string.IsNullOrWhiteSpace(ReplacementWords));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public Action<bool> CloseAction { get; set; }

        public string TargetWord
        {
            get => _targetWord;
            set => SetProperty(ref _targetWord, value ?? "");
        }

        /// <summary>One replacement per line.</summary>
        public string ReplacementWords
        {
            get => _replacementWords;
            set => SetProperty(ref _replacementWords, value ?? "");
        }

        public string[] GetReplacements()
        {
            return (ReplacementWords ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
    }
}
