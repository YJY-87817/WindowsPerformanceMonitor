using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PerformanceMonitor.App.Converters;

/// <summary>值為 null 時收起（<see cref="Visibility.Collapsed"/>），否則顯示。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
