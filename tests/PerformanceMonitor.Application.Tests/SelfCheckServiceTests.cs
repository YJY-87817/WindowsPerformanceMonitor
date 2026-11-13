using PerformanceMonitor.Application.Services;

namespace PerformanceMonitor.Application.Tests;

public class SelfCheckServiceTests
{
    [Fact]
    public void Run_PassesWhenClockMovesForward()
    {
        var clock = new FakeClock(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        SelfCheckResult result = new SelfCheckService(clock).Run();

        Assert.True(result.Passed);
        Assert.Equal(clock.StartUtc, result.CheckedAtUtc);
    }

    [Fact]
    public void Run_FailsWhenClockGoesBackwards()
    {
        var clock = new FakeClock(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        SelfCheckResult result = new SelfCheckService(clock).Run();

        Assert.False(result.Passed);
    }
}
