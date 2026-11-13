using PerformanceMonitor.Infrastructure.Common;

namespace PerformanceMonitor.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void Elapsed_NeverGoesBackwards()
    {
        var clock = new SystemClock();

        TimeSpan first = clock.Elapsed;
        TimeSpan second = clock.Elapsed;

        Assert.True(first >= TimeSpan.Zero);
        Assert.True(second >= first);
    }

    [Fact]
    public void UtcNow_IsUtcAndCloseToSystemTime()
    {
        DateTimeOffset now = new SystemClock().UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange((DateTimeOffset.UtcNow - now).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DelayAsync_WaitsAndCanBeCancelled()
    {
        var clock = new SystemClock();
        TimeSpan before = clock.Elapsed;

        await clock.DelayAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None);

        Assert.True(clock.Elapsed - before >= TimeSpan.FromMilliseconds(15));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clock.DelayAsync(TimeSpan.FromSeconds(10), cts.Token));
    }

    [Fact]
    public void HostInfo_ProvidesComputerNameAndLocalOffset()
    {
        var host = new HostInfo();

        Assert.False(string.IsNullOrWhiteSpace(host.ComputerName));
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UtcNow), host.GetUtcOffset(DateTimeOffset.UtcNow));
    }
}
