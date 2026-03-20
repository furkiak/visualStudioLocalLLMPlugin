using System.Windows;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class InputDialogView : Window
    {
        public InputDialogView(InputDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.CloseAction = (isOk) =>
            {
                this.DialogResult = isOk;
                this.Close();
            };
        }
    }
}