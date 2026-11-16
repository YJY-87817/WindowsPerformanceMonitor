using System.Globalization;

namespace PerformanceMonitor.Reporting.Model;

/// <summary>
/// 報告時間軸與趨勢彙總粒度（規劃 v2.1 §8，2026-11-16 確認照表）：1 天以內每分鐘、7 天以內每 10 分鐘、
/// 30 天以內每小時、超過 30 天每日。時段由期間起點（起日本機 00:00）起算。
/// </summary>
public sealed class ReportTimeline
{
    /// <summary>建立。</summary>
    public ReportTimeline(ReportPeriod period, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(zone);
        Zone = zone;
        Start = LocalMidnight(period.From, zone);
        End = LocalMidnight(period.To.AddDays(1), zone);
        Granularity = GranularityFor(period.Days);
        BucketCount = (int)Math.Ceiling((End - Start) / Granularity);
    }

    /// <summary>本機時區。</summary>
    public TimeZoneInfo Zone { get; }

    /// <summary>期間起點（起日本機 00:00）。</summary>
    public DateTimeOffset Start { get; }

    /// <summary>期間終點（訖日隔天本機 00:00，不含）。</summary>
    public DateTimeOffset End { get; }

    /// <summary>趨勢彙總粒度。</summary>
    public TimeSpan Granularity { get; }

    /// <summary>時段數。</summary>
    public int BucketCount { get; }

    /// <summary>粒度的顯示文字。</summary>
    public string GranularityText => Granularity.TotalDays >= 1 ? "每日"
        : Granularity.TotalHours >= 1 ? "每小時"
        : string.Create(CultureInfo.InvariantCulture, $"每 {Granularity.TotalMinutes:0} 分鐘").Replace("每 1 分鐘", "每分鐘", StringComparison.Ordinal);

    /// <summary>依期間天數決定粒度。</summary>
    public static TimeSpan GranularityFor(int days) => days switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        <= 7 => TimeSpan.FromMinutes(10),
        <= 30 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromDays(1),
    };

    /// <summary>時間所在的時段；不在期間內時為 null。</summary>
    public int? BucketOf(DateTimeOffset time)
    {
        if (time < Start || time >= End)
        {
            return null;
        }

        return Math.Min((int)((time - Start) / Granularity), BucketCount - 1);
    }

    /// <summary>轉為本機時間。</summary>
    public DateTimeOffset ToLocal(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone);

    /// <summary>時段的起點。</summary>
    public DateTimeOffset BucketStart(int index) => Start + (Granularity * index);

    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1); // 夏令時間開始於午夜的地區
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
