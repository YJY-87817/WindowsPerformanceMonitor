using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application.Tests;

/// <summary>記錄所有 Log 呼叫的假物件。</summary>
internal sealed class RecordingLog : IAppLog
{
    public List<(LogChannel Channel, LogSeverity Severity, string Source, string Message)> Entries { get; } = [];

    public int FailureCount => 0;

    public IEnumerable<string> Messages(LogChannel channel) => Entries.Where(e => e.Channel == channel).Select(e => e.Message);

    public void Write(LogChannel channel, LogSeverity severity, string source, string message) =>
        Entries.Add((channel, severity, source, message));
}
