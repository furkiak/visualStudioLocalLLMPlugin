using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LLLMPluginModern.UI.Behaviors
{
    /// <summary>Placeholder text shown by the themed TextBox template while the box is empty.</summary>
    public static class TextBoxHelper
    {
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
            "Placeholder", typeof(string), typeof(TextBoxHelper), new FrameworkPropertyMetadata(null));

        public static string GetPlaceholder(DependencyObject element) => (string)element.GetValue(PlaceholderProperty);

        public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Text boxes inside the conversation swallow mouse-wheel events even when they cannot scroll.
    /// This forwards the wheel to the parent when the inner content is at its top/bottom.
    /// </summary>
    public static class ScrollBehavior
    {
        public static readonly DependencyProperty BubbleMouseWheelProperty = DependencyProperty.RegisterAttached(
            "BubbleMouseWheel", typeof(bool), typeof(ScrollBehavior), new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject element) => (bool)element.GetValue(BubbleMouseWheelProperty);

        public static void SetBubbleMouseWheel(DependencyObject element, bool value) => element.SetValue(BubbleMouseWheelProperty, value);

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UIElement element))
                return;
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue)
                element.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || !(sender is DependencyObject element))
                return;

            var inner = FindChild<ScrollViewer>(element);
            if (inner != null && inner.ScrollableHeight > 0)
            {
                bool atTop = inner.VerticalOffset <= 0;
                bool atBottom = inner.VerticalOffset >= inner.ScrollableHeight - 0.5;
                if ((e.Delta > 0 && !atTop) || (e.Delta < 0 && !atBottom))
                    return;
            }

            e.Handled = true;
            var parent = VisualTreeHelper.GetParent(element) as UIElement;
            parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            });
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var nested = FindChild<T>(child);
                if (nested != null)
                    return nested;
            }
            return null;
        }
    }
}
