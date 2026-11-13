using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class MonitoringEngineTests
{
    private static readonly TimeSpan WarmUp = MonitoringEngine.WarmUpDelay;

    private readonly FakeClock _clock = new();
    private readonly RecordingRecorder _recorder = new();
    private readonly SnapshotStore _store = new();

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>執行引擎，直到發布 <paramref name="samples"/> 筆快照後停止。</summary>
    private async Task<FakeMonitor> RunAsync(int samples, AppSettings? settings = null, TimeSpan? cost = null)
    {
        var monitor = new FakeMonitor("Cpu", _clock) { Cost = cost ?? TimeSpan.Zero };
        var engine = new MonitoringEngine(
            new SampleRunner([monitor], _clock, new FakeHostInfo()), _clock, settings ?? new AppSettings(), _store, _recorder);

        using var cts = new CancellationTokenSource();
        int published = 0;
        _store.SnapshotPublished += (_, _) =>
        {
            if (++published == samples)
            {
                cts.Cancel();
            }
        };

        await engine.RunAsync(cts.Token);
        return monitor;
    }

    [Fact]
    public async Task WarmsUpEnabledModulesAndWaitsOneSecondBeforeFirstSample()
    {
        FakeMonitor monitor = await RunAsync(samples: 1);

        Assert.Equal(1, monitor.WarmUpCount);
        Assert.Equal(WarmUp, _clock.Delays[0]);
        Assert.Equal(WarmUp, monitor.SampledAt[0]);
    }

    [Fact]
    public async Task SamplesOnTheGridEveryInterval()
    {
        FakeMonitor monitor = await RunAsync(samples: 4);

        Assert.Equal([WarmUp, WarmUp + S(2), WarmUp + S(4), WarmUp + S(6)], monitor.SampledAt);
    }

    [Fact]
    public async Task RecordsFirstSampleImmediatelyThenEveryLogInterval()
    {
        // 取樣 2 秒、CSV 10 秒：第 1、6、11 次取樣寫入（時間 0、10、20 秒）。
        await RunAsync(samples: 11);

        Assert.Equal(3, _recorder.Records.Count);
        Assert.Equal(
            [S(0), S(10), S(20)],
            _recorder.Records.Select(r => r.Snapshot.TimestampUtc - _clock.StartUtc - WarmUp));
    }

    [Fact]
    public async Task ProcessesAreRecordedOnlyWhenTheirIntervalIsDueAtARecordPoint()
    {
        var settings = new AppSettings { Process = new ProcessSettings { LogIntervalSeconds = 20 } };

        await RunAsync(samples: 21, settings);

        Assert.Equal([true, false, true, false, true], _recorder.Records.Select(r => r.IncludeProcesses));
    }

    [Fact]
    public async Task ProcessIntervalShorterThanLogIntervalRecordsAtEveryRecordPoint()
    {
        var settings = new AppSettings { Process = new ProcessSettings { LogIntervalSeconds = 3 } };

        await RunAsync(samples: 11, settings);

        Assert.All(_recorder.Records, r => Assert.True(r.IncludeProcesses));
    }

    [Fact]
    public async Task ProcessLoggingDisabledNeverIncludesProcesses()
    {
        var settings = new AppSettings { Process = new ProcessSettings { LoggingEnabled = false } };

        await RunAsync(samples: 7, settings);

        Assert.NotEmpty(_recorder.Records);
        Assert.All(_recorder.Records, r => Assert.False(r.IncludeProcesses));
    }

    [Fact]
    public async Task LoggingDisabledRecordsNothingEvenOnStop()
    {
        var settings = new AppSettings { Logging = new LoggingSettings { Enabled = false } };

        await RunAsync(samples: 3, settings);

        Assert.Empty(_recorder.Records);
        Assert.NotNull(_store.Latest);
    }

    [Fact]
    public async Task StopRecordsTheLastCompletedButUnrecordedSnapshot()
    {
        // 取樣 3 次（0、2、4 秒）：第 1 次照排程寫入，停止時補寫第 3 次。
        await RunAsync(samples: 3);

        Assert.Equal(2, _recorder.Records.Count);
        Assert.Same(_store.Latest, _recorder.Records[^1].Snapshot);
    }

    [Fact]
    public async Task StopDoesNotRecordTheSameSnapshotTwice()
    {
        await RunAsync(samples: 1);

        Assert.Single(_recorder.Records);
    }

    [Fact]
    public async Task OverrunSkipsMissedGridPointsWithoutCatchUp()
    {
        // 每次取樣花 5 秒、週期 2 秒：0 → 6 → 12（略過 2、4、8、10）。
        FakeMonitor monitor = await RunAsync(samples: 3, cost: S(5));

        Assert.Equal([WarmUp, WarmUp + S(6), WarmUp + S(12)], monitor.SampledAt);
        Assert.True(_store.Latest!.LoopOverrun);
    }

    [Fact]
    public async Task RunOnceWarmsUpSamplesPublishesAndRecordsWithProcesses()
    {
        var monitor = new FakeMonitor("Cpu", _clock);
        var engine = new MonitoringEngine(new SampleRunner([monitor], _clock, new FakeHostInfo()), _clock, new AppSettings(), _store, _recorder);

        MonitorSnapshot snapshot = await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, monitor.WarmUpCount);
        Assert.Equal([WarmUp], monitor.SampledAt);
        Assert.Same(snapshot, _store.Latest);
        var record = Assert.Single(_recorder.Records);
        Assert.Same(snapshot, record.Snapshot);
        Assert.True(record.IncludeProcesses);
    }

    [Fact]
    public async Task RunOnceWithLoggingDisabledDoesNotRecord()
    {
        var settings = new AppSettings { Logging = new LoggingSettings { Enabled = false } };
        var engine = new MonitoringEngine(new SampleRunner([new FakeMonitor("Cpu", _clock)], _clock, new FakeHostInfo()), _clock, settings, _store, _recorder);

        await engine.RunOnceAsync(CancellationToken.None);

        Assert.Empty(_recorder.Records);
    }
}
