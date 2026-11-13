using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Application.Tests;

public class MonitoringLogObserverTests
{
    private readonly FakeClock _clock = new();
    private readonly RecordingLog _log = new();
    private readonly MonitoringLogObserver _observer;

    public MonitoringLogObserverTests() => _observer = new MonitoringLogObserver(_log, new StatusLogTracker(_log, _clock), _clock);

    private static MonitorSnapshot Snapshot(bool overrun = false, params ModuleResult[] results) =>
        new(SampleId.New(), DateTimeOffset.UnixEpoch, TimeSpan.Zero, "PC", results, TimeSpan.FromMilliseconds(2500), overrun);

    [Fact]
    public void DeviceErrorsAreLoggedPerDeviceNotAsModuleAggregate()
    {
        var disk = new DiskResult(MonitorStatus.Error, [
            new DiskItem(MonitorStatus.OK, "C:", null, 100, 50, 50, 50, 50),
            new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null) { ErrorCode = ErrorCode.DataError, ErrorMessage = "size 0" },
        ]);

        _observer.Observe(Snapshot(results: disk));

        Assert.Equal(["Disk D: 發生錯誤 [DataError]：size 0（處理：下次取樣重試）"], _log.Messages(LogChannel.Error));
    }

    [Fact]
    public void ModuleLevelFailureOfDeviceModuleIsLogged()
    {
        var network = new NetworkResult(MonitorStatus.Unavailable, [], null, null) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "No network adapter matched" };

        _observer.Observe(Snapshot(results: network));

        Assert.Equal(["Network 無法使用：No network adapter matched"], _log.Messages(LogChannel.Error));
    }

    [Fact]
    public void SimpleModuleErrorIsLogged()
    {
        _observer.Observe(Snapshot(results: ModuleResult.Failed("Cpu", ErrorCode.InternalError, "boom")));

        Assert.Single(_log.Messages(LogChannel.Error));
    }

    [Fact]
    public void LoopOverrunIsLoggedFirstThenSummarizedEveryTenMinutes()
    {
        _observer.Observe(Snapshot(overrun: true));
        _observer.Observe(Snapshot(overrun: true));
        _clock.Advance(TimeSpan.FromMinutes(10));
        _observer.Observe(Snapshot(overrun: true));
        _observer.Observe(Snapshot(overrun: false));

        string[] messages = [.. _log.Messages(LogChannel.Application)];
        Assert.Equal(2, messages.Length);
        Assert.Equal("取樣耗時 2500 ms，超過取樣週期（LoopOverrun）；錯過的排程點不補跑。", messages[0]);
        Assert.Equal("過去 10 分鐘又發生 2 次 LoopOverrun（最近一次耗時 2500 ms）。", messages[1]);
    }
}
