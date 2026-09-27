using System.Windows;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class SettingsView : Window
    {
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            AppearanceController.Apply(this, viewModel.TextScalePercent);
            if (!string.IsNullOrEmpty(viewModel.ProfileNotice)) Title = "ISOLATED HTTP FIXTURE · Settings";

            txtApiKey.Password = viewModel.ApiKey;
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(viewModel.ApiKey) && txtApiKey.Password != viewModel.ApiKey)
                    txtApiKey.Password = viewModel.ApiKey ?? "";
                if (args.PropertyName == nameof(viewModel.TextScalePercent) &&
                    (viewModel.TextScalePercent == 100 || viewModel.TextScalePercent == 150 || viewModel.TextScalePercent == 200))
                    AppearanceController.Apply(this, viewModel.TextScalePercent);
            };

            viewModel.CloseAction = () => this.Close();
        }

        private void txtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.ApiKey = txtApiKey.Password;
            }
        }
    }
}
