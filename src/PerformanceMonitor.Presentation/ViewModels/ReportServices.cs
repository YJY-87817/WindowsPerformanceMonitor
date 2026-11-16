using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>產生報告（由組裝點以 <see cref="ReportService"/> 在背景執行緒實作，測試以假物件取代）。</summary>
public interface IReportRunner
{
    /// <summary>報告預設輸出目錄。</summary>
    string OutputDirectory { get; }

    /// <summary>在背景產生報告；寫入失敗時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。</summary>
    Task<ReportResult> GenerateAsync(ReportPeriod period);
}

/// <summary>以系統預設程式開啟檔案或資料夾（由 App 實作）。</summary>
public interface IShellLauncher
{
    /// <summary>開啟；失敗時擲出例外。</summary>
    void Open(string path);
}
