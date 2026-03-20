using System.Windows;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class AddView : Window
    {
        public AddView(AddViewModel viewModel)
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