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
}
