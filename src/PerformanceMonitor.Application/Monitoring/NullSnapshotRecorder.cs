using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// 不寫入任何紀錄。CSV 實作完成前（Phase 1b）作為預設值。
/// </summary>
public sealed class NullSnapshotRecorder : ISnapshotRecorder
{
    /// <inheritdoc />
    public Task RecordAsync(MonitorSnapshot snapshot, bool includeProcesses, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
