using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>設定檔存取（由組裝點以 Infrastructure 實作，測試以假物件取代）。</summary>
public interface ISettingsService
{
    /// <summary>設定檔完整路徑。</summary>
    string SettingsPath { get; }

    /// <summary>檢查設定中的資料／Log 目錄是否可寫入；回傳錯誤（沒有錯誤時為空）。</summary>
    IReadOnlyList<string> CheckDirectories(AppSettings settings);

    /// <summary>寫入設定檔（舊檔另存 .bak）。失敗時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。</summary>
    void Save(AppSettings settings);
}

/// <summary>重新啟動程式：停止監控（補寫與停止 Log）→ 啟動新的程式 → 結束目前程式（由 App 實作）。</summary>
public interface IApplicationRestart
{
    /// <summary>重新啟動。</summary>
    Task RestartAsync();
}
