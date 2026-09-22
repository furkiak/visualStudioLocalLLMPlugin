using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;

namespace LLLMPluginModern.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ILlmApiServiceFactory _apiFactory;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource request;
        private LlmSettings preparedSettings;
        private bool hasCandidate, closed;
        private string _selectedText = "", _promptText = "", _responseText = "", _statusText = "", _requestSummary = "";
        private bool _isBusy, _reviewed;
        public string SelectedText { get => _selectedText; set { if (SetProperty(ref _selectedText, value)) { InvalidateCandidate(); UpdateSummary(); } } }
        public string PromptText { get => _promptText; set { SetProperty(ref _promptText, value); UpdateSummary(); } }
        public string ResponseText { get => _responseText; private set { SetProperty(ref _responseText, value); OnPropertyChanged(nameof(DiffText)); } }
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
        public string RequestSummary { get => _requestSummary; private set => SetProperty(ref _requestSummary, value); }
        public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); CommandManager.InvalidateRequerySuggested(); } }
        public bool Reviewed { get => _reviewed; set { SetProperty(ref _reviewed, value); CommandManager.InvalidateRequerySuggested(); } }
        public string DiffText => Core.Models.SelectionChange.Preview(SelectedText, ResponseText);
        public Func<bool> IsTargetCurrent { get; set; }
        public bool CanApply
        {
            get
            {
                if (closed || IsBusy || !hasCandidate || !Reviewed || string.IsNullOrWhiteSpace(ResponseText)) return false;
                try { return IsTargetCurrent != null && IsTargetCurrent(); }
                catch (Exception) { return false; }
            }
        }
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
            SendAiCommand = new AsyncCommand(SendToAiAsync, () => !closed && !IsBusy && preparedSettings != null &&
                !(string.IsNullOrWhiteSpace(PromptText) && string.IsNullOrWhiteSpace(SelectedText)),
                () => { InvalidateCandidate(); StatusText = "The operation failed. Nothing can be applied; check Settings and retry."; });
            RemoveDuplicatesCommand = new RelayCommand(_ => RemoveDuplicates(), _ => !IsBusy && !closed);
            ModifyReplicateCommand = new RelayCommand(_ => ModifyAndReplicate(), _ => !IsBusy && !closed);
            EraseCommand = new RelayCommand(_ => EraseText(), _ => !IsBusy && !closed);
            AddCommand = new RelayCommand(_ => AddText(), _ => !IsBusy && !closed);
            SettingsCommand = new RelayCommand(_ => { _dialogService.ShowSettings(); RefreshSettings(); }, _ => !IsBusy);
            CreditCommand = new RelayCommand(_ => _dialogService.ShowMessage("Local LLM tools for Visual Studio\nFurkan Akça · Space & Tab\nPersonal project label · AGPL-3.0", "About"));
            ApplyCommand = new RelayCommand(_ => CloseAction?.Invoke(true), _ => CanApply);
            CancelCommand = new RelayCommand(_ => { if (IsBusy) CancelRequest(); else CloseAction?.Invoke(false); });
            RefreshSettings();
        }
        public void RefreshSettings()
        {
            try
            {
                preparedSettings = _settingsService.GetSettings();
                HttpLlmApiService.Endpoint(preparedSettings);
                StatusText = "Review the destination and selection before sending.";
            }
            catch (Exception)
            {
                preparedSettings = null;
                StatusText = "Saved settings could not be read or validated. Open Settings to re-enter and save them. Your saved file has not been reset.";
            }
            UpdateSummary();
            CommandManager.InvalidateRequerySuggested();
        }
        private void UpdateSummary()
        {
            if (preparedSettings == null) { RequestSummary = "Open Settings to choose a destination."; return; }
            string input = string.IsNullOrWhiteSpace(SelectedText) ? PromptText : SelectedText + "\n\n" + PromptText;
            var endpoint = HttpLlmApiService.Endpoint(preparedSettings);
            RequestSummary = preparedSettings.AiServer + " · " + preparedSettings.Model + " · " + endpoint.AbsoluteUri +
                "\nOnly the displayed selection and prompt are sent (" + Encoding.UTF8.GetByteCount(input ?? "") + " UTF-8 bytes). " +
                (endpoint.IsLoopback ? "Loopback endpoint; check its own forwarding policy." : "This request leaves your computer. Remove secrets before sending.");
        }
        private void InvalidateCandidate()
        {
            hasCandidate = false;
            Reviewed = false;
            ResponseText = "";
            CommandManager.InvalidateRequerySuggested();
        }
        private void SetCandidate(string text)
        {
            ResponseText = text ?? "";
            hasCandidate = !string.IsNullOrWhiteSpace(ResponseText);
            Reviewed = false;
            StatusText = hasCandidate ? "Candidate ready. Review Changes and check the review box before applying. AI output can be wrong." : "No usable text was produced. Nothing can be applied.";
        }
        public void CancelRequest() { request?.Cancel(); }
        public void OnClosed()
        {
            closed = true;
            request?.Cancel();
        }
        public async Task SendToAiAsync()
        {
            if (closed || IsBusy || preparedSettings == null || (string.IsNullOrWhiteSpace(PromptText) && string.IsNullOrWhiteSpace(SelectedText))) return;
            InvalidateCandidate();
            IsBusy = true;
            StatusText = "Generating… You can cancel this request.";
            using (var cancellation = new CancellationTokenSource())
            {
                request = cancellation;
                try
                {
                    var progress = new Progress<string>(text =>
                    {
                        if (!closed && IsBusy && ReferenceEquals(request, cancellation) && !cancellation.IsCancellationRequested)
                            ResponseText = text;
                    });
                    var response = await _apiFactory.Create(preparedSettings.AiServer)
                        .GenerateTextAsync(PromptText, SelectedText, preparedSettings, cancellation.Token, progress);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (response.IsSuccess) SetCandidate(response.Text);
                    else { InvalidateCandidate(); StatusText = response.ErrorMessage; }
                }
                catch (OperationCanceledException) { InvalidateCandidate(); StatusText = "Request cancelled. Nothing was applied."; }
                catch (Exception) { InvalidateCandidate(); StatusText = "The request failed. Check Settings and retry. Nothing can be applied."; }
                finally { request = null; IsBusy = false; }
            }
        }

        private void RemoveDuplicates()
        {
            if (string.IsNullOrWhiteSpace(SelectedText)) return;

            var lines = SelectedText.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var uniqueLines = lines.Distinct().ToArray();
            SetCandidate(string.Join(Environment.NewLine, uniqueLines));
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
                SetCandidate(sb.ToString().TrimEnd());
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
                SetCandidate(sb.ToString().TrimEnd());
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
                SetCandidate(sb.ToString().TrimEnd());
            }
        }
    }
}