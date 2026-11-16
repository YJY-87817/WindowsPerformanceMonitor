using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class SeriesAccumulatorTests
{
    private static readonly SeriesKey Key = new("CPU", "使用率", "%");

    [Fact]
    public void OnlyOkRowsWithValuesCount()
    {
        var series = new SeriesAccumulator(Key, new ReportTimeline(TestData.Period(1, 1), TestData.Taipei));
        series.Add(TestData.Local(1, 1), 10, MonitorStatus.OK);
        series.Add(TestData.Local(1, 2), 30, MonitorStatus.OK);
        series.Add(TestData.Local(1, 3), 99, MonitorStatus.Error); // 有數值但狀態不是 OK
        series.Add(TestData.Local(1, 4), null, MonitorStatus.Unavailable);
        series.Add(TestData.Local(1, 5), null, MonitorStatus.OK); // OK 但缺值
        series.Add(TestData.Local(1, 6), 0, MonitorStatus.OK); // 0 是有效值

        SeriesStatistics s = series.ToStatistics();

        Assert.Equal(3, s.ValidCount);
        Assert.Equal(6, s.Statuses.Total);
        Assert.Equal(4, s.Statuses[MonitorStatus.OK]);
        Assert.Equal(1, s.Statuses[MonitorStatus.Error]);
        Assert.Equal(40.0 / 3, s.Average!.Value, 9);
        Assert.Equal(30, s.Max);
        Assert.Equal(30, s.P95);
        Assert.Equal(TestData.Local(1, 1), s.FirstValid);
        Assert.Equal(TestData.Local(1, 6), s.LastValid);
    }

    [Fact]
    public void NoValidValuesGivesNulls()
    {
        var series = new SeriesAccumulator(Key, new ReportTimeline(TestData.Period(1, 1), TestData.Taipei));
        series.Add(TestData.Local(1, 1), null, MonitorStatus.Unavailable);

        SeriesStatistics s = series.ToStatistics();

        Assert.Equal(0, s.ValidCount);
        Assert.Null(s.Average);
        Assert.Null(s.Max);
        Assert.Null(s.P95);
        Assert.Null(s.FirstValid);
        Assert.All(s.Buckets, b => Assert.Null(b));
    }

    [Fact]
    public void BucketsHoldAverageAndMaxPerGranularity()
    {
        var series = new SeriesAccumulator(Key, new ReportTimeline(TestData.Period(1, 1), TestData.Taipei));
        series.Add(TestData.Local(1, 10, 0, 0), 10, MonitorStatus.OK);
        series.Add(TestData.Local(1, 10, 0, 30), 20, MonitorStatus.OK);
        series.Add(TestData.Local(1, 10, 1, 0), 50, MonitorStatus.OK);
        series.Add(TestData.Local(3, 0), 70, MonitorStatus.OK); // 期間外：列入統計但不在趨勢時段

        SeriesStatistics s = series.ToStatistics();

        Assert.Equal(new TrendBucket(15, 20, 2), s.Buckets[600]);
        Assert.Equal(new TrendBucket(50, 50, 1), s.Buckets[601]);
        Assert.Equal(2, s.Buckets.Count(b => b is not null));
        Assert.Equal(70, s.Max);
    }
}
