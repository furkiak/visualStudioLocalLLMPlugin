using System.Windows;
using System.Windows.Input;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class MainView : Window
    {
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            AppearanceController.Apply(this, viewModel.TextScalePercent);
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(viewModel.TextScalePercent))
                    AppearanceController.Apply(this, viewModel.TextScalePercent);
            };
            if (!string.IsNullOrEmpty(viewModel.ProfileNotice)) Title = "ISOLATED HTTP FIXTURE · Local LLM tools";

            viewModel.CloseAction = isOk =>
            {
                if (isOk && !viewModel.CanApply) return;
                DialogResult = isOk;
            };
            Closed += (sender, args) => viewModel.OnClosed();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtPrompt.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            if (e.Key == Key.Escape)
            {
                if (vm.CancelCommand.CanExecute(null)) vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && txtPrompt.IsKeyboardFocusWithin)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (vm.ApplyCommand.CanExecute(null)) vm.ApplyCommand.Execute(null);
                    e.Handled = true;
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                   
                }
                else if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (vm.SendAiCommand.CanExecute(null)) vm.SendAiCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
