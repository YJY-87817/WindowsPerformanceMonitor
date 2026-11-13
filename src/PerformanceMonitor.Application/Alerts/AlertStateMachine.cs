using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Alerts;

/// <summary>單一告警對象的狀態（時間皆為單調時間）。</summary>
public sealed class AlertTargetState
{
    /// <summary>目前狀態。</summary>
    public AlertState State { get; internal set; } = AlertState.Normal;

    /// <summary>進入 Pending 的時間。</summary>
    public TimeSpan? PendingSince { get; internal set; }

    /// <summary>最近一次通知時間。</summary>
    public TimeSpan? LastNotified { get; internal set; }

    /// <summary>進入 Recovering 的時間。</summary>
    public TimeSpan? RecoveringSince { get; internal set; }
}

/// <summary>
/// 告警狀態轉換（舊規格 v1.5 §6.2、§6.4 實作決策）：
/// <code>
/// NORMAL ─達門檻→ PENDING ─持續達 duration→ WARNING（通知）→ COOLDOWN
/// PENDING ─未達門檻或資料不可用→ NORMAL
/// COOLDOWN ─冷卻結束且仍達門檻且允許重複→ 再次通知（Repeat）
/// COOLDOWN ─達恢復值→ RECOVERING ─持續達 recoveryDuration→ RECOVERED（通知）→ NORMAL
/// RECOVERING ─離開恢復值或資料不可用→ COOLDOWN（恢復計時歸零，冷卻計時延續）
/// </code>
/// 持續、冷卻、恢復時間為 0 表示立即。資料不可用時 COOLDOWN 維持（不結束、不重新通知）。
/// </summary>
public static class AlertStateMachine
{
    /// <summary>依本次判斷更新狀態，回傳本次產生的事件；沒有事件時回傳 null。</summary>
    public static AlertEventType? Update(AlertTargetState target, AlertCondition condition, TimeSpan now, AlertSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        TimeSpan duration = TimeSpan.FromSeconds(settings.DurationSeconds);
        TimeSpan cooldown = TimeSpan.FromSeconds(settings.CooldownSeconds);
        TimeSpan recovery = TimeSpan.FromSeconds(settings.RecoveryDurationSeconds);

        switch (target.State)
        {
            case AlertState.Normal:
                if (condition != AlertCondition.Breach)
                {
                    return null;
                }

                target.State = AlertState.Pending;
                target.PendingSince = now;
                goto case AlertState.Pending;

            case AlertState.Pending:
                if (condition != AlertCondition.Breach)
                {
                    target.State = AlertState.Normal;
                    target.PendingSince = null;
                    return null;
                }

                if (now - target.PendingSince!.Value < duration)
                {
                    return null;
                }

                target.State = AlertState.Cooldown;
                target.LastNotified = now;
                return AlertEventType.Warning;

            case AlertState.Cooldown:
                if (condition == AlertCondition.Recovered)
                {
                    target.State = AlertState.Recovering;
                    target.RecoveringSince = now;
                    goto case AlertState.Recovering;
                }

                if (condition == AlertCondition.Breach
                    && settings.RepeatNotificationAfterCooldown
                    && now - target.LastNotified!.Value >= cooldown)
                {
                    target.LastNotified = now;
                    return AlertEventType.Repeat;
                }

                return null;

            case AlertState.Recovering:
                if (condition != AlertCondition.Recovered)
                {
                    target.State = AlertState.Cooldown;
                    target.RecoveringSince = null;
                    return null;
                }

                if (now - target.RecoveringSince!.Value < recovery)
                {
                    return null;
                }

                target.State = AlertState.Normal;
                target.PendingSince = null;
                target.RecoveringSince = null;
                target.LastNotified = null;
                return AlertEventType.Recovered;

            default:
                return null;
        }
    }
}
