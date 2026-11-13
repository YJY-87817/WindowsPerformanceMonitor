using PerformanceMonitor.Application.Services;

namespace PerformanceMonitor.Application.Tests;

public class SelfCheckServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_PassesWhenClockMovesForward()
    {
        var clock = new FakeClock(Now, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        SelfCheckResult result = new SelfCheckService(clock).Run();

        Assert.True(result.Passed);
        Assert.Equal(Now, result.CheckedAtUtc);
    }

    [Fact]
    public void Run_FailsWhenClockGoesBackwards()
    {
        var clock = new FakeClock(Now, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        SelfCheckResult result = new SelfCheckService(clock).Run();

        Assert.False(result.Passed);
    }
}
