using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Prompts;

namespace LLLMPluginModern.UI.ViewModels
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ILlmApiServiceFactory _apiFactory;
        private readonly AppSettings _working;

        private ProviderInfo _provider;
        private string _url;
        private string _model;
        private string _apiKey;
        private string _temperature;
        private string _maxTokens;
        private string _timeout;
        private string _systemPrompt;
        private bool _includeFileContext;
        private bool _matchIndentation;
        private bool _showReasoning;
        private bool _useTranslucency;
        private string _theme;
        private bool _isLoadingModels;
        private string _connectionStatus;
        private bool? _connectionOk;
        private string _validationError;
        private CancellationTokenSource _modelsCts;

        public SettingsViewModel(ISettingsService settingsService, ILlmApiServiceFactory apiFactory)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
            _working = _settingsService.Load();

            _timeout = _working.RequestTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            _systemPrompt = string.IsNullOrWhiteSpace(_working.SystemPrompt) ? PromptBuilder.DefaultSystemPrompt : _working.SystemPrompt;
            _includeFileContext = _working.IncludeFileContext;
            _matchIndentation = _working.MatchIndentation;
            _showReasoning = _working.ShowReasoning;
            _useTranslucency = _working.UseTranslucency;
            _theme = _working.Theme;

            _provider = ProviderInfo.Get(_working.ActiveProvider);
            LoadProfile(_provider);

            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
            RefreshModelsCommand = new AsyncRelayCommand(_ => LoadModelsAsync(reportErrors: true), _ => !IsLoadingModels, ex => ReportConnection(false, ex.Message));
            TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync(), _ => !IsLoadingModels, ex => ReportConnection(false, ex.Message));
            ResetSystemPromptCommand = new RelayCommand(_ => SystemPrompt = PromptBuilder.DefaultSystemPrompt);
            SetThemeCommand = new RelayCommand(p => Theme = p as string ?? ThemeModes.Auto);
        }

        public Action<bool> CloseAction { get; set; }

        /// <summary>Lets the view push the API key into the PasswordBox when the provider changes.</summary>
        public event EventHandler ApiKeyReloaded;

        public IReadOnlyList<ProviderInfo> Providers => ProviderInfo.All;

        public ObservableCollection<string> AvailableModels { get; } = new ObservableCollection<string>();

        public ProviderInfo Provider
        {
            get => _provider;
            set
            {
                if (value == null || ReferenceEquals(value, _provider))
                    return;
                StoreProfile();
                _provider = value;
                OnPropertyChanged();
                LoadProfile(value);
                ReportConnection(null, null);
                _ = LoadModelsQuietlyAsync();
            }
        }

        public string ProviderHint => _provider.Hint;
        public bool ApiKeyOptional => !_provider.RequiresApiKey;
        public string ApiKeyPlaceholder => _provider.RequiresApiKey ? "Required" : "Optional for local servers";

        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        public string MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        public string Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value);
        }

        public bool IncludeFileContext
        {
            get => _includeFileContext;
            set => SetProperty(ref _includeFileContext, value);
        }

        public bool MatchIndentation
        {
            get => _matchIndentation;
            set => SetProperty(ref _matchIndentation, value);
        }

        public bool ShowReasoning
        {
            get => _showReasoning;
            set => SetProperty(ref _showReasoning, value);
        }

        public bool UseTranslucency
        {
            get => _useTranslucency;
            set => SetProperty(ref _useTranslucency, value);
        }

        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value))
                {
                    OnPropertyChanged(nameof(IsThemeAuto));
                    OnPropertyChanged(nameof(IsThemeDark));
                    OnPropertyChanged(nameof(IsThemeLight));
                }
            }
        }

        public bool IsThemeAuto => _theme == ThemeModes.Auto;
        public bool IsThemeDark => _theme == ThemeModes.Dark;
        public bool IsThemeLight => _theme == ThemeModes.Light;

        public bool IsLoadingModels
        {
            get => _isLoadingModels;
            private set
            {
                if (SetProperty(ref _isLoadingModels, value))
                    RelayCommand.Refresh();
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set
            {
                if (SetProperty(ref _connectionStatus, value))
                    OnPropertyChanged(nameof(HasConnectionStatus));
            }
        }

        public bool HasConnectionStatus => !string.IsNullOrEmpty(_connectionStatus);

        /// <summary>null = neutral/in progress, true = success, false = failure.</summary>
        public bool? ConnectionOk
        {
            get => _connectionOk;
            private set => SetProperty(ref _connectionOk, value);
        }

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                if (SetProperty(ref _validationError, value))
                    OnPropertyChanged(nameof(HasValidationError));
            }
        }

        public bool HasValidationError => !string.IsNullOrEmpty(_validationError);

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RefreshModelsCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ResetSystemPromptCommand { get; }
        public ICommand SetThemeCommand { get; }

        /// <summary>Called once the window is shown: fills the model list without nagging about errors.</summary>
        public Task LoadModelsQuietlyAsync() => LoadModelsAsync(reportErrors: false);

        private void LoadProfile(ProviderInfo provider)
        {
            var profile = _working.GetProfile(provider.Id);
            Url = profile.Url;
            Model = profile.Model;
            ApiKey = profile.ApiKey ?? "";
            Temperature = profile.Temperature?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            MaxTokens = profile.MaxTokens?.ToString(CultureInfo.InvariantCulture) ?? "";
            AvailableModels.Clear();
            OnPropertyChanged(nameof(ProviderHint));
            OnPropertyChanged(nameof(ApiKeyOptional));
            OnPropertyChanged(nameof(ApiKeyPlaceholder));
            ApiKeyReloaded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Copies the editable fields into the working profile. Unparsable numbers are kept as "default".</summary>
        private void StoreProfile()
        {
            var profile = _working.GetProfile(_provider.Id).Clone();
            profile.Url = (Url ?? "").Trim();
            profile.Model = (Model ?? "").Trim();
            profile.ApiKey = (ApiKey ?? "").Trim();
            profile.Temperature = TryParseDouble(Temperature, out double t) ? t : (double?)null;
            profile.MaxTokens = int.TryParse((MaxTokens ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) && m > 0 ? m : (int?)null;
            _working.Profiles[_provider.Id] = profile;
        }

        private ProviderProfile CurrentProfile()
        {
            StoreProfile();
            return _working.GetProfile(_provider.Id);
        }

        private async Task LoadModelsAsync(bool reportErrors)
        {
            _modelsCts?.Cancel();
            var cts = new CancellationTokenSource();
            _modelsCts = cts;
            var provider = _provider;

            IsLoadingModels = true;
            if (reportErrors)
                ReportConnection(null, "Loading models…");
            try
            {
                var models = await _apiFactory.Create(provider.Id).ListModelsAsync(CurrentProfile(), cts.Token);
                if (cts.IsCancellationRequested || !ReferenceEquals(provider, _provider))
                    return;

                string current = Model;
                AvailableModels.Clear();
                foreach (var model in models)
                    AvailableModels.Add(model);

                if (string.IsNullOrWhiteSpace(current) && AvailableModels.Count > 0)
                    Model = AvailableModels[0];

                if (reportErrors)
                    ReportConnection(true, models.Count == 0
                        ? "Connected, but the server reported no models."
                        : string.Format(CultureInfo.CurrentCulture, "Connected · {0:N0} models available.", models.Count));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (reportErrors && ReferenceEquals(provider, _provider))
                    ReportConnection(false, ex.Message);
            }
            finally
            {
                if (ReferenceEquals(_modelsCts, cts))
                    IsLoadingModels = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            await LoadModelsAsync(reportErrors: true);
            if (ConnectionOk == true && AvailableModels.Count > 0 && !string.IsNullOrWhiteSpace(Model) &&
                !AvailableModels.Any(m => string.Equals(m, Model.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                ReportConnection(false, $"Connected, but the model \"{Model.Trim()}\" was not found on the server. Pick one from the list.");
            }
        }

        private void ReportConnection(bool? ok, string message)
        {
            ConnectionOk = ok;
            ConnectionStatus = message;
        }

        private void Save()
        {
            ValidationError = null;

            if (!string.IsNullOrWhiteSpace(Temperature) && (!TryParseDouble(Temperature, out double t) || t < 0 || t > 2))
            {
                ValidationError = "Temperature must be a number between 0 and 2 (or empty for the model default).";
                return;
            }

            if (!string.IsNullOrWhiteSpace(MaxTokens) &&
                (!int.TryParse(MaxTokens.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) || m <= 0))
            {
                ValidationError = "Max output tokens must be a positive whole number (or empty for the default).";
                return;
            }

            if (!int.TryParse((Timeout ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeout) || timeout < 10 || timeout > 3600)
            {
                ValidationError = "The timeout must be between 10 and 3600 seconds.";
                return;
            }

            string url = (Url ?? "").Trim();
            if (url.Length > 0 && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            {
                ValidationError = "The endpoint URL must start with http:// or https://.";
                return;
            }

            StoreProfile();
            _working.ActiveProvider = _provider.Id;
            _working.RequestTimeoutSeconds = timeout;
            _working.SystemPrompt = string.Equals((SystemPrompt ?? "").Trim(), PromptBuilder.DefaultSystemPrompt, StringComparison.Ordinal) ? null : SystemPrompt;
            _working.IncludeFileContext = IncludeFileContext;
            _working.MatchIndentation = MatchIndentation;
            _working.ShowReasoning = ShowReasoning;
            _working.UseTranslucency = UseTranslucency;
            _working.Theme = Theme;

            try
            {
                _settingsService.Save(_working);
            }
            catch (Exception ex)
            {
                ValidationError = "Settings could not be saved: " + ex.Message;
                return;
            }

            _modelsCts?.Cancel();
            CloseAction?.Invoke(true);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            text = (text ?? "").Trim();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
