namespace PerformanceMonitor.Application.Reports;

/// <summary>報告讀取的資料夾。</summary>
public enum ReportFolder
{
    /// <summary>CSV 資料目錄。</summary>
    Data,

    /// <summary>Log 目錄。</summary>
    Logs,
}

/// <summary>報告的檔案存取（Infrastructure 實作；測試以記憶體假物件取代）。</summary>
public interface IReportStore
{
    /// <summary>預設輸出目錄（資料根目錄下的 <c>Reports</c>）。</summary>
    string DefaultOutputDirectory { get; }

    /// <summary>
    /// 逐行讀取；檔案不存在時回傳 null。以共用讀取開啟（監控中也能讀），不修改原始檔。
    /// 讀取中發生的 I/O 錯誤在列舉時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。
    /// </summary>
    IEnumerable<string>? ReadLines(ReportFolder folder, string fileName);

    /// <summary>寫入報告（UTF-8；同名檔覆蓋），回傳完整路徑。</summary>
    string Save(string path, string content);
}
