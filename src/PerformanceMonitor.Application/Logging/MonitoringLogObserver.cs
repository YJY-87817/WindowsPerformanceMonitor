using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Application.Logging;

/// <summary>
/// 依每筆快照寫 Log：各模組與各裝置的狀態變化交給 <see cref="StatusLogTracker"/> 去重；
/// LoopOverrun 首次寫入 Application Log，持續發生時每 10 分鐘彙總一次。
/// </summary>
public sealed class MonitoringLogObserver(IAppLog log, StatusLogTracker tracker, IClock clock)
{
    private const string Source = "Monitor";

    private TimeSpan? _overrunPeriodStart;
    private int _overrunCount;

    /// <summary>處理一筆快照。</summary>
    public void Observe(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (ModuleResult result in snapshot.Results)
        {
            // 多裝置模組沒有模組層級錯誤代碼時，整體狀態只是裝置狀態的彙總，改由各裝置分別記錄。
            bool aggregateOnly = result is IDeviceResult && result.ErrorCode is null;
            MonitorStatus moduleStatus = aggregateOnly ? MonitorStatus.OK : result.Status;
            tracker.Report(result.Module, null, moduleStatus, result.ErrorCode, result.ErrorMessage);

            if (result is IDeviceResult devices)
            {
                foreach (DeviceItem device in devices.Devices)
                {
                    tracker.Report(result.Module, device.DeviceId, device.Status, device.ErrorCode, device.ErrorMessage);
                }
            }
        }

        if (snapshot.LoopOverrun)
        {
            ObserveOverrun(snapshot);
        }
    }

    private void ObserveOverrun(MonitorSnapshot snapshot)
    {
        TimeSpan now = clock.Elapsed;
        string duration = snapshot.SampleDuration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
        if (_overrunPeriodStart is null)
        {
            log.Warn(Source, $"取樣耗時 {duration} ms，超過取樣週期（LoopOverrun）；錯過的排程點不補跑。");
            _overrunPeriodStart = now;
            return;
        }

        _overrunCount++;
        if (now - _overrunPeriodStart.Value >= StatusLogTracker.SummaryInterval)
        {
            log.Warn(Source, $"過去 10 分鐘又發生 {_overrunCount} 次 LoopOverrun（最近一次耗時 {duration} ms）。");
            _overrunCount = 0;
            _overrunPeriodStart = now;
        }
    }
}
