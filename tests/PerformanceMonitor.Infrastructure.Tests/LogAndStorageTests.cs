using System.Text;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Logging;
using PerformanceMonitor.Infrastructure.Storage;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class LogAndStorageTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private DailyLogWriter Writer(LoggingSettings? logging = null) => new(
        new AppSettings { Logging = logging ?? new LoggingSettings() },
        new AppDataPaths(_temp.Path),
        new FakeClock(),
        new FixedHostInfo(TimeSpan.FromHours(8)));

    [Fact]
    public void WritesOneLinePerEntryWithLocalTimeLevelAndSource()
    {
        DailyLogWriter writer = Writer();

        writer.Info("Startup", "第一行\r\n第二行");
        writer.Write(LogChannel.Error, LogSeverity.Error, "Gpu", "timeout");

        // FakeClock：UTC 2026-11-13 15:59:59.500 → 本機 2026-11-13 23:59:59.500+08:00
        string appPath = Path.Combine(_temp.Path, "Logs", "Application_20261113.log");
        byte[] bytes = File.ReadAllBytes(appPath);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Equal("2026-11-13T23:59:59.500+08:00 [INFO] [Startup] 第一行 第二行\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Equal(
            ["2026-11-13T23:59:59.500+08:00 [ERROR] [Gpu] timeout"],
            File.ReadAllLines(Path.Combine(_temp.Path, "Logs", "Error_20261113.log")));
        Assert.Equal(0, writer.FailureCount);
    }

    [Fact]
    public void WriteFailureIsCountedNotThrown()
    {
        string blocker = Path.Combine(_temp.Path, "not-a-directory");
        File.WriteAllText(blocker, "x");
        DailyLogWriter writer = Writer(new LoggingSettings { LogDirectory = blocker });

        writer.Warn("Monitor", "x");

        Assert.Equal(1, writer.FailureCount);
    }

    [Fact]
    public void StorageInitializer_AcceptsWritableDirectoriesAndLeavesNoProbeFile()
    {
        IReadOnlyList<string> errors = StorageInitializer.Check(new AppSettings(), new AppDataPaths(_temp.Path));

        Assert.Empty(errors);
        Assert.Empty(Directory.GetFiles(Path.Combine(_temp.Path, "Data")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_temp.Path, "Logs")));
    }

    [Fact]
    public void StorageInitializer_ReportsUnwritableDirectories()
    {
        string blocker = Path.Combine(_temp.Path, "file");
        File.WriteAllText(blocker, "x");
        var settings = new AppSettings { Logging = new LoggingSettings { DataDirectory = blocker, LogDirectory = Path.Combine(blocker, "sub") } };

        IReadOnlyList<string> errors = StorageInitializer.Check(settings, new AppDataPaths(_temp.Path));

        Assert.Equal(2, errors.Count);
        Assert.StartsWith("Log 目錄無法寫入：", errors[0], StringComparison.Ordinal);
        Assert.StartsWith("CSV 目錄無法寫入：", errors[1], StringComparison.Ordinal);
    }

    [Fact]
    public void StorageInitializer_SkipsDataDirectoryWhenCsvDisabled()
    {
        string blocker = Path.Combine(_temp.Path, "file");
        File.WriteAllText(blocker, "x");
        var settings = new AppSettings { Logging = new LoggingSettings { Enabled = false, DataDirectory = blocker } };

        Assert.Empty(StorageInitializer.Check(settings, new AppDataPaths(_temp.Path)));
    }
}
