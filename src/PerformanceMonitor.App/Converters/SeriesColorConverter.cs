using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PerformanceMonitor.App.Controls;

namespace PerformanceMonitor.App.Converters;

/// <summary>線的顏色編號 → 顏色（圖例使用，與圖上的線相同）。</summary>
public sealed class SeriesColorConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        SeriesBrush.For(value is int index ? index : 0);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
