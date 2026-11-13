using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// 保存最新快照並通知訂閱者。執行緒安全。
/// </summary>
public sealed class SnapshotStore : ISnapshotProvider
{
    private MonitorSnapshot? _latest;

    /// <inheritdoc />
    public event EventHandler<MonitorSnapshot>? SnapshotPublished;

    /// <inheritdoc />
    public MonitorSnapshot? Latest => Volatile.Read(ref _latest);

    /// <summary>發布新快照。</summary>
    public void Publish(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _latest, snapshot);
        SnapshotPublished?.Invoke(this, snapshot);
    }
}
