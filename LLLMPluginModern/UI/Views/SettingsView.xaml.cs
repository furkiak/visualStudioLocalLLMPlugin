using System;
using System.Windows;
using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class SettingsView : GlassWindow
    {
        private readonly SettingsViewModel _viewModel;
        private bool _syncingKey;

        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            viewModel.CloseAction = ok =>
            {
                try
                {
                    DialogResult = ok;
                }
                catch (InvalidOperationException)
                {
                    Close();
                }
            };
            viewModel.ApiKeyReloaded += (s, e) => SyncApiKey();
            SyncApiKey();

            Loaded += (s, e) => _ = LoadModelsQuietlyAsync();
        }

        private async System.Threading.Tasks.Task LoadModelsQuietlyAsync()
        {
            try
            {
                await _viewModel.LoadModelsQuietlyAsync();
            }
            catch (Exception)
            {
                // Errors are only shown when the user explicitly refreshes.
            }
        }

        private void SyncApiKey()
        {
            _syncingKey = true;
            ApiKeyBox.Password = _viewModel.ApiKey ?? "";
            _syncingKey = false;
            UpdateKeyPlaceholder();
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_syncingKey)
                _viewModel.ApiKey = ApiKeyBox.Password;
            UpdateKeyPlaceholder();
        }

        private void UpdateKeyPlaceholder()
        {
            ApiKeyPlaceholder.Visibility = string.IsNullOrEmpty(ApiKeyBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
