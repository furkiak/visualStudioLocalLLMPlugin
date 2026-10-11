using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LLLMPluginModern.UI.Controls
{
    /// <summary>macOS style window buttons: close, minimize and zoom. Unavailable actions are greyed out.</summary>
    [TemplatePart(Name = "PART_Close", Type = typeof(ButtonBase))]
    [TemplatePart(Name = "PART_Minimize", Type = typeof(ButtonBase))]
    [TemplatePart(Name = "PART_Zoom", Type = typeof(ButtonBase))]
    public sealed class TrafficLights : Control
    {
        private ButtonBase _close;
        private ButtonBase _minimize;
        private ButtonBase _zoom;

        public TrafficLights()
        {
            SetResourceReference(StyleProperty, "TrafficLightsStyle");
        }

        public override void OnApplyTemplate()
        {
            if (_close != null)
                _close.Click -= OnClose;
            if (_minimize != null)
                _minimize.Click -= OnMinimize;
            if (_zoom != null)
                _zoom.Click -= OnZoom;

            base.OnApplyTemplate();

            _close = GetTemplateChild("PART_Close") as ButtonBase;
            _minimize = GetTemplateChild("PART_Minimize") as ButtonBase;
            _zoom = GetTemplateChild("PART_Zoom") as ButtonBase;

            if (_close != null)
                _close.Click += OnClose;
            if (_minimize != null)
                _minimize.Click += OnMinimize;
            if (_zoom != null)
                _zoom.Click += OnZoom;

            var window = Window.GetWindow(this) as GlassWindow;
            if (_minimize != null)
                _minimize.IsEnabled = window?.CanMinimize == true;
            if (_zoom != null)
                _zoom.IsEnabled = window?.CanZoom == true;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
                window.WindowState = WindowState.Minimized;
        }

        private void OnZoom(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as GlassWindow)?.ToggleMaximize();
    }
}
