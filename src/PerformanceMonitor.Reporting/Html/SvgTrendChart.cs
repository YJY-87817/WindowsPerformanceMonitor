using System.Globalization;
using System.Text;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Html;

/// <summary>一張趨勢圖的定義。</summary>
/// <param name="Title">標題。</param>
/// <param name="Unit">單位。</param>
/// <param name="Roles">畫哪些角色的數列。</param>
/// <param name="FixedMax">固定的縱軸最大值（百分比用 100）；null 時依資料自動。</param>
/// <param name="EmptyText">沒有資料時的說明。</param>
/// <param name="Note">圖下方的補充說明。</param>
public sealed record TrendChartDefinition(string Title, string Unit, IReadOnlyList<SeriesRole> Roles, double? FixedMax, string EmptyText, string? Note = null)
{
    /// <summary>報告的 6 張趨勢圖（決策 0008）。</summary>
    public static IReadOnlyList<TrendChartDefinition> All { get; } =
    [
        new("CPU 使用率", "%", [SeriesRole.CpuUsage], 100, "期間內沒有 CPU 使用率資料"),
        new("記憶體使用率", "%", [SeriesRole.MemoryUsage], 100, "期間內沒有記憶體使用率資料"),
        new("邏輯磁碟忙碌率", "%", [SeriesRole.LogicalDiskBusy], 100, "期間內沒有磁碟 I/O 資料"),
        new("網路合計", "Mbps", [SeriesRole.NetworkDownload, SeriesRole.NetworkUpload], null, "期間內沒有網路資料（沒有符合篩選條件的介面卡或模組停用）"),
        new("GPU 使用率", "%", [SeriesRole.GpuUsage], 100, "期間內沒有 GPU 資料（沒有 NVIDIA GPU 或模組停用）"),
        new("溫度（GPU 與 ACPI 熱區）", "°C", [SeriesRole.Temperature], null, "期間內沒有溫度資料（沒有 NVIDIA GPU 與 ACPI 熱區）", "ACPI 熱區不是 CPU 核心溫度。"),
    ];
}

/// <summary>
/// 內嵌 SVG 趨勢圖（不使用 JavaScript）：每段平均為實線、每段最大為淡色線；沒有資料的段斷開，不連成假線。
/// 滑鼠停留在某段時以 SVG <c>&lt;title&gt;</c> 顯示該段各數列的平均與最大。
/// </summary>
public static class SvgTrendChart
{
    /// <summary>數列顏色（依圖例順序）。</summary>
    public static IReadOnlyList<string> Palette { get; } = ["#0969da", "#cf222e", "#1a7f37", "#8250df", "#bf8700", "#0a7d8c", "#bc4c00", "#57606a"];

    private const double Width = 960;
    private const double Height = 240;
    private const double Left = 56;
    private const double Right = 16;
    private const double Top = 12;
    private const double Bottom = 34;
    private const double PlotWidth = Width - Left - Right;
    private const double PlotHeight = Height - Top - Bottom;

