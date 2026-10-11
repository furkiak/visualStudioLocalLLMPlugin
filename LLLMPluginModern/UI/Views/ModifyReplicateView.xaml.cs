using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class ModifyReplicateView : GlassWindow
    {
        public ModifyReplicateView(ModifyReplicateViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = ok => DialogHelpers.CloseWithResult(this, ok);
            Loaded += (s, e) => TargetBox.Focus();
        }
    }
}
