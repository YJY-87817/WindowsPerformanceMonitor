using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Alerts;

/// <summary>
/// 依每筆快照更新所有告警對象：事件寫入 Application Log（告警／再次通知為 WARN、恢復為 INFO），
/// 保留最近 5 筆事件，並提供進行中的告警給 UI。快照中不再出現的對象（例如磁碟被移除）移除其狀態。
/// 告警只描述監控結果，不終止程序、不清理檔案、不變更系統設定。執行緒安全。
/// </summary>
public sealed class AlertService(AppSettings settings, IAppLog log, IClock clock) : IAlertProvider
{
    /// <summary>保留的最近事件數。</summary>
    public const int RecentLimit = 5;

    private const string Source = "Alert";

    private readonly Dictionary<string, Tracked> _targets = new(StringComparer.Ordinal);
    private readonly List<AlertEvent> _recent = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public AlertView Current
    {
        get
        {
            if (!settings.Alert.Enabled)
            {
                return new AlertView(false, [], []);
            }

            lock (_gate)
            {
                ActiveAlert[] active =
                [
                    .. _targets
                        .Where(t => t.Value.State.State is AlertState.Cooldown or AlertState.Recovering)
                        .Select(t => new ActiveAlert(t.Key, t.Value.State.State, t.Value.ValueText, t.Value.Threshold, t.Value.ActiveSince!.Value)),
                ];
                return new AlertView(true, active, [.. _recent]);
            }
        }
    }

    /// <summary>處理一筆快照，回傳本次產生的事件。</summary>
    public IReadOnlyList<AlertEvent> Observe(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!settings.Alert.Enabled)
        {
            return [];
        }

        TimeSpan now = clock.Elapsed;
        var events = new List<AlertEvent>();
        lock (_gate)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (AlertCheck check in AlertConditionEvaluator.Evaluate(snapshot, settings.Thresholds))
            {
                seen.Add(check.Target);
                if (!_targets.TryGetValue(check.Target, out Tracked? tracked))
                {
                    tracked = new Tracked();
                    _targets[check.Target] = tracked;
                }

                tracked.ValueText = check.ValueText;
                tracked.Threshold = check.Threshold;
                AlertEventType? type = AlertStateMachine.Update(tracked.State, check.Condition, now, settings.Alert);
                if (type is null)
                {
                    continue;
                }

                if (type == AlertEventType.Warning)
                {
                    // 告警開始時間以進入 PENDING 的取樣時間計（換算為本機時間）。
                    TimeSpan pendingFor = now - (tracked.State.PendingSince ?? now);
                    tracked.ActiveSince = snapshot.TimestampLocal - pendingFor;
                }

                var alertEvent = new AlertEvent(snapshot.TimestampLocal, check.Target, type.Value, check.ValueText, check.Threshold);
                events.Add(alertEvent);
                _recent.Insert(0, alertEvent);
                if (_recent.Count > RecentLimit)
                {
                    _recent.RemoveRange(RecentLimit, _recent.Count - RecentLimit);
                }

                WriteLog(alertEvent);
            }

            foreach (string removed in _targets.Keys.Where(k => !seen.Contains(k)).ToArray())
            {
                _targets.Remove(removed);
            }
        }

        return events;
    }

    private void WriteLog(AlertEvent e)
    {
        string label = e.Type switch
        {
            AlertEventType.Warning => "告警",
            AlertEventType.Repeat => "告警持續（冷卻結束後再次通知）",
            _ => "已恢復",
        };
        string message = $"{label} {e.Target}：目前 {e.ValueText}，門檻 {e.Threshold}";
        if (e.Type == AlertEventType.Recovered)
        {
            log.Info(Source, message);
        }
        else
        {
            log.Warn(Source, message);
        }
    }

    private sealed class Tracked
    {
        public AlertTargetState State { get; } = new();

        public string ValueText { get; set; } = "N/A";

        public string Threshold { get; set; } = string.Empty;

        public DateTimeOffset? ActiveSince { get; set; }
    }
}
