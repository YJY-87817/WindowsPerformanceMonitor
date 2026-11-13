namespace PerformanceMonitor.Application.Scheduling;

/// <summary>
/// 固定間隔的時間格點（以單調時間表示）。執行後跳到下一個尚未經過的格點；
/// 錯過的格點直接略過，不補跑（舊規格 v1.5 §4.3）。
/// </summary>
public sealed class IntervalSchedule
{
    /// <summary>建立排程；第一個格點就是 <paramref name="start"/>。</summary>
    public IntervalSchedule(TimeSpan interval, TimeSpan start)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        Interval = interval;
        NextDue = start;
    }

    /// <summary>間隔。</summary>
    public TimeSpan Interval { get; }

    /// <summary>下一個格點。</summary>
    public TimeSpan NextDue { get; private set; }

    /// <summary>目前時間是否已到下一個格點。</summary>
    public bool IsDue(TimeSpan now) => now >= NextDue;

    /// <summary>
    /// 將下一個格點移到 <paramref name="now"/> 之後最近的格點。
    /// 回傳被略過的格點數（不含剛執行的那一個）；尚未到期時不移動並回傳 0。
    /// </summary>
    public long Advance(TimeSpan now)
    {
        if (now < NextDue)
        {
            return 0;
        }

        long steps = ((now - NextDue).Ticks / Interval.Ticks) + 1;
        NextDue += TimeSpan.FromTicks(Interval.Ticks * steps);
        return steps - 1;
    }
}
