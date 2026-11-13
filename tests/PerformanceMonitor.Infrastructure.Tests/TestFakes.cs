using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Infrastructure.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 13, 15, 59, 59, 500, TimeSpan.Zero);

    public TimeSpan Elapsed { get; set; }

    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        Elapsed += delay;
        return Task.CompletedTask;
    }
}

internal sealed class FixedHostInfo(TimeSpan offset) : IHostInfo
{
    public string ComputerName => "TEST-PC";

    public TimeSpan GetUtcOffset(DateTimeOffset utcTime) => offset;
}

internal sealed class RecordingLog : IAppLog
{
    public List<(LogChannel Channel, LogSeverity Severity, string Source, string Message)> Entries { get; } = [];

    public int FailureCount => 0;

    public void Write(LogChannel channel, LogSeverity severity, string source, string message) =>
        Entries.Add((channel, severity, source, message));
}

/// <summary>測試用暫存目錄，結束時刪除。</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wpm-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
