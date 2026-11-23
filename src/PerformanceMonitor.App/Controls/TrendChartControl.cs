using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PerformanceMonitor.Presentation.Trends;

namespace PerformanceMonitor.App.Controls;

/// <summary>
/// WPF 原生折線圖（Phase 3 確認不使用第三方圖表套件）：依 <see cref="TrendChart"/> 的正規化座標繪製格線與各線段；
/// 圖例與標題由 XAML 呈現。資料計算在 Presentation 的 <see cref="TrendChartBuilder"/>。
/// 6a：每分鐘時間刻度、告警門檻線、滑鼠停留顯示數值；<see cref="Compact"/> 模式只畫線（總覽迷你走勢用）。
/// </summary>
public sealed class TrendChartControl : FrameworkElement
{
    /// <summary>要繪製的圖。</summary>
    public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
        nameof(Chart), typeof(TrendChart), typeof(TrendChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChartChanged));

    /// <summary>精簡模式：不畫座標軸、刻度、格線與停留提示，只畫線。</summary>
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(TrendChartControl), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double AxisWidth = 56;
    private const double BottomMargin = 18;
    private const double CompactMargin = 2;

    private readonly ToolTip _tip = new() { Placement = PlacementMode.Relative, StaysOpen = false };
    private double? _hoverX;
    private Point _mouse;
    private Window? _window;

    /// <summary>建立圖；提示框是手動開啟的，所以在卸載（換頁）與視窗失去焦點時必須自行關閉。</summary>
    public TrendChartControl()
    {
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is not null)
            {
                _window.Deactivated += OnWindowDeactivated;
            }
        };
        Unloaded += (_, _) =>
        {
            if (_window is not null)
            {
                _window.Deactivated -= OnWindowDeactivated;
                _window = null;
            }

            CloseHover();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
            {
                CloseHover();
            }
        };
    }

    /// <summary>要繪製的圖。</summary>
    public TrendChart? Chart
    {
        get => (TrendChart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <summary>精簡模式。</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        if (Compact)
        {
            return;
        }

        Rect plot = PlotRect();
        _mouse = e.GetPosition(this);
        _hoverX = plot.Width > 0 && plot.Contains(_mouse) ? (_mouse.X - plot.Left) / plot.Width : null;
        UpdateHover();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverX = null;
        UpdateHover();
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        Brush grid = (Brush)FindResource("BorderBrush");
        Brush text = (Brush)FindResource("SecondaryTextBrush");
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Rect plot = PlotRect();
        if (Compact)
        {
            // 透明底，讓卡片底色透出來；只畫線。
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        }
        else
        {
            drawingContext.DrawRectangle((Brush)FindResource("SurfaceBrush"), new Pen(grid, 1), plot);
        }

        TrendChart? chart = Chart;
        if (chart is null || plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        if (!Compact)
        {
            DrawAxes(drawingContext, chart, plot, grid, text, dpi);
        }

        if (!chart.HasData)
        {
            if (!Compact)
            {
                var empty = new FormattedText(chart.EmptyText ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, text, dpi);
                drawingContext.DrawText(empty, new Point(plot.Left + ((plot.Width - empty.Width) / 2), plot.Top + ((plot.Height - empty.Height) / 2)));
            }

            return;
        }

        drawingContext.PushClip(new RectangleGeometry(plot));
        if (!Compact)
        {
            DrawReferenceLines(drawingContext, chart, plot, dpi);
        }

        foreach (ChartSeries series in chart.Series)
        {
            var pen = new Pen(SeriesBrush.For(series.ColorIndex), Compact ? 1.4 : 1.6) { LineJoin = PenLineJoin.Round };
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

        if (!Compact && _hoverX is double hoverX)
        {
            DrawHover(drawingContext, chart, plot, hoverX, text);
        }

        drawingContext.Pop();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => CloseHover();

    private void CloseHover()
    {
        _hoverX = null;
        _tip.IsOpen = false;
        InvalidateVisual();
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TrendChartControl)d).UpdateHover();

    private Rect PlotRect() => Compact
        ? new Rect(CompactMargin, CompactMargin, Math.Max(0, ActualWidth - (2 * CompactMargin)), Math.Max(0, ActualHeight - (2 * CompactMargin)))
        : new Rect(AxisWidth, 4, Math.Max(0, ActualWidth - AxisWidth - 8), Math.Max(0, ActualHeight - BottomMargin - 4));

    private void UpdateHover()
    {
        string? text = _hoverX is double x ? Chart?.HoverText(x) : null;
        if (text is null)
        {
            _tip.IsOpen = false;
        }
        else
        {
            _tip.Content = text;
            _tip.PlacementTarget = this;
            _tip.HorizontalOffset = _mouse.X + 14;
            _tip.VerticalOffset = _mouse.Y + 18;
            _tip.IsOpen = true;
        }

        InvalidateVisual();
    }

    private static void DrawAxes(DrawingContext dc, TrendChart chart, Rect plot, Brush grid, Brush text, double dpi)
    {
        // 水平格線與縱軸刻度（0、1/4、1/2、3/4、最大值）
        var gridPen = new Pen(grid, 0.5) { DashStyle = DashStyles.Dot };
        for (int i = 0; i <= 4; i++)
        {
            double y = plot.Bottom - (plot.Height * i / 4);
            if (i is > 0 and < 4)
            {
                dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            }

            string label = (chart.YMax * i / 4).ToString("0.##", CultureInfo.InvariantCulture);
            var formatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, text, dpi);
            dc.DrawText(formatted, new Point(plot.Left - formatted.Width - 6, y - (formatted.Height / 2)));
        }

        // 每分鐘一條垂直格線與時間刻度（−5 分 … −1 分、最新）
        int minutes = (int)TrendHistory.Window.TotalMinutes;
        for (int i = 0; i <= minutes; i++)
        {
            double x = plot.Left + (plot.Width * i / minutes);
            if (i > 0 && i < minutes)
            {
                dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            }

            string label = i == minutes ? "最新" : $"−{minutes - i} 分";
            DrawTimeLabel(dc, label, x, plot, text, dpi, i == minutes ? LabelAlign.Right : i == 0 ? LabelAlign.Left : LabelAlign.Center);
        }
    }

    private void DrawReferenceLines(DrawingContext dc, TrendChart chart, Rect plot, double dpi)
    {
        Brush error = (Brush)FindResource("ErrorBrush");
        var pen = new Pen(error, 1) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        foreach (ChartReferenceLine line in chart.ReferenceLines)
        {
            double y = plot.Bottom - (line.Y * plot.Height);
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = new FormattedText(line.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, error, dpi);
            double labelY = y - label.Height - 1 < plot.Top ? y + 1 : y - label.Height - 1;
            dc.DrawText(label, new Point(plot.Right - label.Width - 4, labelY));
        }
    }

    private static void DrawHover(DrawingContext dc, TrendChart chart, Rect plot, double hoverX, Brush cursor)
    {
        double x = plot.Left + (hoverX * plot.Width);
        var cursorPen = new Pen(cursor, 1);
        cursorPen.Freeze();
        dc.DrawLine(cursorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        foreach (HoverHit hit in chart.HoverHits(hoverX))
        {
            var point = new Point(
                plot.Left + (hit.Point.X * plot.Width),
                plot.Bottom - (Math.Clamp(hit.Point.Value / chart.YMax, 0, 1) * plot.Height));
            dc.DrawEllipse(SeriesBrush.For(hit.Series.ColorIndex), new Pen(Brushes.White, 1), point, 3.5, 3.5);
        }
    }

    private static Point ToScreen(ChartPoint point, Rect plot) =>
        new(plot.Left + (point.X * plot.Width), plot.Bottom - (point.Y * plot.Height));

    private enum LabelAlign
    {
        Left,
        Center,
        Right,
    }

    private static void DrawTimeLabel(DrawingContext dc, string label, double x, Rect plot, Brush brush, double dpi, LabelAlign align)
    {
        var formatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, brush, dpi);
        double left = align switch
        {
            LabelAlign.Right => x - formatted.Width,
            LabelAlign.Center => x - (formatted.Width / 2),
            _ => x,
        };
        dc.DrawText(formatted, new Point(left, plot.Bottom + 2));
    }
}

/// <summary>線的顏色（深色背景上容易辨識、色盲較易分辨的組合）。</summary>
public static class SeriesBrush
{
    private static readonly Brush[] Palette =
    [
        Frozen("#58A6FF"), Frozen("#FF7B8A"), Frozen("#56D364"), Frozen("#F0B341"), Frozen("#BC8CFF"), Frozen("#39C5CF"),
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
