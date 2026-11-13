using CommunityToolkit.Mvvm.ComponentModel;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>畫面更新時各頁取得的資料（同一次更新共用，畫面更新週期與取樣週期分開）。</summary>
/// <param name="Snapshot">最新快照；尚未取樣時為 null。</param>
/// <param name="Alerts">告警狀態。</param>
public sealed record RefreshData(MonitorSnapshot? Snapshot, AlertView Alerts);

/// <summary>左側導覽的一頁。</summary>
/// <param name="title">頁面名稱。</param>
public abstract class PageViewModel(string title) : ObservableObject
{
    /// <summary>頁面名稱。</summary>
    public string Title { get; } = title;

    /// <summary>以最新資料更新（在 UI 執行緒呼叫）。</summary>
    public abstract void Refresh(RefreshData data);
}
