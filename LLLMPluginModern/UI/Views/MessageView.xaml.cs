using System;
using System.Diagnostics;
using System.Windows;
using LLLMPluginModern.UI.Controls;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern.UI.Views
{
    public partial class MessageView : GlassWindow
    {
        private readonly MessageViewModel _viewModel;

        public MessageView(MessageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            viewModel.CloseAction = ok => DialogHelpers.CloseWithResult(this, ok);

            switch (viewModel.Kind)
            {
                case MessageKind.Error:
                case MessageKind.Warning:
                    BadgeIcon.Data = (System.Windows.Media.Geometry)FindResource("Icon.Warning");
                    BadgeIcon.IsFilled = false;
                    BadgeIcon.Fill = null;
                    BadgeIcon.SetResourceReference(Controls.Icon.ForegroundProperty, viewModel.Kind == MessageKind.Error ? "Brush.Danger" : "Brush.Warning");
                    Halo.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, viewModel.Kind == MessageKind.Error ? "Brush.DangerSoft" : "Brush.WarningSoft");
                    Halo.Opacity = 1;
                    break;
                case MessageKind.Info:
                    BadgeIcon.Data = (System.Windows.Media.Geometry)FindResource("Icon.Info");
                    BadgeIcon.IsFilled = false;
                    BadgeIcon.Fill = null;
                    BadgeIcon.SetResourceReference(Controls.Icon.ForegroundProperty, "Brush.Accent");
                    Halo.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Brush.AccentSoft");
                    Halo.Opacity = 1;
                    break;
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_viewModel.LinkUrl) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // No default browser configured; nothing else to do.
            }
        }
    }
}
