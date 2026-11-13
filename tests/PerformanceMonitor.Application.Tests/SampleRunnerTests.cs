using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Tests;

public class SampleRunnerTests
{
    private readonly FakeClock _clock = new();

    private SampleRunner Runner(params IMonitor[] monitors) => new(monitors, _clock, new FakeHostInfo());

    [Fact]
    public async Task Run_BuildsSnapshotWithModuleResultsInOrder()
    {
        SampleRunner runner = Runner(
            new FakeMonitor("Cpu", _clock) { Cost = TimeSpan.FromMilliseconds(100) },
            new FakeMonitor("Memory", _clock) { Cost = TimeSpan.FromMilliseconds(50) });

        MonitorSnapshot snapshot = await runner.RunAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(["Cpu", "Memory"], snapshot.Results.Select(r => r.Module));
        Assert.Equal(TimeSpan.FromMilliseconds(100), snapshot.Results[0].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(150), snapshot.SampleDuration);
        Assert.Equal(_clock.StartUtc, snapshot.TimestampUtc);
        Assert.Equal(TimeSpan.FromHours(8), snapshot.LocalOffset);
        Assert.Equal("TEST-PC", snapshot.ComputerName);
        Assert.False(snapshot.LoopOverrun);
    }

    [Fact]
    public async Task Run_DisabledModuleIsNotSampled()
    {
        var gpu = new FakeMonitor("Gpu", _clock) { IsEnabled = false };

        MonitorSnapshot snapshot = await Runner(gpu).RunAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(MonitorStatus.Disabled, snapshot.Results[0].Status);
        Assert.Empty(gpu.SampledAt);
    }

    [Fact]
    public async Task Run_FailingModuleBecomesErrorWithoutStoppingOthers()
    {
        SampleRunner runner = Runner(
            new FakeMonitor("Gpu", _clock) { Throws = new InvalidOperationException("boom\nat line 1") },
            new FakeMonitor("Memory", _clock));

        MonitorSnapshot snapshot = await runner.RunAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        ModuleResult gpu = snapshot.Results[0];
        Assert.Equal(MonitorStatus.Error, gpu.Status);
        Assert.Equal(ErrorCode.InternalError, gpu.ErrorCode);
        Assert.Equal("InvalidOperationException: boom", gpu.ErrorMessage);
        Assert.Equal(MonitorStatus.OK, snapshot.Results[1].Status);
    }

    [Fact]
    public async Task Run_MarksOverrunWhenSlowerThanInterval()
    {
        SampleRunner runner = Runner(new FakeMonitor("Cpu", _clock) { Cost = TimeSpan.FromSeconds(3) });

        MonitorSnapshot snapshot = await runner.RunAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.True(snapshot.LoopOverrun);
    }

    [Fact]
    public async Task Run_CancellationPropagatesInsteadOfBecomingError()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        SampleRunner runner = Runner(new FakeMonitor("Cpu", _clock) { Throws = new OperationCanceledException(cts.Token) });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(TimeSpan.FromSeconds(2), cts.Token));
    }
}
