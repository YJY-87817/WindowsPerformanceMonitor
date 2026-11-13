using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Settings;
using static PerformanceMonitor.Core.Models.Alerts.AlertCondition;

namespace PerformanceMonitor.Application.Tests;

/// <summary>對應舊版 AlertService.Tests.ps1 的狀態機案例（TC-018、TC-019 等）。</summary>
public class AlertStateMachineTests
{
    private static readonly AlertSettings Default = new(); // 持續 10、冷卻 60、恢復 10 秒

    private readonly AlertTargetState _target = new();

    private AlertEventType? At(double seconds, AlertCondition condition, AlertSettings? settings = null) =>
        AlertStateMachine.Update(_target, condition, TimeSpan.FromSeconds(seconds), settings ?? Default);

    private void EnterCooldownAt(double seconds)
    {
        Assert.Null(At(seconds, Breach));
        Assert.Equal(AlertEventType.Warning, At(seconds + 10, Breach));
    }

    [Fact]
    public void WarningIsSentOnceAfterDurationThenCooldown()
    {
        Assert.Null(At(0, Breach));
        Assert.Equal(AlertState.Pending, _target.State);
        Assert.Null(At(9.9, Breach));

        Assert.Equal(AlertEventType.Warning, At(10, Breach));
        Assert.Equal(AlertState.Cooldown, _target.State);
        Assert.Null(At(12, Breach));
    }

    [Fact]
    public void PendingReturnsToNormalWhenBelowThresholdBeforeDuration()
    {
        At(0, Breach);

        Assert.Null(At(5, Hysteresis));
        Assert.Equal(AlertState.Normal, _target.State);
        Assert.Null(At(10, Breach));
        Assert.Equal(AlertState.Pending, _target.State);
    }

    [Fact]
    public void RepeatOnlyAfterCooldownWhileStillBreaching()
    {
        EnterCooldownAt(0);

        Assert.Null(At(69, Breach));
        Assert.Equal(AlertEventType.Repeat, At(70, Breach));
        Assert.Null(At(100, Breach));
        Assert.Equal(AlertEventType.Repeat, At(130, Breach));
    }

    [Fact]
    public void NoRepeatWhenDisabledButAlertStaysActive()
    {
        var settings = new AlertSettings { RepeatNotificationAfterCooldown = false };
        At(0, Breach, settings);
        At(10, Breach, settings);

        Assert.Null(At(1000, Breach, settings));
        Assert.Equal(AlertState.Cooldown, _target.State);
    }

    [Fact]
    public void HysteresisKeepsAlertWithoutRecoveryOrNotification()
    {
        EnterCooldownAt(0);

        Assert.Null(At(20, Hysteresis));
        Assert.Null(At(200, Hysteresis));
        Assert.Equal(AlertState.Cooldown, _target.State);
    }

    [Fact]
    public void RecoveredAfterRecoveryDurationThenNormal()
    {
        EnterCooldownAt(0);

        Assert.Null(At(20, Recovered));
        Assert.Equal(AlertState.Recovering, _target.State);
        Assert.Null(At(29, Recovered));
        Assert.Equal(AlertEventType.Recovered, At(30, Recovered));
        Assert.Equal(AlertState.Normal, _target.State);
    }

    [Theory]
    [InlineData(Hysteresis)]
    [InlineData(Breach)]
    [InlineData(Unknown)]
    public void LeavingRecoveryReturnsToCooldownAndRestartsRecoveryTimer(AlertCondition interruption)
    {
        EnterCooldownAt(0);
        At(20, Recovered);

        Assert.Null(At(25, interruption));
        Assert.Equal(AlertState.Cooldown, _target.State);

        Assert.Null(At(26, Recovered));
        Assert.Null(At(35, Recovered));
        Assert.Equal(AlertEventType.Recovered, At(36, Recovered));
    }

    [Fact]
    public void CooldownTimerContinuesAfterReturningFromRecovering()
    {
        EnterCooldownAt(0); // 通知於 10 秒
        At(30, Recovered);
        At(35, Breach); // 回到 COOLDOWN，冷卻從 10 秒起算

        Assert.Equal(AlertEventType.Repeat, At(70, Breach));
    }

    [Fact]
    public void UnavailableDataResetsPendingAndKeepsActiveAlert()
    {
        At(0, Breach);
        Assert.Null(At(5, Unknown));
        Assert.Equal(AlertState.Normal, _target.State);

        EnterCooldownAt(10);
        Assert.Null(At(200, Unknown));
        Assert.Equal(AlertState.Cooldown, _target.State);
    }

    [Fact]
    public void ZeroDurationsAlertAndRecoverImmediately()
    {
        var settings = new AlertSettings { DurationSeconds = 0, CooldownSeconds = 0, RecoveryDurationSeconds = 0 };

        Assert.Equal(AlertEventType.Warning, At(0, Breach, settings));
        Assert.Equal(AlertEventType.Repeat, At(0.1, Breach, settings));
        Assert.Equal(AlertEventType.Recovered, At(1, Recovered, settings));
        Assert.Equal(AlertState.Normal, _target.State);
    }
}
