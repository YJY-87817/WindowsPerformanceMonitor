using System.Globalization;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.Presentation.Trends;

/// <summary>圖上的一點；座標已正規化到 0～1（X：時間由左到右；Y：0 在底部）。</summary>
/// <param name="X">水平位置。</param>
/// <param name="Y">垂直位置。</param>
public readonly record struct ChartPoint(double X, double Y);

/// <summary>滑鼠停留用的取樣點（座標已正規化；保留原始時間與數值）。</summary>
/// <param name="X">水平位置（0～1）。</param>
/// <param name="Time">取樣時間（UTC）。</param>
/// <param name="Value">原始數值。</param>
public readonly record struct HoverPoint(double X, DateTimeOffset Time, double Value);

/// <summary>滑鼠停留命中的線與取樣點。</summary>
/// <param name="Series">線。</param>
/// <param name="Point">最接近的取樣點。</param>
public readonly record struct HoverHit(ChartSeries Series, HoverPoint Point);

/// <summary>橫向參考線（例如告警門檻）。</summary>
/// <param name="Value">數值。</param>
/// <param name="Y">縱向位置（0～1，0 在底部）。</param>
/// <param name="Label">標籤文字。</param>
public sealed record ChartReferenceLine(double Value, double Y, string Label);

/// <summary>一條線。</summary>
/// <param name="Name">名稱（例如 <c>CPU</c>、<c>C:</c>、<c>GPU 0</c>）。</param>
/// <param name="ColorIndex">顏色編號（畫面依此取色）。</param>
/// <param name="Segments">連續的線段；沒有資料的時段斷開。</param>
/// <param name="CurrentText">最新數值文字（圖例使用）；最新一次無法取得時為「—」。</param>
public sealed record ChartSeries(string Name, int ColorIndex, IReadOnlyList<IReadOnlyList<ChartPoint>> Segments, string CurrentText)
{
    /// <summary>所有有數值的取樣點（由舊到新），滑鼠停留時使用。</summary>
    public IReadOnlyList<HoverPoint> Points { get; init; } = [];
}

/// <summary>一張趨勢圖。</summary>
/// <param name="Title">標題。</param>
/// <param name="YMax">縱軸最大值。</param>
/// <param name="YMaxText">縱軸最大值文字（含單位）。</param>
/// <param name="Series">各線。</param>
/// <param name="EmptyText">沒有任何資料時顯示的文字；有資料時為 null。</param>
public sealed record TrendChart(string Title, double YMax, string YMaxText, IReadOnlyList<ChartSeries> Series, string? EmptyText)
{
    /// <summary>單位（例如 <c>%</c>、<c>Mbps</c>）；停留提示使用。</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>橫向參考線（例如告警門檻）；沒有時為空。</summary>
    public IReadOnlyList<ChartReferenceLine> ReferenceLines { get; init; } = [];

    /// <summary>停留時可命中的水平距離（0～1，約一個取樣週期）。</summary>
    public double HoverTolerance { get; init; }

    /// <summary>是否有資料可畫。</summary>
    public bool HasData => EmptyText is null;

    /// <summary>
    /// 水平位置 <paramref name="x"/>（0～1）附近的各線取樣點：每條線取最近的一點，距離超過
    /// <see cref="HoverTolerance"/>（例如停止監控的空檔）就不命中。
    /// </summary>
    public IReadOnlyList<HoverHit> HoverHits(double x)
    {
        var hits = new List<HoverHit>();
        foreach (ChartSeries series in Series)
        {
            HoverPoint? best = null;
            foreach (HoverPoint point in series.Points)
            {
                if (best is null || Math.Abs(point.X - x) < Math.Abs(best.Value.X - x))
                {
                    best = point;
                }
            }

            if (best is HoverPoint found && Math.Abs(found.X - x) <= HoverTolerance)
            {
                hits.Add(new HoverHit(series, found));
            }
        }

        return hits;
    }

    /// <summary>停留提示文字：第一行為時間（本機時區），之後每條線一行；沒有命中時為 null。</summary>
    public string? HoverText(double x)
    {
        IReadOnlyList<HoverHit> hits = HoverHits(x);
        if (hits.Count == 0)
        {
            return null;
        }

        HoverHit nearest = hits.MinBy(h => Math.Abs(h.Point.X - x));
        IEnumerable<string> lines = hits.Select(h => $"{h.Series.Name}　{TrendChartBuilder.ValueText(h.Point.Value, Unit)}");
        return string.Join('\n', [nearest.Point.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture), .. lines]);
    }
}

/// <summary>由趨勢資料計算圖形（純函式，可在任何平台測試；WPF 只負責依正規化座標繪製）。</summary>
public static class TrendChartBuilder
{
    /// <summary>
    /// 相鄰兩筆取樣間隔超過取樣週期的這個倍數時斷開（例如停止監控期間），避免把前後兩點連起來而誤導。
    /// </summary>
    public const double GapFactor = 2.5;

