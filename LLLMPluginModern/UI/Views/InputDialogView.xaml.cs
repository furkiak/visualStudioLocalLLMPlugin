using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class InputDialogView : GlassWindow
    {
        public InputDialogView(InputDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = ok => DialogHelpers.CloseWithResult(this, ok);
            Loaded += (s, e) => InputBox.Focus();
        }
    }
}
