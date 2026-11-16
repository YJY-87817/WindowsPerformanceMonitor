using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Reporting.Statistics;

/// <summary>
/// 尖峰時段（決策 0008）：依趨勢粒度分段，依段平均由高到低檢視；與已選時段相鄰的段併入該時段，
/// 其餘成為新時段，直到有 <c>count</c> 個時段（避免同一次尖峰佔滿名次）。
/// </summary>
public static class PeakFinder
{
    /// <summary>每個指標列出的尖峰時段數。</summary>
    public const int DefaultCount = 5;

    /// <summary>找出尖峰時段；<paramref name="buckets"/> 與時間軸的時段一一對應，沒有資料為 null。</summary>
    public static IReadOnlyList<PeakPeriod> Find(IReadOnlyList<(TrendBucket Bucket, string? Device)?> buckets, ReportTimeline timeline, int count = DefaultCount)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(timeline);
        int[] order =
        [
            .. Enumerable.Range(0, buckets.Count)
                .Where(i => buckets[i] is not null)
                .OrderByDescending(i => buckets[i]!.Value.Bucket.Average)
                .ThenBy(i => i),
        ];

        var periods = new List<(int First, int Last)>();
        foreach (int index in order)
        {
            int adjacent = periods.FindIndex(p => index == p.First - 1 || index == p.Last + 1);
            if (adjacent >= 0)
            {
                (int first, int last) = periods[adjacent];
                periods[adjacent] = (Math.Min(first, index), Math.Max(last, index));

                // 併入後可能與另一個時段相鄰，一併合併。
                int other = periods.FindIndex(p => p != periods[adjacent] && (p.First == periods[adjacent].Last + 1 || p.Last == periods[adjacent].First - 1));
                if (other >= 0)
                {
                    (int f, int l) = (Math.Min(periods[adjacent].First, periods[other].First), Math.Max(periods[adjacent].Last, periods[other].Last));
                    periods[adjacent] = (f, l);
                    periods.RemoveAt(other);
                }

                continue;
            }

            if (periods.Count >= count)
            {
                break;
            }

            periods.Add((index, index));
        }

        return
        [
            .. periods.Select(p => ToPeriod(p.First, p.Last, buckets, timeline))
                .OrderByDescending(p => p.Average)
                .ThenBy(p => p.Start),
        ];
    }

    private static PeakPeriod ToPeriod(int first, int last, IReadOnlyList<(TrendBucket Bucket, string? Device)?> buckets, ReportTimeline timeline)
    {
        double sum = 0;
        int n = 0;
        double max = double.MinValue;
        (TrendBucket Bucket, string? Device)? best = null;
        for (int i = first; i <= last; i++)
        {
            if (buckets[i] is not { } b)
            {
                continue;
            }

            sum += b.Bucket.Average * b.Bucket.Count;
            n += b.Bucket.Count;
            max = Math.Max(max, b.Bucket.Max);
            if (best is null || b.Bucket.Average > best.Value.Bucket.Average)
            {
                best = b;
            }
        }

        DateTimeOffset end = timeline.BucketStart(last) + timeline.Granularity;
        return new PeakPeriod(timeline.BucketStart(first), end < timeline.End ? end : timeline.End, sum / n, max, best?.Device);
    }

    /// <summary>
    /// 把同一角色的多條數列合成一條：每段取平均最高的裝置（例如最忙的邏輯磁碟），並記下是哪個裝置。
    /// </summary>
    public static (TrendBucket Bucket, string? Device)?[] Combine(IEnumerable<SeriesStatistics> series, int bucketCount)
    {
        ArgumentNullException.ThrowIfNull(series);
        var combined = new (TrendBucket Bucket, string? Device)?[bucketCount];
        foreach (SeriesStatistics s in series)
        {
            for (int i = 0; i < bucketCount && i < s.Buckets.Count; i++)
            {
                if (s.Buckets[i] is TrendBucket b && (combined[i] is null || b.Average > combined[i]!.Value.Bucket.Average))
                {
                    combined[i] = (b, s.Key.Device);
                }
            }
        }

        return combined;
    }
}
