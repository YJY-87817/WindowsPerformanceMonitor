using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Application.Tests;

public class StatusLogTrackerTests
{
    private readonly FakeClock _clock = new();
    private readonly RecordingLog _log = new();
    private readonly StatusLogTracker _tracker;

    public StatusLogTrackerTests() => _tracker = new StatusLogTracker(_log, _clock);

    private void Error(string message = "timeout", ErrorCode code = ErrorCode.Timeout) =>
        _tracker.Report("Gpu", "GPU 0", MonitorStatus.Error, code, message);

    [Fact]
    public void FirstErrorIsWrittenToErrorLogWithCodeAndHandling()
    {
        Error();

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogChannel.Error, entry.Channel);
        Assert.Equal(LogSeverity.Error, entry.Severity);
        Assert.Equal("Gpu", entry.Source);
        Assert.Equal("Gpu GPU 0 發生錯誤 [Timeout]：timeout（處理：下次取樣重試）", entry.Message);
    }

    [Fact]
    public void RepeatedSameErrorIsSuppressedUntilTenMinuteSummary()
    {
        Error();
        _clock.Advance(TimeSpan.FromMinutes(5));
        Error();
        Error();
        Assert.Single(_log.Entries);

        _clock.Advance(TimeSpan.FromMinutes(5));
        Error();

        Assert.Equal(2, _log.Entries.Count);
        Assert.Equal("Gpu GPU 0 仍持續錯誤 [Timeout]，過去 10 分鐘又發生 3 次：timeout", _log.Entries[1].Message);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Error();
        Assert.Equal(2, _log.Entries.Count);
    }

    [Fact]
    public void ChangedCodeOrMessageIsWrittenAgain()
    {
        Error();
        Error("other");
        Error("other", ErrorCode.DataError);

        Assert.Equal(3, _log.Entries.Count);
    }

    [Fact]
    public void UnavailableIsWrittenOnlyOnFirstDiscoveryAndChange()
    {
        _tracker.Report("Gpu", null, MonitorStatus.Unavailable, ErrorCode.Unavailable, "nvidia-smi not found");
        _tracker.Report("Gpu", null, MonitorStatus.Unavailable, ErrorCode.Unavailable, "nvidia-smi not found");
        _clock.Advance(TimeSpan.FromHours(1));
        _tracker.Report("Gpu", null, MonitorStatus.Unavailable, ErrorCode.Unavailable, "nvidia-smi not found");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogSeverity.Warn, entry.Severity);
        Assert.Equal("Gpu 無法使用：nvidia-smi not found", entry.Message);
    }

    [Fact]
    public void RecoveryIsWrittenOnceAfterErrorOrUnavailable()
    {
        Error();
        _tracker.Report("Gpu", "GPU 0", MonitorStatus.OK, null, null);
        _tracker.Report("Gpu", "GPU 0", MonitorStatus.OK, null, null);

        Assert.Equal(2, _log.Entries.Count);
        Assert.Equal(LogSeverity.Info, _log.Entries[1].Severity);
        Assert.Equal("Gpu GPU 0 已恢復（OK）", _log.Entries[1].Message);
    }

    [Fact]
    public void NormalStatusesWithoutPriorProblemWriteNothing()
    {
        _tracker.Report("Cpu", null, MonitorStatus.WarmingUp, null, null);
        _tracker.Report("Cpu", null, MonitorStatus.OK, null, null);
        _tracker.Report("Cpu", null, MonitorStatus.Disabled, null, null);

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void DevicesAreTrackedSeparately()
    {
        _tracker.Report("Disk", "C:", MonitorStatus.Error, ErrorCode.DataError, "size 0");
        _tracker.Report("Disk", "D:", MonitorStatus.Error, ErrorCode.DataError, "size 0");

        Assert.Equal(2, _log.Entries.Count);
    }
}
