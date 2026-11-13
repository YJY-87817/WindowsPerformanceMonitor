using PerformanceMonitor.Application.Scheduling;

namespace PerformanceMonitor.Application.Tests;

public class IntervalScheduleTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void FirstGridPointIsStart()
    {
        var schedule = new IntervalSchedule(S(2), S(5));

        Assert.False(schedule.IsDue(S(4.9)));
        Assert.True(schedule.IsDue(S(5)));
    }

    [Fact]
    public void Advance_OnTime_MovesOneIntervalWithoutSkipping()
    {
        var schedule = new IntervalSchedule(S(2), S(0));

        long skipped = schedule.Advance(S(0.3));

        Assert.Equal(0, skipped);
        Assert.Equal(S(2), schedule.NextDue);
    }

    [Fact]
    public void Advance_AfterOverrun_SkipsMissedPointsWithoutCatchUp()
    {
        var schedule = new IntervalSchedule(S(2), S(0));

        long skipped = schedule.Advance(S(5.1));

        Assert.Equal(2, skipped);
        Assert.Equal(S(6), schedule.NextDue);
    }

    [Fact]
    public void Advance_ExactlyOnNextPoint_MovesPastIt()
    {
        var schedule = new IntervalSchedule(S(2), S(0));

        long skipped = schedule.Advance(S(2));

        Assert.Equal(1, skipped);
        Assert.Equal(S(4), schedule.NextDue);
    }

    [Fact]
    public void Advance_BeforeDue_DoesNothing()
    {
        var schedule = new IntervalSchedule(S(2), S(10));

        Assert.Equal(0, schedule.Advance(S(3)));
        Assert.Equal(S(10), schedule.NextDue);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalSchedule(TimeSpan.Zero, S(0)));
    }
}
