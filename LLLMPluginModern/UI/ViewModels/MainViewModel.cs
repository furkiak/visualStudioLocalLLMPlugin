using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Prompts;
using LLLMPluginModern.Core.Text;

namespace LLLMPluginModern.UI.ViewModels
{
    public enum StatusKind
    {
        Idle,
        Busy,
        Success,
        Error
    }

    public sealed class MainViewModel : ViewModelBase
    {
        private readonly ILlmApiServiceFactory _apiFactory;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogs;
        private readonly IClipboardService _clipboard;
        private readonly EditorContext _context;
        private readonly List<ChatMessage> _history = new List<ChatMessage>();

        private AppSettings _settings;
        private CancellationTokenSource _cts;
        private string _lastSentCode;

        private string _selectedText;
        private string _promptText = "";
        private bool _isBusy;
        private bool _isContextExpanded = true;
        private bool _includeFileContext;
        private string _statusText;
        private StatusKind _statusKind;
        private string _providerName;
        private string _modelName;

        public MainViewModel(ILlmApiServiceFactory apiFactory, ISettingsService settingsService, IDialogService dialogs,
                             IClipboardService clipboard, EditorContext context)
        {
            _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _context = context ?? EditorContext.Empty;

            _settings = _settingsService.Load();
            _selectedText = _context.SelectedText ?? "";
            _includeFileContext = _settings.IncludeFileContext && CanIncludeFile;

            Messages = new ObservableCollection<ChatItemViewModel>();
            Messages.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasMessages));
                UpdateLastFlags();
            };

            SendCommand = new AsyncRelayCommand(_ => SendAsync(PromptText, null, clearPrompt: true), _ => !IsBusy, ReportError);
            StopCommand = new RelayCommand(_ => Stop(), _ => IsBusy);
            QuickActionCommand = new AsyncRelayCommand(p => RunQuickActionAsync(p as QuickAction), _ => !IsBusy, ReportError);
            RegenerateCommand = new AsyncRelayCommand(p => RegenerateAsync(p as ChatItemViewModel), p => !IsBusy && (p as ChatItemViewModel)?.CanRegenerate == true, ReportError);
            NewChatCommand = new RelayCommand(_ => NewChat(), _ => HasMessages || IsBusy);
            SettingsCommand = new RelayCommand(_ => OpenSettings(), _ => !IsBusy);
            AboutCommand = new RelayCommand(_ => _dialogs.ShowAbout());

            ApplyCommand = new RelayCommand(_ => Apply(ApplyMode.ReplaceSelection), _ => CanApply);
            InsertBelowCommand = new RelayCommand(_ => Apply(ApplyMode.InsertBelow), _ => CanApply);
            CopyResultCommand = new RelayCommand(_ => CopyResult(), _ => CanApply);
            CancelCommand = new RelayCommand(_ => Cancel());

            CopyBlockCommand = new RelayCommand(p => Copy((p as ResponseBlockViewModel)?.Content, "Code copied to the clipboard."), p => p is ResponseBlockViewModel);
            ApplyBlockCommand = new RelayCommand(p => ApplyBlock(p as ResponseBlockViewModel), p => p is ResponseBlockViewModel && !IsBusy);
            CopyMessageCommand = new RelayCommand(p => Copy((p as ChatItemViewModel)?.Text, "Answer copied to the clipboard."), p => (p as ChatItemViewModel)?.Text?.Length > 0);
            UseAsInputCommand = new RelayCommand(p => UseAsInput(p as ChatItemViewModel), p => (p as ChatItemViewModel)?.HasResult == true && !IsBusy);
            ToggleContextCommand = new RelayCommand(_ => IsContextExpanded = !IsContextExpanded);

            RemoveDuplicatesCommand = ToolCommand("Remove duplicate lines", TextTools.RemoveDuplicateLines);
            SortAscendingCommand = ToolCommand("Sort lines A → Z", t => TextTools.SortLines(t, false));
            SortDescendingCommand = ToolCommand("Sort lines Z → A", t => TextTools.SortLines(t, true));
            ReverseLinesCommand = ToolCommand("Reverse lines", TextTools.ReverseLines);
            RemoveEmptyLinesCommand = ToolCommand("Remove empty lines", TextTools.RemoveEmptyLines);
            TrimWhitespaceCommand = ToolCommand("Trim trailing whitespace", TextTools.TrimTrailingWhitespace);
            UpperCaseCommand = ToolCommand("UPPERCASE", TextTools.ToUpper);
            LowerCaseCommand = ToolCommand("lowercase", TextTools.ToLower);
            ModifyReplicateCommand = new RelayCommand(_ => ModifyAndReplicate(), _ => !IsBusy);
            EraseCommand = new RelayCommand(_ => Erase(), _ => !IsBusy);
            AddCommand = new RelayCommand(_ => AddToLines(), _ => !IsBusy);

            UpdateProviderDisplay();
            SetStatus(StatusKind.Idle, InitialStatus());
        }

        // ---- Bindable state -------------------------------------------------------------------

        public ObservableCollection<ChatItemViewModel> Messages { get; }

        public IReadOnlyList<QuickAction> QuickActions => QuickAction.Defaults;

        public bool HasMessages => Messages.Count > 0;

        public string SelectedText
        {
            get => _selectedText;
            set
            {
                if (SetProperty(ref _selectedText, value ?? ""))
                {
                    OnPropertyChanged(nameof(ContextSubtitle));
                    OnPropertyChanged(nameof(HasContext));
                }
            }
        }

        public bool HasContext => !string.IsNullOrEmpty(_selectedText);

        public string ContextTitle => string.IsNullOrEmpty(_context.FileName) ? "Code context" : _context.FileName;

        public string ContextSubtitle
        {
            get
            {
                string language = string.IsNullOrEmpty(_context.LanguageName) ? "" : _context.LanguageName + " · ";
                int lines = TextTools.CountLines(_selectedText);
                if (lines == 0)
                    return language + (_context.IsAvailable ? "nothing selected — the answer goes to the caret" : "no document open");
                return language + string.Format(CultureInfo.CurrentCulture, lines == 1 ? "{0} line" : "{0:N0} lines", lines);
            }
        }

        public bool IsContextExpanded
        {
            get => _isContextExpanded;
            set => SetProperty(ref _isContextExpanded, value);
        }

        public bool CanIncludeFile => _context.DocumentTextProvider != null;

        public bool IncludeFileContext
        {
            get => _includeFileContext;
            set => SetProperty(ref _includeFileContext, value && CanIncludeFile);
        }

        public string PromptText
        {
            get => _promptText;
            set => SetProperty(ref _promptText, value ?? "");
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanApply));
                    RelayCommand.Refresh();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public StatusKind StatusKind
        {
            get => _statusKind;
            private set => SetProperty(ref _statusKind, value);
        }

        public string ProviderName
        {
            get => _providerName;
            private set => SetProperty(ref _providerName, value);
        }

        public string ModelName
        {
            get => _modelName;
            private set => SetProperty(ref _modelName, value);
        }

        public bool CanApply => !IsBusy && LastResult != null;

        /// <summary>Set when the window closes with "Apply"; consumed by the command that opened the window.</summary>
        public ApplyRequest PendingApply { get; private set; }

        /// <summary>Provided by the view: text the user highlighted inside an answer, if any.</summary>
        public Func<string> SelectedResponseTextProvider { get; set; }

        public Action<bool> CloseAction { get; set; }

        /// <summary>Raised after settings were saved so the view can refresh the theme.</summary>
        public event EventHandler SettingsChanged;

        public double PreferredWidth => _settings.MainWindowWidth;
        public double PreferredHeight => _settings.MainWindowHeight;

        // ---- Commands -------------------------------------------------------------------------

        public ICommand SendCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand QuickActionCommand { get; }
        public ICommand RegenerateCommand { get; }
        public ICommand NewChatCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand InsertBelowCommand { get; }
        public ICommand CopyResultCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CopyBlockCommand { get; }
        public ICommand ApplyBlockCommand { get; }
        public ICommand CopyMessageCommand { get; }
        public ICommand UseAsInputCommand { get; }
        public ICommand ToggleContextCommand { get; }

        public ICommand RemoveDuplicatesCommand { get; }
        public ICommand ModifyReplicateCommand { get; }
        public ICommand EraseCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand SortAscendingCommand { get; }
        public ICommand SortDescendingCommand { get; }
        public ICommand ReverseLinesCommand { get; }
        public ICommand RemoveEmptyLinesCommand { get; }
        public ICommand TrimWhitespaceCommand { get; }
        public ICommand UpperCaseCommand { get; }
        public ICommand LowerCaseCommand { get; }

        // ---- Conversation ---------------------------------------------------------------------

        private ChatItemViewModel LastResult =>
            Messages.LastOrDefault(m => m.HasResult && (m.IsTool || m.Succeeded));

        private Task RunQuickActionAsync(QuickAction action)
        {
            if (action == null)
                return Task.CompletedTask;

            string extra = PromptText.Trim();
            string prompt = extra.Length > 0 ? action.Prompt + "\n\nAdditional instructions: " + extra : action.Prompt;
            return SendAsync(prompt, extra.Length > 0 ? action.Title + " — " + extra : action.Title, clearPrompt: extra.Length > 0);
        }

        private async Task SendAsync(string prompt, string displayText, bool clearPrompt)
        {
            if (IsBusy)
                return;

            prompt = (prompt ?? "").Trim();
            string code = _selectedText ?? "";
            if (prompt.Length == 0 && code.Trim().Length == 0)
            {
                SetStatus(StatusKind.Error, "Type a question, or select some code first.");
                return;
            }

            bool attachCode = code.Trim().Length > 0 && !string.Equals(code, _lastSentCode, StringComparison.Ordinal);
            string userContent = PromptBuilder.BuildUserMessage(prompt, code, _context, attachCode);

            string attachment = null;
            if (attachCode)
            {
                int lines = TextTools.CountLines(code);
                attachment = string.Format(CultureInfo.CurrentCulture, lines == 1 ? "{0} · {1} line" : "{0} · {1:N0} lines", ContextTitle, lines);
            }

            Messages.Add(ChatItemViewModel.ForUser(displayText ?? (prompt.Length > 0 ? prompt : "Look at this code"), attachment));
            if (clearPrompt)
                PromptText = "";

            await CompleteAsync(userContent, attachCode ? code : null);
        }

        private async Task RegenerateAsync(ChatItemViewModel item)
        {
            if (item == null || IsBusy || !item.CanRegenerate)
                return;

            // Drop the previous answer; a failed answer never reached the history.
            if (item.Succeeded && _history.Count > 0 && _history[_history.Count - 1].Role == ChatRole.Assistant)
                _history.RemoveAt(_history.Count - 1);
            Messages.Remove(item);

            await CompleteAsync(item.SourceUserContent, item.AttachedCode);
        }

        private async Task CompleteAsync(string userContent, string attachedCode)
        {
            if (_history.Count == 0 || _history[_history.Count - 1].Role != ChatRole.User)
                _history.Add(new ChatMessage(ChatRole.User, userContent));

            _settings = _settingsService.Load();
            UpdateProviderDisplay();

            var assistant = ChatItemViewModel.ForAssistant(ModelName, userContent, attachedCode);
            assistant.ShowReasoning = _settings.ShowReasoning;
            Messages.Add(assistant);

            var sink = new StreamSink(assistant, OnStreamProgress);
            _cts = new CancellationTokenSource();
            IsBusy = true;
            SetStatus(StatusKind.Busy, $"Waiting for {ProviderName}…");

            LlmResponse response;
            try
            {
                string system = PromptBuilder.BuildSystemPrompt(_settings.SystemPrompt, _context, IncludeFileContext, out bool truncated);
                if (truncated)
                    SetStatus(StatusKind.Busy, $"File context was shortened to {PromptBuilder.MaxFileContextChars:N0} characters…");

                var request = new LlmRequest
                {
                    SystemPrompt = system,
                    Messages = _history.ToList(),
                    Profile = _settings.ActiveProfile,
                    IdleTimeout = TimeSpan.FromSeconds(Math.Max(10, _settings.RequestTimeoutSeconds))
                };
                response = await _apiFactory.Create(_settings.ActiveProvider).GenerateAsync(request, sink, _cts.Token);
            }
            catch (Exception ex)
            {
                response = LlmResponse.Failure(ex.Message);
            }
            finally
            {
                sink.Close();
                _cts.Dispose();
                _cts = null;
            }

            assistant.Complete(response);

            if (response.IsSuccess)
            {
                _history.Add(new ChatMessage(ChatRole.Assistant, response.Text));
                if (attachedCode != null)
                    _lastSentCode = attachedCode;
                SetStatus(StatusKind.Success, "Done" + (assistant.Stats != null ? " · " + assistant.Stats : "") +
                                              (CanApplyAfterBusy(assistant) ? " — Shift+Enter applies it" : ""));
            }
            else
            {
                // Keep the history alternating user/assistant: the unanswered prompt is retried via "Regenerate".
                if (_history.Count > 0 && _history[_history.Count - 1].Role == ChatRole.User)
                    _history.RemoveAt(_history.Count - 1);

                if (response.IsCancelled)
                    SetStatus(StatusKind.Idle, "Stopped.");
                else
                    SetStatus(StatusKind.Error, "Request failed — see the message above.");
            }

            IsBusy = false;
            UpdateLastFlags();
            OnPropertyChanged(nameof(CanApply));
        }

        private static bool CanApplyAfterBusy(ChatItemViewModel item) => item.HasResult;

        private void OnStreamProgress(int characters)
        {
            if (IsBusy)
                SetStatus(StatusKind.Busy, characters == 0
                    ? "Thinking…"
                    : string.Format(CultureInfo.CurrentCulture, "Writing… {0:N0} characters", characters));
        }

        private void Stop()
        {
            _cts?.Cancel();
        }

        private void NewChat()
        {
            Stop();
            Messages.Clear();
            _history.Clear();
            _lastSentCode = null;
            SetStatus(StatusKind.Idle, InitialStatus());
        }

        private void Cancel()
        {
            if (IsBusy)
            {
                Stop();
                return;
            }
            CloseAction?.Invoke(false);
        }

        private void UpdateLastFlags()
        {
            ChatItemViewModel lastAssistant = Messages.LastOrDefault(m => !m.IsUser);
            foreach (var item in Messages)
                item.IsLast = ReferenceEquals(item, lastAssistant) && ReferenceEquals(item, Messages[Messages.Count - 1]);
            RelayCommand.Refresh();
        }

        // ---- Applying results -------------------------------------------------------------------

        private ApplyRequest BuildApplyRequest(ApplyMode mode)
        {
            string highlighted = SelectedResponseTextProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(highlighted))
                return new ApplyRequest(highlighted, mode, isCode: true);

            var last = LastResult;
            if (last == null)
                return null;
            if (last.IsTool)
                return new ApplyRequest(last.Text, mode, isCode: false);
            return new ApplyRequest(Markdown.ExtractCode(last.Text, out _), mode, isCode: true);
        }

        private void Apply(ApplyMode mode)
        {
            var request = BuildApplyRequest(mode);
            if (request == null || request.Text.Length == 0)
                return;
            PendingApply = request;
            CloseAction?.Invoke(true);
        }

        private void ApplyBlock(ResponseBlockViewModel block)
        {
            if (block == null || string.IsNullOrEmpty(block.Content))
                return;
            bool isToolOutput = Messages.Any(m => m.IsTool && m.Blocks.Contains(block));
            PendingApply = new ApplyRequest(block.Content, ApplyMode.ReplaceSelection, isCode: !isToolOutput);
            CloseAction?.Invoke(true);
        }

        private void CopyResult()
        {
            var request = BuildApplyRequest(ApplyMode.ReplaceSelection);
            if (request != null)
                Copy(request.Text, "Copied to the clipboard.");
        }

        private void Copy(string text, string status)
        {
            if (string.IsNullOrEmpty(text))
                return;
            try
            {
                _clipboard.SetText(text);
                SetStatus(StatusKind.Success, status);
            }
            catch (Exception ex)
            {
                SetStatus(StatusKind.Error, "Could not access the clipboard: " + ex.Message);
            }
        }

        private void UseAsInput(ChatItemViewModel item)
        {
            if (item == null || !item.HasResult)
                return;
            SelectedText = item.IsTool ? item.Text : Markdown.ExtractCode(item.Text, out _);
            IsContextExpanded = true;
            SetStatus(StatusKind.Success, "Result moved into the code context — tools and prompts now work on it.");
        }

        // ---- Text tools -------------------------------------------------------------------------

        private ICommand ToolCommand(string title, Func<string, string> transform)
        {
            return new RelayCommand(_ => RunTool(title, transform), _ => !IsBusy);
        }

        private bool EnsureContext()
        {
            if (!string.IsNullOrEmpty(_selectedText))
                return true;
            SetStatus(StatusKind.Error, "Select or paste some text into the code context first.");
            return false;
        }

        private void RunTool(string title, Func<string, string> transform)
        {
            if (!EnsureContext())
                return;
            string result = transform(_selectedText);
            Messages.Add(ChatItemViewModel.ForTool(title, result, _context.LanguageId));
            SetStatus(StatusKind.Success, title + " — Shift+Enter applies the result.");
        }

        private void ModifyAndReplicate()
        {
            if (!EnsureContext())
                return;
            if (_dialogs.ShowModifyReplicate(out string target, out string[] replacements) &&
                !string.IsNullOrEmpty(target) && replacements.Length > 0)
            {
                RunTool("Modify & replicate", t => TextTools.ReplicateLines(t, target, replacements));
            }
        }

        private void Erase()
        {
            if (!EnsureContext())
                return;
            if (_dialogs.ShowInput("Erase text", "Every occurrence of this text is removed from the code context.", "Erase", out string word) &&
                !string.IsNullOrEmpty(word))
            {
                RunTool("Erase “" + word + "”", t => TextTools.EraseText(t, word));
            }
        }

        private void AddToLines()
        {
            if (!EnsureContext())
                return;
            if (_dialogs.ShowAdd(out string addition, out bool atLineStart) && !string.IsNullOrEmpty(addition))
            {
                RunTool(atLineStart ? "Add to line starts" : "Add to line ends", t => TextTools.AddToLines(t, addition, atLineStart));
            }
        }

        // ---- Settings & status --------------------------------------------------------------------

        private void OpenSettings()
        {
            if (_dialogs.ShowSettings())
            {
                _settings = _settingsService.Load();
                UpdateProviderDisplay();
                SetStatus(StatusKind.Success, $"Settings saved — using {ProviderName} · {ModelName}.");
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SaveWindowSize(double width, double height)
        {
            try
            {
                var settings = _settingsService.Load();
                settings.MainWindowWidth = Math.Round(width);
                settings.MainWindowHeight = Math.Round(height);
                _settingsService.Save(settings);
            }
            catch (Exception)
            {
                // Remembering the window size is a convenience; never fail closing because of it.
            }
        }

        private void UpdateProviderDisplay()
        {
            var info = ProviderInfo.Get(_settings.ActiveProvider);
            ProviderName = info.Id == ProviderIds.OpenAICompatible ? "Local server" : info.DisplayName.Replace(" (local)", "");
            string model = _settings.ActiveProfile.Model;
            ModelName = string.IsNullOrWhiteSpace(model) ? "no model selected" : model;
        }

        private string InitialStatus()
        {
            return $"Ready · {ProviderName} · {ModelName}";
        }

        private void SetStatus(StatusKind kind, string text)
        {
            StatusKind = kind;
            StatusText = text;
        }

        private void ReportError(Exception ex)
        {
            IsBusy = false;
            SetStatus(StatusKind.Error, "Unexpected error: " + ex.Message);
        }

        /// <summary>
        /// Collects streamed text on the network thread and pushes it to the UI at most ~20 times per
        /// second, so fast models don't flood the dispatcher or re-layout the answer for every token.
        /// </summary>
        private sealed class StreamSink : IProgress<StreamChunk>
        {
            private const int FlushIntervalMs = 50;

            private readonly object _gate = new object();
            private readonly TaskScheduler _uiScheduler;
            private readonly ChatItemViewModel _target;
            private readonly Action<int> _onProgress;
            private readonly StringBuilder _text = new StringBuilder();
            private readonly StringBuilder _reasoning = new StringBuilder();
            private bool _flushScheduled;
            private bool _closed;

            public StreamSink(ChatItemViewModel target, Action<int> onProgress)
            {
                _uiScheduler = SynchronizationContext.Current != null
                    ? TaskScheduler.FromCurrentSynchronizationContext()
                    : null;
                _target = target;
                _onProgress = onProgress;
            }

            public void Report(StreamChunk chunk)
            {
                lock (_gate)
                {
                    if (_closed)
                        return;

                    switch (chunk.Kind)
                    {
                        case StreamChunkKind.Reset:
                            _text.Clear();
                            _reasoning.Clear();
                            break;
                        case StreamChunkKind.Reasoning:
                            _reasoning.Append(chunk.Text);
                            break;
                        default:
                            _text.Append(chunk.Text);
                            break;
                    }

                    if (_flushScheduled)
                        return;
                    _flushScheduled = true;
                }

                if (_uiScheduler == null)
                {
                    Flush();
                    return;
                }

                _ = Task.Delay(FlushIntervalMs).ContinueWith(_ => Flush(), CancellationToken.None, TaskContinuationOptions.None, _uiScheduler);
            }

            public void Close()
            {
                lock (_gate)
                    _closed = true;
            }

            private void Flush()
            {
                string text, reasoning;
                lock (_gate)
                {
                    _flushScheduled = false;
                    if (_closed)
                        return;
                    text = _text.ToString();
                    reasoning = _reasoning.ToString();
                }

                _target.UpdateStreaming(text, reasoning);
                _onProgress?.Invoke(text.Length);
            }
        }
    }
}
