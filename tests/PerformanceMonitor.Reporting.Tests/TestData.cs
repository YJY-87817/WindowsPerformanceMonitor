using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Reporting.Tests;

internal static class TestData
{
    /// <summary>UTC+8、無夏令時間（台灣）。</summary>
    public static TimeZoneInfo Taipei { get; } = TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    public static ReportPeriod Period(int fromDay, int toDay) =>
        ReportPeriod.TryCreate(new DateOnly(2026, 11, fromDay), new DateOnly(2026, 11, toDay), out _)!;

    /// <summary>2026-11-<paramref name="day"/> 本機（UTC+8）時間。</summary>
    public static DateTimeOffset Local(int day, int hour, int minute = 0, int second = 0) =>
        new(2026, 11, day, hour, minute, second, TimeSpan.FromHours(8));
}
