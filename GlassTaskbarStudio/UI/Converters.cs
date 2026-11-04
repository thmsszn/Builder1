using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GlassTaskbarStudio.Core;

namespace GlassTaskbarStudio.UI
{
    /// <summary>Two-way bridge between an enum property and a RadioButton (ConverterParameter = enum member name).</summary>
    public sealed class EnumMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? Enum.Parse(targetType, parameter.ToString()) : Binding.DoNothing;
    }

    public sealed class HexBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new SolidColorBrush(ColorUtil.Parse(value as string, Colors.Transparent));
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class BadgeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? "").ToUpperInvariant())
            {
                case "NATIVE": return new SolidColorBrush(Color.FromRgb(0x23, 0xA6, 0x5E));
                case "REGION": return new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xE0));
                case "OVERLAY": return new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xE0));
                case "REGISTRY": return new SolidColorBrush(Color.FromRgb(0xC9, 0x84, 0x10));
                case "APP": return new SolidColorBrush(Color.FromRgb(0x5B, 0x66, 0x82));
                default: return new SolidColorBrush(Color.FromRgb(0x5B, 0x66, 0x82));
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class NotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    }
}
