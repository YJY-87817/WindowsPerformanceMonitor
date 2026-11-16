using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Reporting.Tests;

public class PeriodAndTimelineTests
{
    [Fact]
    public void PeriodIsInclusiveAndLimitedTo366Days()
    {
        var from = new DateOnly(2026, 1, 1);

        ReportPeriod? oneDay = ReportPeriod.TryCreate(from, from, out _);
        ReportPeriod? year = ReportPeriod.TryCreate(from, from.AddDays(365), out _);
        ReportPeriod? tooLong = ReportPeriod.TryCreate(from, from.AddDays(366), out string? longError);
        ReportPeriod? reversed = ReportPeriod.TryCreate(from.AddDays(1), from, out string? reversedError);

        Assert.Equal(1, oneDay!.Days);
        Assert.Equal("2026-01-01", oneDay.ToString());
        Assert.Equal(366, year!.Days);
        Assert.Equal("2026-01-01 ～ 2027-01-01（366 天）", year.ToString());
        Assert.Null(tooLong);
        Assert.Contains("366", longError, StringComparison.Ordinal);
        Assert.Null(reversed);
        Assert.Contains("晚於", reversedError, StringComparison.Ordinal);
        Assert.Equal([from, from.AddDays(1)], ReportPeriod.TryCreate(from, from.AddDays(1), out _)!.Dates);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 10)]
    [InlineData(7, 10)]
    [InlineData(8, 60)]
    [InlineData(30, 60)]
    [InlineData(31, 1440)]
    [InlineData(366, 1440)]
    public void GranularityFollowsPlanTable(int days, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), ReportTimeline.GranularityFor(days));

    [Fact]
    public void TimelineStartsAtLocalMidnightAndBucketsByGranularity()
    {
        var timeline = new ReportTimeline(TestData.Period(1, 1), TestData.Taipei);

        Assert.Equal(TestData.Local(1, 0), timeline.Start);
        Assert.Equal(TestData.Local(2, 0), timeline.End);
        Assert.Equal(1440, timeline.BucketCount);
        Assert.Equal("每分鐘", timeline.GranularityText);
        Assert.Equal(0, timeline.BucketOf(TestData.Local(1, 0, 0, 59)));
        Assert.Equal(61, timeline.BucketOf(TestData.Local(1, 1, 1, 30)));
        Assert.Equal(1439, timeline.BucketOf(TestData.Local(1, 23, 59, 59)));
        Assert.Null(timeline.BucketOf(TestData.Local(2, 0)));
        Assert.Null(timeline.BucketOf(TestData.Local(1, 0).AddTicks(-1)));
        Assert.Equal(TestData.Local(1, 1, 1), timeline.BucketStart(61));
    }

    [Fact]
    public void GranularityTextForLongerPeriods()
    {
        Assert.Equal("每 10 分鐘", new ReportTimeline(TestData.Period(1, 7), TestData.Taipei).GranularityText);
        Assert.Equal("每小時", new ReportTimeline(TestData.Period(1, 30), TestData.Taipei).GranularityText);
        Assert.Equal(7 * 144, new ReportTimeline(TestData.Period(1, 7), TestData.Taipei).BucketCount);
    }
}
