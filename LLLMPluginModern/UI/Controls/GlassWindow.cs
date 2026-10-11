using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using LLLMPluginModern.UI.Theming;

namespace LLLMPluginModern.UI.Controls
{
    /// <summary>
    /// Borderless window with the Liquid Glass look: custom chrome with macOS style traffic lights,
    /// native Windows 11 rounded corners and shadow, and the acrylic backdrop when available.
    /// The title bar area (top <see cref="TitleBarHeight"/> pixels) drags the window.
    /// </summary>
    public class GlassWindow : Window
    {
        public static readonly DependencyProperty IsBackdropActiveProperty = DependencyProperty.Register(
            nameof(IsBackdropActive), typeof(bool), typeof(GlassWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
            nameof(TitleBarHeight), typeof(double), typeof(GlassWindow), new PropertyMetadata(52.0));

        public static readonly DependencyProperty CanMinimizeProperty = DependencyProperty.Register(
            nameof(CanMinimize), typeof(bool), typeof(GlassWindow), new PropertyMetadata(false));

        private readonly bool _useBackdrop;

        public GlassWindow()
        {
            ThemeManager.Apply(this);
            _useBackdrop = ThemeManager.UseTranslucency && NativeMethods.SupportsBackdrop;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = _useBackdrop ? new Thickness(-1) : new Thickness(0, 0, 0, 1),
                UseAeroCaptionButtons = false
            });

            SetResourceReference(StyleProperty, "GlassWindowStyle");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            ShowInTaskbar = false;
        }

        /// <summary>True when the Windows 11 acrylic backdrop is rendering behind the window.</summary>
        public bool IsBackdropActive
        {
            get => (bool)GetValue(IsBackdropActiveProperty);
            private set => SetValue(IsBackdropActiveProperty, value);
        }

        public double TitleBarHeight
        {
            get => (double)GetValue(TitleBarHeightProperty);
            set => SetValue(TitleBarHeightProperty, value);
        }

        /// <summary>Modal dialogs cannot be minimized (Visual Studio would stay disabled with nothing visible).</summary>
        public bool CanMinimize
        {
            get => (bool)GetValue(CanMinimizeProperty);
            set => SetValue(CanMinimizeProperty, value);
        }

        public bool CanZoom => ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetDarkMode(hwnd, ThemeManager.IsDark);
            NativeMethods.SetRoundedCorners(hwnd);

            if (_useBackdrop)
            {
                var source = HwndSource.FromHwnd(hwnd);
                if (source?.CompositionTarget != null)
                    source.CompositionTarget.BackgroundColor = Colors.Transparent;
                IsBackdropActive = NativeMethods.TrySetAcrylicBackdrop(hwnd);
            }
        }

        /// <summary>Re-applies the palette after the user changed the theme in Settings.</summary>
        public void RefreshTheme()
        {
            ThemeManager.Apply(this);
            NativeMethods.SetDarkMode(new WindowInteropHelper(this).Handle, ThemeManager.IsDark);
        }

        public void ToggleMaximize()
        {
            if (CanZoom)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.Handled || e.GetPosition(this).Y > TitleBarHeight)
                return;

            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                e.Handled = true;
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The button was released before the drag started.
            }
        }
    }
}
