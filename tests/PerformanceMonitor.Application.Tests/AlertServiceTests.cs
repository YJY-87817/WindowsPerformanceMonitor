using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class AlertServiceTests
{
    private static readonly DateTimeOffset StartLocal = new(2026, 11, 13, 14, 0, 0, TimeSpan.FromHours(8));

    private readonly FakeClock _clock = new();
    private readonly RecordingLog _log = new();

    private static DiskItem Disk(string drive, double freePercent, double freeGB, MonitorStatus status = MonitorStatus.OK) =>
        new(status, drive, null, 100, 100 - freeGB, freeGB, 100 - freePercent, freePercent);

    private MonitorSnapshot Snapshot(double? cpu, double? ram = 50, params DiskItem[] disks)
    {
        DateTimeOffset utc = StartLocal.ToUniversalTime() + _clock.Now;
        List<ModuleResult> results =
        [
            new CpuResult(cpu is null ? MonitorStatus.Error : MonitorStatus.OK, cpu),
            new MemoryResult(MonitorStatus.OK, 16, 8, 8, ram),
        ];
        if (disks.Length > 0)
        {
            results.Add(new DiskResult(MonitorStatus.OK, disks));
        }

        return new MonitorSnapshot(SampleId.New(), utc, TimeSpan.FromHours(8), "PC", results, TimeSpan.Zero, false);
    }

    /// <summary>把時間推進到指定秒數後才建立快照，讓快照時間與單調時間一致。</summary>
    private IReadOnlyList<AlertEvent> ObserveAt(AlertService service, double seconds, Func<MonitorSnapshot>? snapshot = null, double cpu = 95)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds) - _clock.Now);
        return service.Observe(snapshot is null ? Snapshot(cpu) : snapshot());
    }

    [Fact]
    public void TargetsAreIndependentAndEventsCarryValueAndThreshold()
    {
        var service = new AlertService(new AppSettings(), _log, _clock);

        ObserveAt(service, 0, () => Snapshot(95, 95, Disk("C:", 5, 20), Disk("D:", 50, 200)));
        IReadOnlyList<AlertEvent> events = ObserveAt(service, 10, () => Snapshot(95, 95, Disk("C:", 5, 20), Disk("D:", 50, 200)));

        Assert.Equal(["CPU", "RAM", "Disk C:"], events.Select(e => e.Target));
        AlertEvent cpu = events[0];
        Assert.Equal(AlertEventType.Warning, cpu.Type);
        Assert.Equal("95.0%", cpu.ValueText);
        Assert.Equal(">= 90% (recover <= 85%)", cpu.Threshold);
        Assert.Equal("5.0% / 20.00 GB free", events[2].ValueText);
        Assert.Equal("free <= 10%", events[2].Threshold);
        Assert.Equal(StartLocal.AddSeconds(10), cpu.Time);
    }

    [Fact]
    public void ActiveAlertsAndStartTimeFromPendingSample()
    {
        var service = new AlertService(new AppSettings(), _log, _clock);
        ObserveAt(service, 0, () => Snapshot(80));
        ObserveAt(service, 2);
        ObserveAt(service, 12);

        ActiveAlert active = Assert.Single(service.Current.Active);
        Assert.Equal("CPU", active.Target);
        Assert.Equal(AlertState.Cooldown, active.State);
        Assert.Equal(StartLocal.AddSeconds(2), active.Since);
    }

    [Fact]
    public void RecentEventsAreNewestFirstAndLimitedToFive()
    {
        var settings = new AppSettings { Alert = new AlertSettings { DurationSeconds = 0, CooldownSeconds = 0, RecoveryDurationSeconds = 0 } };
        var service = new AlertService(settings, _log, _clock);

        for (int i = 0; i < 8; i++)
        {
            ObserveAt(service, i, () => Snapshot(i % 2 == 0 ? 95 : 10));
        }

        AlertView view = service.Current;
        Assert.Equal(AlertService.RecentLimit, view.Recent.Count);
        Assert.Equal(StartLocal.AddSeconds(7), view.Recent[0].Time);
        Assert.Equal(AlertEventType.Recovered, view.Recent[0].Type);
    }

    [Fact]
    public void CpuErrorNeverTriggersAlert()
    {
        var service = new AlertService(new AppSettings(), _log, _clock);

        ObserveAt(service, 0, () => Snapshot(null));
        IReadOnlyList<AlertEvent> events = ObserveAt(service, 100, () => Snapshot(null));

        Assert.Empty(events);
        Assert.Empty(service.Current.Active);
    }

    [Fact]
    public void EventsAreWrittenToApplicationLog()
    {
        var settings = new AppSettings { Alert = new AlertSettings { DurationSeconds = 0, RecoveryDurationSeconds = 0 } };
        var service = new AlertService(settings, _log, _clock);

        ObserveAt(service, 0);
        ObserveAt(service, 70);
        ObserveAt(service, 71, cpu: 10);

        Assert.Equal(
            [
                (LogSeverity.Warn, "告警 CPU：目前 95.0%，門檻 >= 90% (recover <= 85%)"),
                (LogSeverity.Warn, "告警持續（冷卻結束後再次通知） CPU：目前 95.0%，門檻 >= 90% (recover <= 85%)"),
                (LogSeverity.Info, "已恢復 CPU：目前 10.0%，門檻 >= 90% (recover <= 85%)"),
            ],
            _log.Entries.Select(e => (e.Severity, e.Message)));
        Assert.All(_log.Entries, e => Assert.Equal(("Alert", LogChannel.Application), (e.Source, e.Channel)));
    }

    [Fact]
    public void RemovedDiskLosesItsAlertState()
    {
        var service = new AlertService(new AppSettings(), _log, _clock);
        ObserveAt(service, 0, () => Snapshot(50, 50, Disk("E:", 5, 1)));
        ObserveAt(service, 10, () => Snapshot(50, 50, Disk("E:", 5, 1)));
        Assert.Single(service.Current.Active);

        ObserveAt(service, 12, () => Snapshot(50, 50));

        Assert.Empty(service.Current.Active);
    }

    [Fact]
    public void DisabledAlertsAreNotEvaluated()
    {
        var service = new AlertService(new AppSettings { Alert = new AlertSettings { Enabled = false, DurationSeconds = 0 } }, _log, _clock);

        Assert.Empty(ObserveAt(service, 0));
        Assert.False(service.Current.Enabled);
        Assert.Empty(_log.Entries);
    }
}
