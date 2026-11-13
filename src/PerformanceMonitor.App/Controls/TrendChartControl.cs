using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PerformanceMonitor.Presentation.Trends;

namespace PerformanceMonitor.App.Controls;

/// <summary>
/// WPF 原生折線圖（Phase 3 確認不使用第三方圖表套件）：依 <see cref="TrendChart"/> 的正規化座標繪製格線與各線段；
/// 圖例與標題由 XAML 呈現。資料計算在 Presentation 的 <see cref="TrendChartBuilder"/>。
/// </summary>
public sealed class TrendChartControl : FrameworkElement
{
    /// <summary>要繪製的圖。</summary>
    public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
        nameof(Chart), typeof(TrendChart), typeof(TrendChartControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double AxisWidth = 56;
    private const double BottomMargin = 18;

    /// <summary>要繪製的圖。</summary>
    public TrendChart? Chart
    {
        get => (TrendChart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        Brush grid = (Brush)FindResource("BorderBrush");
        Brush text = (Brush)FindResource("SecondaryTextBrush");
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var plot = new Rect(AxisWidth, 4, Math.Max(0, ActualWidth - AxisWidth - 8), Math.Max(0, ActualHeight - BottomMargin - 4));
        drawingContext.DrawRectangle((Brush)FindResource("SurfaceBrush"), new Pen(grid, 1), plot);

        TrendChart? chart = Chart;
        if (chart is null || plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        // 水平格線與縱軸刻度（0、25、50、75、100%）
        var gridPen = new Pen(grid, 0.5) { DashStyle = DashStyles.Dot };
        for (int i = 0; i <= 4; i++)
        {
            double y = plot.Bottom - (plot.Height * i / 4);
            if (i is > 0 and < 4)
            {
                drawingContext.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            }

            string label = (chart.YMax * i / 4).ToString("0.##", CultureInfo.InvariantCulture);
            var formatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, text, dpi);
            drawingContext.DrawText(formatted, new Point(plot.Left - formatted.Width - 6, y - (formatted.Height / 2)));
        }

        DrawTimeLabel(drawingContext, $"−{TrendHistory.Window.TotalMinutes:0} 分", plot.Left, plot, text, dpi, alignRight: false);
        DrawTimeLabel(drawingContext, "最新", plot.Right, plot, text, dpi, alignRight: true);

        if (!chart.HasData)
        {
            var empty = new FormattedText(chart.EmptyText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, text, dpi);
            drawingContext.DrawText(empty, new Point(plot.Left + ((plot.Width - empty.Width) / 2), plot.Top + ((plot.Height - empty.Height) / 2)));
            return;
        }

        drawingContext.PushClip(new RectangleGeometry(plot));
        foreach (ChartSeries series in chart.Series)
        {
            var pen = new Pen(SeriesBrush.For(series.ColorIndex), 1.6) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            foreach (IReadOnlyList<ChartPoint> segment in series.Segments)
            {
                if (segment.Count == 1)
                {
                    drawingContext.DrawEllipse(pen.Brush, null, ToScreen(segment[0], plot), 1.6, 1.6);
                    continue;
                }

                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(ToScreen(segment[0], plot), isFilled: false, isClosed: false);
                    context.PolyLineTo([.. segment.Skip(1).Select(p => ToScreen(p, plot))], isStroked: true, isSmoothJoin: true);
                }

                geometry.Freeze();
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }

        drawingContext.Pop();
    }

    private static Point ToScreen(ChartPoint point, Rect plot) =>
        new(plot.Left + (point.X * plot.Width), plot.Bottom - (point.Y * plot.Height));

    private static void DrawTimeLabel(DrawingContext dc, string label, double x, Rect plot, Brush brush, double dpi, bool alignRight)
    {
        var formatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, brush, dpi);
        dc.DrawText(formatted, new Point(alignRight ? x - formatted.Width : x, plot.Bottom + 2));
    }
}

/// <summary>線的顏色（淺色主題、色盲較易分辨的組合）。</summary>
public static class SeriesBrush
{
    private static readonly Brush[] Palette =
    [
        Frozen("#1F6FEB"), Frozen("#D1495B"), Frozen("#2E933C"), Frozen("#E89005"), Frozen("#7A4EAB"), Frozen("#00A6A6"),
    ];

    /// <summary>依編號取色（循環使用）。</summary>
    public static Brush For(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];

    private static SolidColorBrush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
