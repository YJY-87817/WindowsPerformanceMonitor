using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Reporting.Statistics;

/// <summary>指標在趨勢圖與尖峰時段中的角色。</summary>
public enum SeriesRole
{
    /// <summary>只出現在統計摘要。</summary>
    None,

    /// <summary>CPU 使用率。</summary>
    CpuUsage,

    /// <summary>記憶體使用率。</summary>
    MemoryUsage,

    /// <summary>邏輯磁碟忙碌率。</summary>
    LogicalDiskBusy,

    /// <summary>網路合計下載。</summary>
    NetworkDownload,

    /// <summary>網路合計上傳。</summary>
    NetworkUpload,

    /// <summary>GPU 使用率。</summary>
    GpuUsage,

    /// <summary>溫度（GPU 或 ACPI 熱區）。</summary>
    Temperature,
}

/// <summary>一個指標（一條數列）的識別。</summary>
/// <param name="Group">分組，例如 <c>CPU</c>、<c>磁碟 I/O</c>。</param>
/// <param name="Name">名稱，例如 <c>使用率</c>、<c>C: 忙碌</c>。</param>
/// <param name="Unit">單位，例如 <c>%</c>、<c>MB/s</c>。</param>
/// <param name="Role">趨勢圖與尖峰時段的角色。</param>
/// <param name="Device">裝置名稱（圖例與尖峰時段標示哪一個裝置），例如 <c>C:</c>、<c>GPU 0 RTX 4060</c>。</param>
public sealed record SeriesKey(string Group, string Name, string Unit, SeriesRole Role = SeriesRole.None, string? Device = null);

/// <summary>趨勢圖的一個時段：該時段有效值的平均與最大。</summary>
/// <param name="Average">平均。</param>
/// <param name="Max">最大。</param>
/// <param name="Count">有效值筆數。</param>
public readonly record struct TrendBucket(double Average, double Max, int Count);

/// <summary>一個指標在報告期間的統計。</summary>
/// <param name="Key">識別。</param>
/// <param name="Statuses">各狀態筆數（含無效資料）。</param>
/// <param name="ValidCount">有效值筆數（狀態 OK 且有數值）。</param>
/// <param name="Average">平均；沒有有效值時為 null。</param>
/// <param name="Max">最大。</param>
/// <param name="P95">P95（Nearest Rank，由 CSV 原始紀錄計算）。</param>
/// <param name="FirstValid">第一筆有效值時間。</param>
/// <param name="LastValid">最後一筆有效值時間。</param>
/// <param name="Buckets">趨勢時段（沒有有效值的時段為 null）。</param>
public sealed record SeriesStatistics(
    SeriesKey Key,
    StatusCounts Statuses,
    int ValidCount,
    double? Average,
    double? Max,
    double? P95,
    DateTimeOffset? FirstValid,
    DateTimeOffset? LastValid,
    IReadOnlyList<TrendBucket?> Buckets);

/// <summary>
/// 逐筆累加一個指標（串流處理，不保留原始列）：平均與最大用累加；P95 需要保留有效值，以單精度保存
/// （CSV 數值最多 3 位小數，單精度足以顯示），一年每 10 秒一筆約 12 MB。
/// 只有狀態 <c>OK</c> 且有數值的紀錄列入統計；其他狀態只計數（規劃 v2.1 §8：排除無效與不可用資料）。
/// </summary>
public sealed class SeriesAccumulator(SeriesKey key, ReportTimeline timeline)
{
    private readonly List<float> _values = [];
    private double _sum;
    private double _max = double.MinValue;
    private DateTimeOffset? _first;
    private DateTimeOffset? _last;
    private double[]? _bucketSum;
    private double[]? _bucketMax;
    private int[]? _bucketCount;

    /// <summary>識別。</summary>
    public SeriesKey Key { get; } = key;

    /// <summary>各狀態筆數。</summary>
    public StatusCounts Statuses { get; } = new();

    /// <summary>加一筆紀錄。</summary>
    public void Add(DateTimeOffset time, double? value, MonitorStatus status)
    {
        Statuses.Add(status);
        if (status != MonitorStatus.OK || value is not double v || !double.IsFinite(v))
        {
            return;
        }

        _values.Add((float)v);
        _sum += v;
        _max = Math.Max(_max, v);
        _first = _first is DateTimeOffset f && f <= time ? f : time;
        _last = _last is DateTimeOffset l && l >= time ? l : time;

        if (timeline.BucketOf(time) is int bucket)
        {
            _bucketSum ??= new double[timeline.BucketCount];
            _bucketMax ??= Enumerable.Repeat(double.MinValue, timeline.BucketCount).ToArray();
            _bucketCount ??= new int[timeline.BucketCount];
            _bucketSum[bucket] += v;
            _bucketMax[bucket] = Math.Max(_bucketMax[bucket], v);
            _bucketCount[bucket]++;
        }
    }

    /// <summary>計算統計。</summary>
    public SeriesStatistics ToStatistics()
    {
        int count = _values.Count;
        TrendBucket?[] buckets = new TrendBucket?[timeline.BucketCount];
        if (_bucketCount is not null)
        {
            for (int i = 0; i < buckets.Length; i++)
            {
                if (_bucketCount[i] > 0)
                {
                    buckets[i] = new TrendBucket(_bucketSum![i] / _bucketCount[i], _bucketMax![i], _bucketCount[i]);
                }
            }
        }

        return new SeriesStatistics(
            Key,
            Statuses,
            count,
            count == 0 ? null : _sum / count,
            count == 0 ? null : _max,
            Percentile.NearestRank(_values, 95),
            _first,
            _last,
            buckets);
    }
}
