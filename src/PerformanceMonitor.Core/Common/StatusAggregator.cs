using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Common;

/// <summary>
/// 多裝置模組（磁碟、GPU、網路等）的整體狀態規則（舊規格 v1.5 §3.1.1）。
/// </summary>
public static class StatusAggregator
{
    /// <summary>
    /// 沒有裝置 → Unavailable；任一 Error → Error；其餘全部 Unavailable → Unavailable；
    /// 排除 Unavailable 後全部 WarmingUp → WarmingUp；其餘 → OK。
    /// </summary>
    public static MonitorStatus Aggregate(IEnumerable<MonitorStatus> deviceStatuses)
    {
        ArgumentNullException.ThrowIfNull(deviceStatuses);
        MonitorStatus[] statuses = [.. deviceStatuses];

        if (statuses.Length == 0)
        {
            return MonitorStatus.Unavailable;
        }

        if (statuses.Contains(MonitorStatus.Error))
        {
            return MonitorStatus.Error;
        }

        MonitorStatus[] available = [.. statuses.Where(s => s != MonitorStatus.Unavailable)];
        if (available.Length == 0)
        {
            return MonitorStatus.Unavailable;
        }

        return available.All(s => s == MonitorStatus.WarmingUp) ? MonitorStatus.WarmingUp : MonitorStatus.OK;
    }
}
