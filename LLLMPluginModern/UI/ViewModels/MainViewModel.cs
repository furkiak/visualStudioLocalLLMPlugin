using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using LLLMPluginModern.Core.Interfaces;

namespace LLLMPluginModern.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ILlmApiServiceFactory _apiFactory;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _selectedText;
        public string SelectedText { get => _selectedText; set => SetProperty(ref _selectedText, value); }

        private string _promptText;
        public string PromptText { get => _promptText; set => SetProperty(ref _promptText, value); }

        private string _responseText;
        public string ResponseText { get => _responseText; set => SetProperty(ref _responseText, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

 
        public ICommand SendAiCommand { get; }
        public ICommand RemoveDuplicatesCommand { get; }
        public ICommand ModifyReplicateCommand { get; }
        public ICommand EraseCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand CreditCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
        public Action<bool> CloseAction { get; set; } 
        public MainViewModel(ILlmApiServiceFactory apiFactory, ISettingsService settingsService, IDialogService dialogService)
        {
            _apiFactory = apiFactory;
            _settingsService = settingsService;
            _dialogService = dialogService;

            SendAiCommand = new RelayCommand(async _ => await SendToAiAsync(), _ => !IsBusy);
            RemoveDuplicatesCommand = new RelayCommand(_ => RemoveDuplicates());
            SettingsCommand = new RelayCommand(_ => _dialogService.ShowSettings());
            CreditCommand = new RelayCommand(_ => _dialogService.ShowMessage("Visual Studio Local LLM Plugin\nModernized Version 3.0\n\n  Furkan Akça - Spaceandtab Company", "Credit"));

            ModifyReplicateCommand = new RelayCommand(_ => ModifyAndReplicate());
            EraseCommand = new RelayCommand(_ => EraseText());
            AddCommand = new RelayCommand(_ => AddText());
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true));  
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false)); 

        }

        private async Task SendToAiAsync()
        {
            if (string.IsNullOrWhiteSpace(PromptText) && string.IsNullOrWhiteSpace(SelectedText)) return;

            IsBusy = true;
            ResponseText = "AI Thinking...";

            var settings = _settingsService.GetSettings();
            var activeApiService = _apiFactory.Create(settings.AiServer);
            var response = await activeApiService.GenerateTextAsync(PromptText, SelectedText, settings);

            if (response.IsSuccess)
                ResponseText = response.Text;
            else
                ResponseText = $"Error: {response.ErrorMessage}";

            IsBusy = false;
        }

        private void RemoveDuplicates()
        {
            if (string.IsNullOrWhiteSpace(SelectedText)) return;

            var lines = SelectedText.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var uniqueLines = lines.Distinct().ToArray();
            ResponseText = string.Join(Environment.NewLine, uniqueLines);
        }

        private void ModifyAndReplicate()
        {
            if (string.IsNullOrWhiteSpace(SelectedText)) return;

   
            if (_dialogService.ShowModifyReplicate(out string targetWord, out string[] replacementWords))
            {
                if (string.IsNullOrEmpty(targetWord) || replacementWords.Length == 0) return;

                var sb = new StringBuilder();
                using (var reader = new StringReader(SelectedText))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains(targetWord))
                        {
                            
                            foreach (var replacement in replacementWords)
                            {
                                sb.AppendLine(line.Replace(targetWord, replacement));
                            }
                        }
                        else
                        {
                        
                            sb.AppendLine(line);
                        }
                    }
                }
                ResponseText = sb.ToString().TrimEnd();
            }
        }

        private void EraseText()
        {
            if (string.IsNullOrWhiteSpace(SelectedText)) return;

         
            if (_dialogService.ShowInput("Enter the Word to Delete", "Delete", out string word) && !string.IsNullOrEmpty(word))
            {
                var sb = new StringBuilder();
                using (var reader = new StringReader(SelectedText))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    { 
                        sb.AppendLine(line.Replace(word, string.Empty));
                    }
                }
                ResponseText = sb.ToString().TrimEnd();
            }
        }

        private void AddText()
        {
            if (string.IsNullOrWhiteSpace(SelectedText)) return;
             
            if (_dialogService.ShowAdd(out string textToAdd, out bool isLeft) && !string.IsNullOrEmpty(textToAdd))
            {
                var sb = new StringBuilder();
                using (var reader = new StringReader(SelectedText))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isLeft)
                        {
                            
                            sb.AppendLine(textToAdd + line);
                        }
                        else
                        {
                        
                            sb.AppendLine(line + textToAdd);
                        }
                    }
                }
                ResponseText = sb.ToString().TrimEnd();
            }
        }
    }
}