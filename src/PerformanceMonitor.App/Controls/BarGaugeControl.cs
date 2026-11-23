using System.Windows;
using System.Windows.Media;

namespace PerformanceMonitor.App.Controls;

/// <summary>
/// 橫條圖：底色軌道加上依比例填色的長條，可選一條門檻刻線。顏色取自主題資源，達告警條件時改用錯誤色。
/// 只負責繪製；數值與是否告警由 Presentation 計算。
/// </summary>
public sealed class BarGaugeControl : FrameworkElement
{
    /// <summary>長條比例（0～1）；null 時不畫（無法取得數值）。</summary>
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio), typeof(double?), typeof(BarGaugeControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>門檻刻線位置（0～1）；null 時不畫。</summary>
    public static readonly DependencyProperty MarkerRatioProperty = DependencyProperty.Register(
        nameof(MarkerRatio), typeof(double?), typeof(BarGaugeControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>是否已達告警條件（長條改為錯誤色）。</summary>
    public static readonly DependencyProperty IsAlertProperty = DependencyProperty.Register(
        nameof(IsAlert), typeof(bool), typeof(BarGaugeControl), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double Radius = 3;

    /// <summary>長條比例。</summary>
    public double? Ratio
    {
        get => (double?)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    /// <summary>門檻刻線位置。</summary>
    public double? MarkerRatio
    {
        get => (double?)GetValue(MarkerRatioProperty);
        set => SetValue(MarkerRatioProperty, value);
    }

    /// <summary>是否已達告警條件。</summary>
    public bool IsAlert
    {
        get => (bool)GetValue(IsAlertProperty);
        set => SetValue(IsAlertProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Ratio is not double ratio || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double height = Math.Min(ActualHeight, 10);
        double top = (ActualHeight - height) / 2;
        var track = new Rect(0, top, ActualWidth, height);
        drawingContext.DrawRoundedRectangle((Brush)FindResource("BorderBrush"), null, track, Radius, Radius);

        double width = Math.Clamp(ratio, 0, 1) * ActualWidth;
        if (width > 0)
        {
            Brush fill = IsAlert ? (Brush)FindResource("ErrorBrush") : SeriesBrush.For(0);
            drawingContext.DrawRoundedRectangle(fill, null, new Rect(0, top, Math.Max(width, Radius * 2), height), Radius, Radius);
        }

        if (MarkerRatio is double marker)
        {
            var pen = new Pen((Brush)FindResource("TextBrush"), 1.5);
            pen.Freeze();
            double x = Math.Clamp(marker, 0, 1) * ActualWidth;
            drawingContext.DrawLine(pen, new Point(x, top - 2), new Point(x, top + height + 2));
        }
    }
}
