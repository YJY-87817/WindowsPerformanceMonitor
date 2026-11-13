namespace PerformanceMonitor.App.ViewModels;

/// <summary>
/// 主視窗 ViewModel。Phase 0 只提供骨架文字；Phase 3 改用 CommunityToolkit.Mvvm 實作即時資料。
/// </summary>
public sealed class MainViewModel
{
    /// <summary>視窗標題。</summary>
    public string Title { get; } = "Windows Performance Monitor";

    /// <summary>顯示訊息。</summary>
    public string Message { get; } = "Phase 0 骨架：監控功能將於後續 Phase 加入。";
}
