namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>啟動時讀取設定與檢查資料目錄的結果（由組裝點提供）。</summary>
/// <param name="Errors">錯誤；有錯誤時不開始監控（規格 v2.0 §5、§7.1）。</param>
/// <param name="Warnings">警告（例如找不到設定檔、使用預設值）。</param>
/// <param name="SettingsFileValid">設定檔是否正確；有錯誤時設定頁顯示內建預設值並提醒儲存會取代原檔。</param>
public sealed record StartupStatus(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, bool SettingsFileValid = true)
{
    /// <summary>是否可以開始監控。</summary>
    public bool CanMonitor => Errors.Count == 0;
}
