using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Tests;

public class StatusAggregatorTests
{
    [Theory]
    [InlineData(new MonitorStatus[0], MonitorStatus.Unavailable)]
    [InlineData(new[] { MonitorStatus.OK, MonitorStatus.Error }, MonitorStatus.Error)]
    [InlineData(new[] { MonitorStatus.Unavailable, MonitorStatus.Error }, MonitorStatus.Error)]
    [InlineData(new[] { MonitorStatus.Unavailable, MonitorStatus.Unavailable }, MonitorStatus.Unavailable)]
    [InlineData(new[] { MonitorStatus.WarmingUp, MonitorStatus.Unavailable }, MonitorStatus.WarmingUp)]
    [InlineData(new[] { MonitorStatus.WarmingUp, MonitorStatus.WarmingUp }, MonitorStatus.WarmingUp)]
    [InlineData(new[] { MonitorStatus.WarmingUp, MonitorStatus.OK }, MonitorStatus.OK)]
    [InlineData(new[] { MonitorStatus.OK, MonitorStatus.Unavailable }, MonitorStatus.OK)]
    [InlineData(new[] { MonitorStatus.OK }, MonitorStatus.OK)]
    public void Aggregate_FollowsSpecificationRules(MonitorStatus[] devices, MonitorStatus expected)
    {
        Assert.Equal(expected, StatusAggregator.Aggregate(devices));
    }
}
