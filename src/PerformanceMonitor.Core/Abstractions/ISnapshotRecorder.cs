using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// 將快照寫入歷史紀錄（CSV）。實作需自行處理檔案錯誤，不得擲出例外中止監控。
/// </summary>
public interface ISnapshotRecorder
{
    /// <summary>寫入一筆快照。</summary>
    /// <param name="snapshot">完整快照。</param>
    /// <param name="includeProcesses">本次是否同時寫入程序紀錄。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    Task RecordAsync(MonitorSnapshot snapshot, bool includeProcesses, CancellationToken cancellationToken);
}
