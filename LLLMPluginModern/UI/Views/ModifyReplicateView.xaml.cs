using System.Windows;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class ModifyReplicateView : Window
    {
        public ModifyReplicateView(ModifyReplicateViewModel viewModel)
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