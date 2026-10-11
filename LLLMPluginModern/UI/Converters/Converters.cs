using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LLLMPluginModern.UI.Converters
{
    /// <summary>true → Visible. With Invert, true → Collapsed. Strings count as true when non-empty.</summary>
    public sealed class VisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag;
            switch (value)
            {
                case bool b:
                    flag = b;
                    break;
                case string s:
                    flag = s.Length > 0;
                    break;
                case int i:
                    flag = i > 0;
                    break;
                default:
                    flag = value != null;
                    break;
            }
            return flag ^ Invert ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && (visibility == Visibility.Visible) ^ Invert;
        }
    }

    /// <summary>Resolves an icon resource key (e.g. "Icon.Bug") to its geometry.</summary>
    public sealed class IconKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Theming.ThemeManager.FindIcon(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Height → CornerRadius(height / 2): a true capsule at any width. (A huge fixed radius makes WPF
    /// scale the corners per side, which turns wide buttons into ellipses.)
    /// </summary>
    public sealed class CapsuleRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double height = value is double d && !double.IsNaN(d) ? d : 0;
            return new CornerRadius(Math.Max(0, height / 2));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    }
}