    /// <summary>建立一張圖。</summary>
    /// <param name="title">標題。</param>
    /// <param name="unit">單位（例如 <c>%</c>、<c>Mbps</c>、<c>°C</c>）。</param>
    /// <param name="samples">取樣（舊到新）。</param>
    /// <param name="group">分組。</param>
    /// <param name="sampleInterval">取樣週期（判斷斷開）。</param>
    /// <param name="fixedMax">固定的縱軸最大值（百分比用 100）；null 時依資料自動調整。</param>
    /// <param name="minimumMax">自動調整時縱軸最大值的下限。</param>
    /// <param name="emptyText">沒有任何資料時顯示的文字。</param>
    /// <param name="referenceLines">橫向參考線（數值與標籤，例如告警門檻）；高於縱軸最大值的不畫。</param>
    /// <param name="seriesFilter">只畫名稱符合的線（例如總覽只取 CPU）；null 時全部。</param>
    public static TrendChart Build(
        string title,
        string unit,
        IReadOnlyList<TrendSample> samples,
        TrendGroup group,
        TimeSpan sampleInterval,
        double? fixedMax,
        double minimumMax,
        string emptyText,
        IReadOnlyList<(double Value, string Label)>? referenceLines = null,
        Func<string, bool>? seriesFilter = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        string[] names =
        [
            .. samples.SelectMany(s => s.Values.TryGetValue(group, out var v) ? v.Keys : [])
                .Distinct(StringComparer.Ordinal)
                .Where(n => seriesFilter is null || seriesFilter(n)),
        ];
        double[] values =
        [
            .. samples.SelectMany(s => s.Values.TryGetValue(group, out var v) ? v.Where(p => seriesFilter is null || seriesFilter(p.Key)).Select(p => p.Value) : [])
                .OfType<double>(),
        ];
        if (samples.Count == 0 || names.Length == 0 || values.Length == 0)
        {
            return new TrendChart(title, fixedMax ?? minimumMax, Axis(fixedMax ?? minimumMax, unit), [], emptyText) { Unit = unit };
        }

        double yMax = fixedMax ?? NiceCeiling(Math.Max(values.Max() * 1.1, minimumMax));
        DateTimeOffset end = samples[^1].Time;
        DateTimeOffset start = end - TrendHistory.Window;
        TimeSpan gap = sampleInterval * GapFactor;

        ChartSeries[] series =
        [
            .. names.Order(StringComparer.Ordinal).Select((name, index) =>
            {
                double? latest = Value(samples[^1], group, name);
                string current = latest is double v ? ValueText(v, unit) : UiText.NoValue;
                return new ChartSeries(name, index, Segments(samples, group, name, start, gap, yMax), current)
                {
                    Points = Points(samples, group, name, start),
                };
            }),
        ];
        ChartReferenceLine[] lines =
        [
            .. (referenceLines ?? []).Where(l => l.Value > 0 && l.Value <= yMax).Select(l => new ChartReferenceLine(l.Value, l.Value / yMax, l.Label)),
        ];
        return new TrendChart(title, yMax, Axis(yMax, unit), series, null)
        {
            Unit = unit,
            ReferenceLines = lines,
            HoverTolerance = Math.Max(sampleInterval / TrendHistory.Window, 0.01),
        };
    }

    /// <summary>數值加單位的文字（百分比不留空白）。</summary>
    public static string ValueText(double value, string unit) =>
        $"{value.ToString(Format(value), CultureInfo.InvariantCulture)} {unit}".Replace(" %", "%", StringComparison.Ordinal);

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

    private static List<HoverPoint> Points(IReadOnlyList<TrendSample> samples, TrendGroup group, string name, DateTimeOffset start)
    {
        var points = new List<HoverPoint>();
        foreach (TrendSample sample in samples)
        {
            if (Value(sample, group, name) is double value)
            {
                points.Add(new HoverPoint(Math.Clamp((sample.Time - start) / TrendHistory.Window, 0, 1), sample.Time, value));
            }
        }

        return points;
    }

    private static List<IReadOnlyList<ChartPoint>> Segments(
        IReadOnlyList<TrendSample> samples, TrendGroup group, string name, DateTimeOffset start, TimeSpan gap, double yMax)
    {
        var segments = new List<IReadOnlyList<ChartPoint>>();
        var current = new List<ChartPoint>();
        DateTimeOffset? previous = null;
        foreach (TrendSample sample in samples)
        {
            double? value = Value(sample, group, name);
            bool broken = value is null || (previous is DateTimeOffset p && sample.Time - p > gap);
            if (broken && current.Count > 0)
            {
                segments.Add(current);
                current = [];
            }

            if (value is double v)
            {
                double x = Math.Clamp((sample.Time - start) / TrendHistory.Window, 0, 1);
                current.Add(new ChartPoint(x, Math.Clamp(v / yMax, 0, 1)));
            }

            previous = sample.Time;
        }

        if (current.Count > 0)
        {
            segments.Add(current);
        }

        return segments;
    }

    private static double? Value(TrendSample sample, TrendGroup group, string name) =>
        sample.Values.TryGetValue(group, out var series) && series.TryGetValue(name, out double? value) ? value : null;

    private static string Format(double value) => value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";

    private static string Axis(double max, string unit) =>
        $"{max.ToString("0.##", CultureInfo.InvariantCulture)} {unit}".Replace(" %", "%", StringComparison.Ordinal);
}
