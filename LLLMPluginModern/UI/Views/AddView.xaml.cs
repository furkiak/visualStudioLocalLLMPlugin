using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class AddView : GlassWindow
    {
        public AddView(AddViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = ok => DialogHelpers.CloseWithResult(this, ok);
            Loaded += (s, e) => AddBox.Focus();
        }
    }
}
