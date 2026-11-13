using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.App.Converters;

/// <summary>狀態分類 → 顏色（取自 App.xaml 的主題資源）。</summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value switch
        {
            StatusKind.Ok => "OkBrush",
            StatusKind.Pending => "PendingBrush",
            StatusKind.Error => "ErrorBrush",
            _ => "InactiveBrush",
        };
        return System.Windows.Application.Current.FindResource(key);
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
