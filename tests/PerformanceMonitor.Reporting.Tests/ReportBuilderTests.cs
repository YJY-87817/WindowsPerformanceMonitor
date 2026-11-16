using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class ReportBuilderTests
{
    private readonly ReportBuilder _builder = new(TestData.Period(1, 1), TestData.Taipei, csvIntervalSeconds: 10);

    private void Samples(DateTimeOffset start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _builder.AddSample(start.AddSeconds(i * 10), "PC1");
        }
    }

    [Fact]
    public void StartStopSessionGivesExpectedSamplesAndMissingRatio()
    {
        _builder.MarkLogsFound();
        _builder.AddSessionEvent(TestData.Local(1, 9), SessionEventKind.Started);
        _builder.AddSessionEvent(TestData.Local(1, 9, 10), SessionEventKind.Stopped);
        Samples(TestData.Local(1, 9), 51); // 預期 ⌊600 ÷ 10⌋ + 1 = 61，缺 10

        DataQuality q = _builder.Build(TestData.Local(1, 12)).Quality;

        MonitoringSpan span = Assert.Single(q.Sessions);
        Assert.Equal(SessionEnd.Stopped, span.EndKind);
        Assert.Equal(61, span.ExpectedSamples);
        Assert.False(span.StartedBeforePeriod);
        Assert.Equal(61, q.ExpectedSamples);
        Assert.Equal(51, q.ActualSamples);
        Assert.Equal(10.0 / 61, q.MissingRatio!.Value, 9);
        Assert.Equal(TestData.Local(1, 9), q.Earliest);
        Assert.Equal(TestData.Local(1, 9, 8, 20), q.Latest);
    }

    [Fact]
    public void SessionWithoutStopEndsAtItsLastSample()
    {
        _builder.MarkLogsFound();
        _builder.AddSessionEvent(TestData.Local(1, 9), SessionEventKind.Started);
        _builder.AddSessionEvent(TestData.Local(1, 10), SessionEventKind.Started); // 第一個沒有停止紀錄（程式被強制結束）
        _builder.AddSessionEvent(TestData.Local(1, 10, 1), SessionEventKind.Faulted);
        Samples(TestData.Local(1, 9), 31); // 09:00:00～09:05:00
        Samples(TestData.Local(1, 10), 7);

        DataQuality q = _builder.Build(TestData.Local(1, 12)).Quality;

        Assert.Equal(2, q.Sessions.Count);
        Assert.Equal(SessionEnd.NotRecorded, q.Sessions[0].EndKind);
        Assert.Equal(TestData.Local(1, 9, 5), q.Sessions[0].End);
        Assert.Equal(31, q.Sessions[0].ExpectedSamples);
        Assert.Equal(SessionEnd.Faulted, q.Sessions[1].EndKind);
        Assert.Equal(7, q.Sessions[1].ExpectedSamples);
        Assert.Equal(0, q.MissingRatio);
    }

    [Fact]
    public void StopWithoutStartBeganBeforePeriod()
    {
        _builder.MarkLogsFound();
        _builder.AddSessionEvent(TestData.Local(1, 0, 5), SessionEventKind.Stopped);
        _builder.AddSessionEvent(TestData.Local(1, 8), SessionEventKind.OnceStarted);
        _builder.AddSessionEvent(TestData.Local(1, 8, 0, 2), SessionEventKind.OnceCompleted);

        DataQuality q = _builder.Build(TestData.Local(1, 12)).Quality;

        Assert.Equal(TestData.Local(1, 0), q.Sessions[0].Start);
        Assert.True(q.Sessions[0].StartedBeforePeriod);
        Assert.Equal(31, q.Sessions[0].ExpectedSamples);
        Assert.Equal(SessionEnd.Once, q.Sessions[1].EndKind);
        Assert.Equal(1, q.Sessions[1].ExpectedSamples);
        Assert.Equal(1.0, q.MissingRatio);
    }

    [Fact]
    public void WithoutLogsExpectedIsUnknown()
    {
        Samples(TestData.Local(1, 9), 3);

        DataQuality q = _builder.Build(TestData.Local(1, 12)).Quality;

        Assert.False(q.LogsFound);
        Assert.Null(q.ExpectedSamples);
        Assert.Null(q.MissingRatio);
        Assert.Equal(3, q.ActualSamples);
    }

    [Fact]
    public void SessionEventsMustComeBeforeSamples()
    {
        _builder.AddSample(TestData.Local(1, 9), "PC1");

        Assert.Throws<InvalidOperationException>(() => _builder.AddSessionEvent(TestData.Local(1, 9), SessionEventKind.Started));
    }

    [Fact]
    public void SeriesAreOrderedByGroupThenFirstAppearanceAndModulesCollected()
    {
        _builder.AddMetric(new SeriesKey(ReportGroups.ThermalZone, "TZ0", "°C"), TestData.Local(1, 9), 40, MonitorStatus.OK);
        _builder.AddMetric(new SeriesKey(ReportGroups.Disk, "D: 已用", "%"), TestData.Local(1, 9), 10, MonitorStatus.OK);
        _builder.AddMetric(new SeriesKey(ReportGroups.Disk, "C: 已用", "%"), TestData.Local(1, 9), 80, MonitorStatus.OK);
        _builder.AddMetric(new SeriesKey(ReportGroups.Cpu, "使用率", "%"), TestData.Local(1, 9), 5, MonitorStatus.OK);
        _builder.AddModuleStatus(ReportGroups.Gpu, MonitorStatus.Unavailable);
        _builder.AddModuleStatus(ReportGroups.Cpu, MonitorStatus.OK);
        _builder.AddModuleStatus(ReportGroups.Cpu, MonitorStatus.WarmingUp);
        _builder.AddSample(TestData.Local(1, 9), "PC2");
        _builder.AddSample(TestData.Local(1, 9, 0, 10), "pc2");

        ReportModel model = _builder.Build(TestData.Local(1, 12));

        Assert.Equal(["使用率", "D: 已用", "C: 已用", "TZ0"], model.Series.Select(s => s.Key.Name));
        Assert.Equal([ReportGroups.Cpu, ReportGroups.Gpu], model.Quality.Modules.Select(m => m.Module));
        Assert.Equal(2, model.Quality.Modules[0].Statuses.Total);
        Assert.Equal(1.0, model.Quality.Modules[1].Statuses.UnavailableRatio);
        Assert.Equal(["PC2"], model.ComputerNames);
    }
}
