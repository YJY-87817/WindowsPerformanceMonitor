using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class InsightsTests
{
    private static readonly ReportTimeline Day = new(TestData.Period(1, 1), TestData.Taipei);

    private static (TrendBucket Bucket, string? Device)?[] Buckets(params (int Index, double Average)[] values)
    {
        var buckets = new (TrendBucket Bucket, string? Device)?[Day.BucketCount];
        foreach ((int index, double average) in values)
        {
            buckets[index] = (new TrendBucket(average, average + 1, 6), $"D{index}");
        }

        return buckets;
    }

    [Fact]
    public void PeaksMergeAdjacentBucketsAndKeepFivePeriods()
    {
        IReadOnlyList<PeakPeriod> peaks = PeakFinder.Find(
            Buckets((10, 90), (11, 95), (12, 80), (100, 70), (200, 60), (300, 50), (400, 40), (500, 30), (501, 85)),
            Day);

        Assert.Equal(5, peaks.Count);
        PeakPeriod first = peaks[0];
        Assert.Equal(TestData.Local(1, 0, 10), first.Start); // 第 10～12 分鐘合併
        Assert.Equal(TestData.Local(1, 0, 13), first.End);
        Assert.Equal(265.0 / 3, first.Average, 9);
        Assert.Equal(96, first.Max);
        Assert.Equal("D11", first.Device);
        Assert.Equal(85, peaks[1].Average); // 第 500 分鐘（30）排在第 5 個時段之後，不再併入
        Assert.Equal(TestData.Local(1, 8, 21), peaks[1].Start);
        Assert.Equal([70.0, 60, 50], peaks.Skip(2).Select(p => p.Average));
        Assert.DoesNotContain(peaks, p => p.Average == 40);
    }

    [Fact]
    public void PeaksJoinTwoPeriodsBridgedByOneBucket()
    {
        IReadOnlyList<PeakPeriod> peaks = PeakFinder.Find(Buckets((10, 90), (12, 80), (11, 50)), Day);

        PeakPeriod only = Assert.Single(peaks);
        Assert.Equal(TestData.Local(1, 0, 10), only.Start);
        Assert.Equal(TestData.Local(1, 0, 13), only.End);
    }

    [Fact]
    public void NoBucketsNoPeaks() => Assert.Empty(PeakFinder.Find(Buckets(), Day));

    [Fact]
    public void CombinePicksBusiestDevicePerBucket()
    {
        var builder = new ReportBuilder(TestData.Period(1, 1), TestData.Taipei, 10);
        builder.AddMetric(new SeriesKey(ReportGroups.DiskIo, "C: 忙碌", "%", SeriesRole.LogicalDiskBusy, "C:"), TestData.Local(1, 1), 10, MonitorStatus.OK);
        builder.AddMetric(new SeriesKey(ReportGroups.DiskIo, "D: 忙碌", "%", SeriesRole.LogicalDiskBusy, "D:"), TestData.Local(1, 1), 40, MonitorStatus.OK);
        builder.AddMetric(new SeriesKey(ReportGroups.DiskIo, "C: 忙碌", "%", SeriesRole.LogicalDiskBusy, "C:"), TestData.Local(1, 2), 30, MonitorStatus.OK);

        ReportModel model = builder.Build(TestData.Local(1, 12));

        PeakList disk = Assert.Single(model.Peaks);
        Assert.True(disk.ByDevice);
        Assert.Equal(["D:", "C:"], disk.Periods.Select(p => p.Device));
    }

    [Fact]
    public void ProcessesAreGroupedByNameAndSummedWithinRecord()
    {
        var processes = new ProcessAccumulator();
        processes.Add("s1", "chrome", 100);
        processes.Add("s1", "Chrome", 50); // 名稱不分大小寫
        processes.Add("s1", "Code", 400);
        processes.Add("s2", "chrome", 300);
        for (int i = 0; i < 12; i++)
        {
            processes.Add("s3", $"p{i}", i);
        }

        ProcessReport report = processes.ToReport();

        Assert.Equal(3, report.Records);
        Assert.Equal(ProcessReport.MaxCount, report.Top.Count);
        Assert.Equal("Code", report.Top[0].Name);
        ProcessSummary chrome = report.Top[1];
        Assert.Equal(2, chrome.Appearances);
        Assert.Equal(2.0 / 3, chrome.AppearanceRatio, 9);
        Assert.Equal(225, chrome.AverageMB);
        Assert.Equal(300, chrome.MaxMB);
    }

    [Fact]
    public void AlertDurationsFromWarningToRecovered()
    {
        var builder = new ReportBuilder(TestData.Period(1, 1), TestData.Taipei, 10);
        builder.MarkLogsFound();
        builder.AddSessionEvent(TestData.Local(1, 9), SessionEventKind.Started);
        builder.AddSessionEvent(TestData.Local(1, 11), SessionEventKind.Stopped);
        builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 9, 10), AlertLogKind.Warning, "CPU", "95%", "90%"));
        builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 9, 11), AlertLogKind.Repeat, "CPU", "96%", "90%"));
        builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 9, 15), AlertLogKind.Recovered, "CPU", "40%", "85%"));
        builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 10), AlertLogKind.Warning, "CPU", "99%", "90%"));
        builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 10, 30), AlertLogKind.Warning, "RAM", "91%", "90%"));

        AlertReport alerts = builder.Build(TestData.Local(1, 12)).Alerts;

        Assert.Equal(5, alerts.TotalEvents);
        Assert.Equal(TestData.Local(1, 10, 30), alerts.Recent[0].Time); // 新到舊
        AlertSummary cpu = alerts.Summaries[0];
        Assert.Equal(2, cpu.Warnings);
        Assert.Equal(1, cpu.Repeats);
        Assert.Equal(TimeSpan.FromMinutes(5 + 60), cpu.Duration); // 5 分 ＋ 10:00 到監控停止 11:00
        Assert.True(cpu.Unrecovered);
        Assert.Equal("99%", cpu.LastValueText);
        Assert.Equal(TimeSpan.FromMinutes(30), alerts.Summaries[1].Duration);
    }

    [Fact]
    public void RecentAlertsAreLimited()
    {
        var builder = new ReportBuilder(TestData.Period(1, 1), TestData.Taipei, 10);
        for (int i = 0; i < 250; i++)
        {
            builder.AddAlertEvent(new AlertLogEvent(TestData.Local(1, 9).AddSeconds(i), i % 2 == 0 ? AlertLogKind.Warning : AlertLogKind.Recovered, "CPU", "x", "y"));
        }

        AlertReport alerts = builder.Build(TestData.Local(1, 12)).Alerts;

        Assert.Equal(250, alerts.TotalEvents);
        Assert.Equal(AlertReport.MaxRecent, alerts.Recent.Count);
        Assert.Equal(125, alerts.Summaries[0].Warnings);
        Assert.False(alerts.Summaries[0].Unrecovered);
    }

    [Fact]
    public void ExpectedSamplesCountFinalRecordAtStop()
    {
        var builder = new ReportBuilder(TestData.Period(1, 1), TestData.Taipei, 10);
        builder.MarkLogsFound();
        builder.AddSessionEvent(TestData.Local(1, 9), SessionEventKind.Started);
        builder.AddSessionEvent(TestData.Local(1, 9, 0, 25), SessionEventKind.Stopped); // 0、10、20 秒各一筆，停止時補寫一筆

        Assert.Equal(4, Assert.Single(builder.Build(TestData.Local(1, 12)).Quality.Sessions).ExpectedSamples);
    }

    [Theory]
    [InlineData(40, "40 秒")]
    [InlineData(65, "1 分 5 秒")]
    [InlineData(3900, "1 小時 5 分")]
    public void FormatsDurations(int seconds, string expected) =>
        Assert.Equal(expected, HtmlReportRenderer.Duration(TimeSpan.FromSeconds(seconds)));
}
