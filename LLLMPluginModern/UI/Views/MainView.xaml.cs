using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class MainView : GlassWindow
    {
        private readonly MainViewModel _viewModel;
        private IInputElement _lastTextFocus;
        private bool _autoScroll = true;

        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            CanMinimize = false;

            viewModel.CloseAction = CloseWithResult;
            viewModel.SelectedResponseTextProvider = GetHighlightedAnswerText;
            viewModel.SettingsChanged += (s, e) => RefreshTheme();
            viewModel.Messages.CollectionChanged += OnMessagesChanged;

            ApplyPreferredSize(viewModel.PreferredWidth, viewModel.PreferredHeight);

            PreviewGotKeyboardFocus += OnPreviewGotKeyboardFocus;
            ConversationScroll.ScrollChanged += OnConversationScrollChanged;
            Loaded += (s, e) => PromptBox.Focus();
            Closing += (s, e) =>
            {
                if (_viewModel.IsBusy && _viewModel.StopCommand.CanExecute(null))
                    _viewModel.StopCommand.Execute(null);
                if (WindowState == WindowState.Normal)
                    _viewModel.SaveWindowSize(ActualWidth, ActualHeight);
            };
        }

        private void CloseWithResult(bool ok)
        {
            try
            {
                DialogResult = ok;
            }
            catch (InvalidOperationException)
            {
                // Not shown as a dialog (e.g. design-time preview).
                Close();
            }
        }

        /// <summary>Restores the last size, clamped to the current screen so the footer is never off-screen.</summary>
        private void ApplyPreferredSize(double width, double height)
        {
            var area = SystemParameters.WorkArea;
            if (width >= MinWidth)
                Width = width;
            if (height >= MinHeight)
                Height = height;
            Width = Math.Min(Width, Math.Max(MinWidth, area.Width * 0.95));
            Height = Math.Min(Height, Math.Max(MinHeight, area.Height * 0.92));
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled)
                return;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape && modifiers == ModifierKeys.None)
            {
                Execute(_viewModel.CancelCommand);
                e.Handled = true;
            }
            else if (key == Key.Enter && modifiers == ModifierKeys.Shift)
            {
                Execute(_viewModel.ApplyCommand);
                e.Handled = true;
            }
            else if (key == Key.Enter && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                Execute(_viewModel.InsertBelowCommand);
                e.Handled = true;
            }
            else if (key == Key.OemComma && modifiers == ModifierKeys.Control)
            {
                Execute(_viewModel.SettingsCommand);
                e.Handled = true;
            }
            else if (key == Key.N && modifiers == ModifierKeys.Control)
            {
                Execute(_viewModel.NewChatCommand);
                PromptBox.Focus();
                e.Handled = true;
            }
        }

        private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                Execute(_viewModel.SendCommand);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                int caret = PromptBox.CaretIndex;
                PromptBox.SelectedText = Environment.NewLine;
                PromptBox.CaretIndex = caret + Environment.NewLine.Length;
                PromptBox.SelectionLength = 0;
                e.Handled = true;
            }
        }

        private static void Execute(ICommand command)
        {
            if (command.CanExecute(null))
                command.Execute(null);
        }

        private void ToolsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(FindResource("ToolsMenu") is ContextMenu menu))
                return;
            menu.DataContext = DataContext;
            menu.PlacementTarget = ToolsButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        // ---- Partial apply: remember the answer text the user highlighted -------------------------

        private void OnPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Clicking a button must not forget the selection the user just made in an answer.
            if (e.NewFocus is ButtonBase)
                return;
            _lastTextFocus = e.NewFocus;
        }

        private string GetHighlightedAnswerText()
        {
            if (!(_lastTextFocus is DependencyObject element) || !IsInsideConversation(element))
                return null;

            switch (_lastTextFocus)
            {
                case TextBox textBox when textBox.SelectionLength > 0:
                    return textBox.SelectedText;
                case RichTextBox richTextBox when !richTextBox.Selection.IsEmpty:
                    return richTextBox.Selection.Text;
                default:
                    return null;
            }
        }

        private bool IsInsideConversation(DependencyObject element)
        {
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, Conversation))
                    return true;
            }
            return false;
        }

        // ---- Keep the newest answer in view while it streams ---------------------------------------

        private void OnMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                _autoScroll = true;
                ConversationScroll.ScrollToEnd();
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _lastTextFocus = null;
            }
        }

        private void OnConversationScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
                _autoScroll = ConversationScroll.VerticalOffset >= ConversationScroll.ScrollableHeight - 24;
            else if (_autoScroll)
                ConversationScroll.ScrollToVerticalOffset(ConversationScroll.ExtentHeight);
        }
    }

    public sealed class ChatTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserTemplate { get; set; }
        public DataTemplate AssistantTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item is ChatItemViewModel chat && chat.IsUser ? UserTemplate : AssistantTemplate;
        }
    }

    public sealed class BlockTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate CodeTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item is ResponseBlockViewModel block && block.IsCode ? CodeTemplate : TextTemplate;
        }
    }
}
