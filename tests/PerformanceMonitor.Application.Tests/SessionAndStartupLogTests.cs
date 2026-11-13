using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class SessionAndStartupLogTests
{
    private readonly FakeClock _clock = new();
    private readonly RecordingLog _log = new();
    private readonly SnapshotStore _store = new();
    private readonly StorageStatus _storage = new();

    private sealed class ThrowingRecorder : ISnapshotRecorder
    {
        public Task RecordAsync(MonitorSnapshot snapshot, bool includeProcesses, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("disk exploded");
    }

    private MonitoringSession Session(ISnapshotRecorder recorder)
    {
        var settings = new AppSettings();
        var monitor = new FakeMonitor("Cpu", _clock) { Throws = new InvalidOperationException("sensor") };
        var engine = new MonitoringEngine(new SampleRunner([monitor], _clock, new FakeHostInfo()), _clock, settings, _store, recorder);
        return new MonitoringSession(engine, _store, new MonitoringLogObserver(_log, new StatusLogTracker(_log, _clock), _clock), new AlertService(settings, _log, _clock), _log, _storage);
    }

    [Fact]
    public async Task NormalStopWritesStartAndStopWithRowCountsAndObservesSnapshots()
    {
        _storage.AddWritten(7);
        _storage.AddLost(2, "locked");
        using var cts = new CancellationTokenSource();
        _store.SnapshotPublished += (_, _) => cts.Cancel();

        await Session(new RecordingRecorder()).RunAsync(cts.Token);

        string[] app = [.. _log.Messages(LogChannel.Application)];
        Assert.Equal("監控開始", app[0]);
        Assert.Equal("監控正常停止；本次 CSV 寫入 7 列、遺失 2 列。", app[^1]);
        Assert.Contains(_log.Messages(LogChannel.Error), m => m.StartsWith("Cpu 發生錯誤 [InternalError]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnexpectedExceptionWritesAbnormalEndAndRethrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Session(new ThrowingRecorder()).RunAsync(CancellationToken.None));

        Assert.Contains("監控異常結束：InvalidOperationException: disk exploded", _log.Messages(LogChannel.Application));
        Assert.Contains("[InternalError] InvalidOperationException: disk exploded（處理：停止監控）", _log.Messages(LogChannel.Error));
    }

    [Fact]
    public void StartupLogSummarizesSettingsAndDirectories()
    {
        var settings = new AppSettings { Gpu = new ModuleSettings { Enabled = false }, Thresholds = new ThresholdSettings { DiskFreeGB = 5, DiskRecoveryFreeGB = 6 } };
        var result = new SettingsLoadResult(settings, @"C:\x\settings.json", [], ["未知欄位 'foo'，已忽略（請檢查拼字）。"], ["timeouts"]);

        StartupLog.Write(_log, new StartupInfo("1.2.3", result, @"C:\data", @"C:\logs"));

        string[] lines = [.. _log.Entries.Select(e => e.Message)];
        Assert.Equal("Windows Performance Monitor 1.2.3 啟動", lines[0]);
        Assert.Contains(@"設定來源：C:\x\settings.json", lines);
        Assert.Contains("未知欄位 'foo'，已忽略（請檢查拼字）。", lines);
        Assert.Contains("使用預設值的欄位：timeouts", lines);
        Assert.Contains("週期：取樣 2 秒、畫面 2 秒、CSV 10 秒、程序紀錄 10 秒", lines);
        Assert.Contains(lines, l => l.Contains("GPU 停用", StringComparison.Ordinal) && l.Contains("CPU 啟用", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("可用 ≤ 5 GB（恢復 ≥ 6 GB），條件 Any", StringComparison.Ordinal));
        Assert.Contains(@"CSV 目錄：C:\data", lines);
        Assert.Contains(@"Log 目錄：C:\logs", lines);
        Assert.Equal(LogSeverity.Warn, _log.Entries.Single(e => e.Message.StartsWith("未知欄位", StringComparison.Ordinal)).Severity);
    }

    [Fact]
    public void StorageStatusTracksRowsAndClearsErrorAfterSuccess()
    {
        _storage.AddLost(3, "locked");
        Assert.Equal("locked", _storage.LastError);

        _storage.AddWritten(4);

        Assert.Equal(3, _storage.LostRows);
        Assert.Equal(4, _storage.WrittenRows);
        Assert.Null(_storage.LastError);
    }
}