    /// <summary>不小於 <paramref name="value"/> 的 1、2、5 × 10ⁿ（縱軸刻度好讀）。</summary>
    public static double NiceCeiling(double value)
    {
        if (value <= 0 || !double.IsFinite(value))
        {
            return 1;
        }

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (double step in (double[])[1, 2, 5, 10])
        {
            if (step * magnitude >= value - (magnitude * 1e-9))
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }

    /// <summary>產生一張圖的 HTML（標題、SVG、圖例）；沒有資料時只有標題與說明。</summary>
    public static string Render(TrendChartDefinition chart, IReadOnlyList<SeriesStatistics> series, ReportTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(timeline);
        SeriesStatistics[] lines = [.. chart.Roles.SelectMany(r => series.Where(s => s.Key.Role == r && s.Buckets.Any(b => b is not null)))];
        var html = new StringBuilder();
        html.Append("<figure class=\"chart\">\n<figcaption>").Append(HtmlReportRenderer.H(chart.Title)).Append('（').Append(HtmlReportRenderer.H(chart.Unit)).Append("）</figcaption>\n");
        if (lines.Length == 0)
        {
            html.Append("<p class=\"note\">").Append(HtmlReportRenderer.H(chart.EmptyText)).Append("</p>\n</figure>\n");
            return html.ToString();
        }

        double dataMax = lines.SelectMany(s => s.Buckets).OfType<TrendBucket>().Max(b => b.Max);
        double yMax = chart.FixedMax ?? NiceCeiling(Math.Max(dataMax * 1.1, chart.Unit == "°C" ? 50 : 1));
        int n = timeline.BucketCount;

        html.Append(Inv($"<svg viewBox=\"0 0 {Width} {Height}\" role=\"img\" aria-label=\"")).Append(HtmlReportRenderer.H(chart.Title)).Append("\">\n");

        // 格線與縱軸刻度。
        for (int i = 0; i <= 4; i++)
        {
            double y = Top + (PlotHeight * (1 - (i / 4.0)));
            html.Append(Inv($"<line class=\"grid\" x1=\"{Left}\" y1=\"{y:0.#}\" x2=\"{Width - Right}\" y2=\"{y:0.#}\"/>"))
                .Append(Inv($"<text class=\"axis\" x=\"{Left - 6}\" y=\"{y + 4:0.#}\" text-anchor=\"end\">{HtmlReportRenderer.Number(yMax * i / 4)}</text>\n"));
        }

        // 橫軸刻度。
        for (int i = 0; i <= 6; i++)
        {
            double x = Left + (PlotWidth * i / 6);
            DateTimeOffset time = timeline.ToLocal(timeline.Start + ((timeline.End - timeline.Start) * i / 6));
            string label = time.ToString(timeline.Granularity.TotalDays >= 1 ? "MM-dd" : timeline.Granularity.TotalMinutes <= 1 ? "HH:mm" : "MM-dd HH:mm", CultureInfo.InvariantCulture);
            string anchor = i == 0 ? "start" : i == 6 ? "end" : "middle";
            html.Append(Inv($"<line class=\"grid\" x1=\"{x:0.#}\" y1=\"{Top + PlotHeight}\" x2=\"{x:0.#}\" y2=\"{Top + PlotHeight + 4}\"/>"))
                .Append(Inv($"<text class=\"axis\" x=\"{x:0.#}\" y=\"{Height - 14}\" text-anchor=\"{anchor}\">{label}</text>\n"));
        }

        // 每條數列：先畫最大（淡色），再畫平均（實線）。
        for (int s = 0; s < lines.Length; s++)
        {
            string color = Palette[s % Palette.Count];
            foreach (string points in Segments(lines[s].Buckets, n, yMax, b => b.Max))
            {
                html.Append("<polyline class=\"max\" stroke=\"").Append(color).Append("\" points=\"").Append(points).Append("\"/>\n");
            }

            foreach (string points in Segments(lines[s].Buckets, n, yMax, b => b.Average))
            {
                html.Append("<polyline class=\"avg\" stroke=\"").Append(color).Append("\" points=\"").Append(points).Append("\"/>\n");
            }
        }

        // 滑鼠停留提示：每個有資料的段一個透明區塊。
        double width = PlotWidth / n;
        for (int i = 0; i < n; i++)
        {
            string[] parts =
            [
                .. lines.Where(l => i < l.Buckets.Count && l.Buckets[i] is not null)
                    .Select(l => $"{Label(l)}：平均 {HtmlReportRenderer.Number(l.Buckets[i]!.Value.Average)}，最大 {HtmlReportRenderer.Number(l.Buckets[i]!.Value.Max)}"),
            ];
            if (parts.Length == 0)
            {
                continue;
            }

            string time = timeline.ToLocal(timeline.BucketStart(i)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            html.Append(Inv($"<rect class=\"hit\" x=\"{Left + (i * width):0.##}\" y=\"{Top}\" width=\"{width:0.###}\" height=\"{PlotHeight}\"><title>"))
                .Append(HtmlReportRenderer.H($"{time}（{timeline.GranularityText}）\n{string.Join("\n", parts)}"))
                .Append("</title></rect>\n");
        }

        html.Append("</svg>\n<p class=\"legend\">");
        for (int s = 0; s < lines.Length; s++)
        {
            html.Append("<span><i style=\"background:").Append(Palette[s % Palette.Count]).Append("\"></i>").Append(HtmlReportRenderer.H(Label(lines[s]))).Append("</span>");
        }

        html.Append("<span class=\"note\">實線：每段平均；淡色線：每段最大</span></p>\n");
        if (chart.Note is not null)
        {
            html.Append("<p class=\"note\">").Append(HtmlReportRenderer.H(chart.Note)).Append("</p>\n");
        }

        html.Append("</figure>\n");
        return html.ToString();
    }

    private static string Label(SeriesStatistics s) => s.Key.Device ?? s.Key.Name;

    /// <summary>連續有資料的段組成一條折線（點在段中央）；只有一點時畫一小段水平線。</summary>
    private static IEnumerable<string> Segments(IReadOnlyList<TrendBucket?> buckets, int count, double yMax, Func<TrendBucket, double> value)
    {
        var current = new List<(double X, double Y)>();
        for (int i = 0; i <= count; i++)
        {
            if (i < count && i < buckets.Count && buckets[i] is TrendBucket b)
            {
                double x = Left + (PlotWidth * (i + 0.5) / count);
                double y = Top + (PlotHeight * (1 - Math.Clamp(value(b) / yMax, 0, 1)));
                current.Add((x, y));
                continue;
            }

            if (current.Count == 1)
            {
                double half = Math.Max(PlotWidth / count / 2, 1.5);
                current = [(current[0].X - half, current[0].Y), (current[0].X + half, current[0].Y)];
            }

            if (current.Count > 0)
            {
                yield return string.Join(' ', current.Select(p => Inv($"{p.X:0.##},{p.Y:0.##}")));
                current.Clear();
            }
        }
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
