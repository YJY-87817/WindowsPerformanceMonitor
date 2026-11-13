using System.Text;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Infrastructure.Storage.Csv;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class CsvSnapshotRecorderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeClock _clock = new();
    private readonly RecordingLog _log = new();
    private readonly StorageStatus _storage = new();

    public void Dispose() => _temp.Dispose();

    private string DataDir => Path.Combine(_temp.Path, "Data");

    private CsvSnapshotRecorder Recorder(LoggingSettings? logging = null) => new(
        new AppSettings { Logging = logging ?? new LoggingSettings() },
        new AppDataPaths(_temp.Path),
        new CsvFileWriter(_clock),
        _storage,
        new StatusLogTracker(_log, _clock));

    private static byte[] Bom => Encoding.UTF8.GetPreamble();

    [Fact]
    public async Task WritesAllTablesWithBomHeaderAndCrLfUsingLocalDateInFileName()
    {
        // UTC 16:30 + 8 小時 = 本機 11/14
        await Recorder().RecordAsync(CsvTablesTests.FullSnapshot(), includeProcesses: true, CancellationToken.None);

        string[] files = [.. Directory.GetFiles(DataDir).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        Assert.Equal(
            ["DiskIoMetrics_v1_20261114.csv", "DiskMetrics_v1_20261114.csv", "GpuMetrics_v1_20261114.csv", "NetworkMetrics_v1_20261114.csv",
             "ProcessMetrics_v1_20261114.csv", "SystemMetrics_v1_20261114.csv", "TemperatureMetrics_v1_20261114.csv"],
            files);

        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(DataDir, "SystemMetrics_v1_20261114.csv"));
        Assert.Equal(Bom, bytes[..3]);
        string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.StartsWith(CsvTables.SystemMetrics.Header + "\r\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        Assert.Equal(9, _storage.WrittenRows);
    }

    [Fact]
    public async Task SecondRecordAppendsWithoutRepeatingHeader()
    {
        CsvSnapshotRecorder recorder = Recorder();
        MonitorSnapshot snapshot = CsvTablesTests.FullSnapshot();

        await recorder.RecordAsync(snapshot, false, CancellationToken.None);
        await recorder.RecordAsync(snapshot, false, CancellationToken.None);

        string[] lines = await File.ReadAllLinesAsync(Path.Combine(DataDir, "SystemMetrics_v1_20261114.csv"));
        Assert.Equal(3, lines.Length);
        Assert.Single(lines, l => l.Contains("SampleId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessesAreWrittenOnlyWhenRequested()
    {
        await Recorder().RecordAsync(CsvTablesTests.FullSnapshot(), includeProcesses: false, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(DataDir, "ProcessMetrics_v1_20261114.csv")));
    }

    [Fact]
    public async Task LoggingDisabledWritesNothing()
    {
        await Recorder(new LoggingSettings { Enabled = false }).RecordAsync(CsvTablesTests.FullSnapshot(), true, CancellationToken.None);

        Assert.False(Directory.Exists(DataDir));
    }

    [Fact]
    public async Task LockedFileIsRetriedThenCountedAsLostAndLoggedOnce()
    {
        CsvSnapshotRecorder recorder = Recorder(new LoggingSettings { LockedFileRetryCount = 2 });
        MonitorSnapshot snapshot = CsvTablesTests.Snapshot();
        Directory.CreateDirectory(DataDir);
        string path = Path.Combine(DataDir, "SystemMetrics_v1_20261114.csv");

        using (new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await recorder.RecordAsync(snapshot, false, CancellationToken.None);
            await recorder.RecordAsync(snapshot, false, CancellationToken.None);
        }

        Assert.Equal([CsvFileWriter.RetryDelay, CsvFileWriter.RetryDelay, CsvFileWriter.RetryDelay, CsvFileWriter.RetryDelay], _clock.Delays);
        Assert.Equal(2, _storage.LostRows);
        Assert.StartsWith("SystemMetrics：", _storage.LastError, StringComparison.Ordinal);
        var error = Assert.Single(_log.Entries);
        Assert.Equal(LogChannel.Error, error.Channel);
        Assert.StartsWith("Csv SystemMetrics 發生錯誤 [IOError]", error.Message, StringComparison.Ordinal);

        await recorder.RecordAsync(snapshot, false, CancellationToken.None);

        Assert.Equal(1, _storage.WrittenRows);
        Assert.Null(_storage.LastError);
        Assert.Equal("Csv SystemMetrics 已恢復（OK）", _log.Entries[^1].Message);
    }
}
