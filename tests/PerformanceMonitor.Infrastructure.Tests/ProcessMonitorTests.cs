using System.ComponentModel;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Processes;

namespace PerformanceMonitor.Infrastructure.Tests;

public class ProcessMonitorTests
{
    private const long MB = 1_048_576;
    private static readonly DateTimeOffset Started = new(2026, 11, 13, 1, 0, 0, TimeSpan.Zero);

    private static ProcessSnapshot Snapshot(int skipped, params (int Pid, string Name, long MB)[] processes) =>
        new([.. processes.Select(p => new ProcessReading(p.Pid, p.Name, p.MB * MB))], skipped);

    [Fact]
    public void SelectsTopNByWorkingSetWithRanks()
    {
        ProcessResult result = ProcessMonitor.Select(
            Snapshot(0, (1, "small", 10), (2, "chrome", 1200), (3, "code", 850), (4, "idle", 0)),
            topCount: 2,
            _ => Started);

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal([(1, "chrome", 2, 1200.0), (2, "code", 3, 850.0)], result.Processes.Select(p => (p.Rank, p.ProcessName, p.Pid, p.WorkingSetMB)));
        Assert.All(result.Processes, p => Assert.Equal(Started, p.StartTimeUtc));
    }

    [Fact]
    public void TiesAreOrderedByPid()
    {
        ProcessResult result = ProcessMonitor.Select(Snapshot(0, (9, "b", 100), (3, "a", 100)), 5, _ => Started);

        Assert.Equal([3, 9], result.Processes.Select(p => p.Pid));
    }

    [Fact]
    public void SkippedProcessesAndMissingStartTimesAreCountedButStillOk()
    {
        ProcessResult result = ProcessMonitor.Select(
            Snapshot(4, (1, "System", 500), (2, "app", 100)),
            5,
            pid => pid == 1 ? null : Started);

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(4, result.SkippedProcessCount);
        Assert.Equal(1, result.MissingFieldCount);
        Assert.Null(result.Processes[0].StartTimeUtc);
    }

    [Fact]
    public void StartTimeIsOnlyReadForTopN()
    {
        var asked = new List<int>();

        ProcessMonitor.Select(Snapshot(0, (1, "a", 3), (2, "b", 2), (3, "c", 1)), 2, pid =>
        {
            asked.Add(pid);
            return Started;
        });

        Assert.Equal([1, 2], asked);
    }

    [Fact]
    public void NoReadableProcessIsError()
    {
        ProcessResult result = ProcessMonitor.Select(Snapshot(7), 5, _ => Started);

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(7, result.SkippedProcessCount);
    }

    [Fact]
    public async Task EnumerationFailureIsTransientError()
    {
        var monitor = new ProcessMonitor(new AppSettings(), new ThrowingSource());

        ProcessResult result = Assert.IsType<ProcessResult>(await monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
    }

    [Fact]
    public async Task RealProcessesIncludeThisTestProcess()
    {
        var monitor = new ProcessMonitor(new AppSettings { Process = new ProcessSettings { TopCount = 20 } }, new SystemProcessSource());

        ProcessResult result = Assert.IsType<ProcessResult>(await monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.InRange(result.Processes.Count, 1, 20);
        Assert.All(result.Processes, p => Assert.True(p.WorkingSetMB >= 0));
        Assert.Equal(Enumerable.Range(1, result.Processes.Count), result.Processes.Select(p => p.Rank));
        Assert.NotNull(new SystemProcessSource().GetStartTimeUtc(Environment.ProcessId));
        Assert.Null(new SystemProcessSource().GetStartTimeUtc(int.MaxValue));
    }

    [Fact]
    public void NameEnabledAndSource()
    {
        var monitor = new ProcessMonitor(new AppSettings { Process = new ProcessSettings { Enabled = false, TopCount = 7 } }, new ThrowingSource());

        Assert.Equal(ModuleNames.Process, monitor.Name);
        Assert.False(monitor.IsEnabled);
        Assert.Contains("Top 7", monitor.SourceDescription, StringComparison.Ordinal);
    }

    private sealed class ThrowingSource : IProcessSource
    {
        public ProcessSnapshot List() => throw new Win32Exception(5);

        public DateTimeOffset? GetStartTimeUtc(int pid) => null;
    }
}
