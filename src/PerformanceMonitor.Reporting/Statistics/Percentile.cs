namespace PerformanceMonitor.Reporting.Statistics;

/// <summary>百分位數（規劃 v2.1 §8：Nearest Rank 法）。</summary>
public static class Percentile
{
    /// <summary>
    /// Nearest Rank：由小到大排序後取第 ⌈p ÷ 100 × n⌉ 個（1 起算），結果一定是實際出現過的數值。
    /// 沒有資料時為 null。<paramref name="values"/> 不會被修改。
    /// </summary>
    public static double? NearestRank(IReadOnlyCollection<float> values, double percent)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(percent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        if (values.Count == 0)
        {
            return null;
        }

        float[] sorted = [.. values];
        Array.Sort(sorted);
        int rank = (int)Math.Ceiling((percent / 100 * sorted.Length) - 1e-9);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }
}
