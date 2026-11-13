using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Tests;

/// <summary>可設定行為的監控模組（手寫假物件）。</summary>
internal sealed class FakeMonitor(string name, FakeClock clock) : IMonitor
{
    public string Name { get; } = name;

    public bool IsEnabled { get; init; } = true;

    /// <summary>每次取樣要花的時間。</summary>
    public TimeSpan Cost { get; init; }

    public Exception? Throws { get; init; }

    public int WarmUpCount { get; private set; }

    public List<TimeSpan> SampledAt { get; } = [];

    public Task WarmUpAsync(CancellationToken cancellationToken)
    {
        WarmUpCount++;
        return Task.CompletedTask;
    }

    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        SampledAt.Add(clock.Now);
        clock.Advance(Cost);
        if (Throws is not null)
        {
            throw Throws;
        }

        return Task.FromResult(new ModuleResult(Name, MonitorStatus.OK));
    }
}

internal sealed class FakeHostInfo : IHostInfo
{
    public string ComputerName => "TEST-PC";

    public TimeSpan GetUtcOffset(DateTimeOffset utcTime) => TimeSpan.FromHours(8);
}

internal sealed class RecordingRecorder : ISnapshotRecorder
{
    public List<(MonitorSnapshot Snapshot, bool IncludeProcesses)> Records { get; } = [];

    public Task RecordAsync(MonitorSnapshot snapshot, bool includeProcesses, CancellationToken cancellationToken)
    {
        Records.Add((snapshot, includeProcesses));
        return Task.CompletedTask;
    }
}
