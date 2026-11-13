using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// 提供最新的完整快照給 UI 或其他讀取端；畫面更新週期與取樣週期分開。
/// </summary>
public interface ISnapshotProvider
{
    /// <summary>最新完成的快照；尚未取樣時為 null。</summary>
    MonitorSnapshot? Latest { get; }

    /// <summary>每次完成取樣後觸發（在背景執行緒）。</summary>
    event EventHandler<MonitorSnapshot>? SnapshotPublished;
}
