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

            txtApiKey.Password = viewModel.ApiKey;

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