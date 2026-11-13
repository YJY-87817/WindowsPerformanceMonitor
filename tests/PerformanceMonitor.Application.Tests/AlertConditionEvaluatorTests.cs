using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using static PerformanceMonitor.Core.Models.Alerts.AlertCondition;

namespace PerformanceMonitor.Application.Tests;

public class AlertConditionEvaluatorTests
{
    private static DiskItem Disk(double freePercent, double freeGB, MonitorStatus status = MonitorStatus.OK) =>
        new(status, "C:", null, 100, null, freeGB, null, freePercent);

    private static AlertCondition Check(double freePercent, double freeGB, ThresholdSettings? t = null) =>
        AlertConditionEvaluator.DiskCondition(Disk(freePercent, freeGB), t ?? new ThresholdSettings());

    [Theory]
    [InlineData(10, Breach)]
    [InlineData(5, Breach)]
    [InlineData(11, Hysteresis)]
    [InlineData(12, Recovered)]
    public void PercentOnly(double freePercent, AlertCondition expected)
    {
        Assert.Equal(expected, Check(freePercent, 999));
    }

    [Theory]
    [InlineData(50, 4, Breach)]      // GB 達門檻
    [InlineData(5, 100, Breach)]     // 百分比達門檻
    [InlineData(50, 5.5, Hysteresis)] // GB 未達恢復值
    [InlineData(50, 6, Recovered)]   // 兩者都達恢復值
    public void AnyMode(double freePercent, double freeGB, AlertCondition expected)
    {
        var t = new ThresholdSettings { DiskFreeGB = 5, DiskRecoveryFreeGB = 6 };

        Assert.Equal(expected, Check(freePercent, freeGB, t));
    }

    [Theory]
    [InlineData(5, 4, Breach)]       // 兩者都達門檻
    [InlineData(5, 100, Recovered)]  // GB 達恢復值即恢復
    [InlineData(50, 4, Recovered)]   // 百分比達恢復值即恢復
    [InlineData(11, 5.5, Hysteresis)]
    public void AllMode(double freePercent, double freeGB, AlertCondition expected)
    {
        var t = new ThresholdSettings { DiskFreeGB = 5, DiskRecoveryFreeGB = 6, DiskConditionMode = DiskConditionMode.All };

        Assert.Equal(expected, Check(freePercent, freeGB, t));
    }

    [Fact]
    public void DiskNotOkIsUnknown()
    {
        Assert.Equal(Unknown, AlertConditionEvaluator.DiskCondition(Disk(1, 1, MonitorStatus.Error), new ThresholdSettings()));
    }
}
