using System.Globalization;

namespace PerformanceMonitor.Reporting.Model;

/// <summary>
/// 報告期間：本機日期，含頭含尾，對應每日分檔的 CSV 與 Log（2026-11-16 確認）。最長 <see cref="MaxDays"/> 天。
/// </summary>
public sealed record ReportPeriod
{
    /// <summary>一次報告最多涵蓋的天數（2026-11-16 確認）。</summary>
    public const int MaxDays = 366;

    private ReportPeriod(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    /// <summary>起日（本機日期）。</summary>
    public DateOnly From { get; }

    /// <summary>訖日（本機日期，含）。</summary>
    public DateOnly To { get; }

    /// <summary>天數（含頭含尾）。</summary>
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>期間內每一天。</summary>
    public IEnumerable<DateOnly> Dates => Enumerable.Range(0, Days).Select(From.AddDays);

    /// <summary>建立；起日晚於訖日或超過 <see cref="MaxDays"/> 天時回傳 null 並說明原因。</summary>
    public static ReportPeriod? TryCreate(DateOnly from, DateOnly to, out string? error)
    {
        if (from > to)
        {
            error = string.Create(CultureInfo.InvariantCulture, $"起日 {from:yyyy-MM-dd} 晚於訖日 {to:yyyy-MM-dd}");
            return null;
        }

        int days = to.DayNumber - from.DayNumber + 1;
        if (days > MaxDays)
        {
            error = string.Create(CultureInfo.InvariantCulture, $"報告期間 {days} 天，超過上限 {MaxDays} 天");
            return null;
        }

        error = null;
        return new ReportPeriod(from, to);
    }

    /// <summary>顯示文字，例如 <c>2026-11-01 ～ 2026-11-07（7 天）</c>。</summary>
    public override string ToString() =>
        Days == 1
            ? From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{From:yyyy-MM-dd} ～ {To:yyyy-MM-dd}（{Days} 天）");
}
